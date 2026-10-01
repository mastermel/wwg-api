using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Features.Maps;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Turns;

internal static class TurnEndpoints
{
    public static IEndpointRouteBuilder MapTurnEndpoints(this IEndpointRouteBuilder app)
    {
        var campaign = app.MapGroup("/api/campaigns/{id:guid}").WithTags("Turns");
        campaign
            .MapGet("/turns", ListTurnsAsync)
            .WithName("ListTurns")
            .RequireCampaignAccess(CampaignAccess.Member);
        campaign
            .MapGet("/positions", ListPositionsAsync)
            .WithName("ListPositions")
            .RequireCampaignAccess(CampaignAccess.Member);
        campaign
            .MapPost("/start", StartCampaignAsync)
            .WithName("StartCampaign")
            .RequireCampaignAccess(CampaignAccess.Umpire)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var placement = app.MapGroup("/api/army-units/{id:guid}/placement").WithTags("Turns");
        placement
            .MapPut("", PlaceUnitAsync)
            .WithName("PlaceUnit")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.ArmyUnit)
            .ProducesProblem(StatusCodes.Status409Conflict);
        placement
            .MapDelete("", UnplaceUnitAsync)
            .WithName("UnplaceUnit")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.ArmyUnit)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>
    /// The campaign's turns and where each stands (every member). The Umpire sees every army's
    /// turn, and what stops the campaign (or, once it's running, the next turn) starting; a
    /// commander sees their own army's, and everyone the counts.
    /// </summary>
    internal static async Task<Ok<CampaignTurnsResponse>> ListTurnsAsync(
        Guid id,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var access = httpContext.CampaignContext();
        var visible = Visibility.VisibleArmyIds(db, access);
        var turns = await db
            .CampaignTurns.AsNoTracking()
            .Where(t => t.CampaignId == id)
            .OrderBy(t => t.Number)
            .Select(t => new CampaignTurnSummary(
                t.Number,
                t.OpenedAt,
                t.ClosedAt,
                db.ArmyTurns.Count(a =>
                    a.CampaignTurnId == t.Id && a.Status != ArmyTurnStatus.Draft
                ),
                db.ArmyTurns.Count(a => a.CampaignTurnId == t.Id),
                db.ArmyTurns.Where(a => a.CampaignTurnId == t.Id && visible.Contains(a.ArmyId))
                    .OrderBy(a => a.Army.Name)
                    .ThenBy(a => a.ArmyId)
                    .Select(a => new ArmyTurnSummary(
                        a.ArmyId,
                        a.Status,
                        a.SubmittedAt,
                        a.CompletedAt
                    ))
                    .ToList()
            ))
            .ToListAsync(cancellationToken);
        var calendar = await CalendarEndpoints.LoadAsync(db, id, cancellationToken);
        turns =
        [
            .. turns.Select(t =>
                TurnParts.Of(calendar.FirstTurnPart, calendar.StartDate, t.Number)
                    is var (part, date)
                    ? t with
                    {
                        Part = part,
                        Date = date,
                    }
                    : t
            ),
        ];

        var open = turns.SingleOrDefault(t => t.ClosedAt is null)?.Number ?? 0;
        var stage = open == 0 ? CampaignStage.Setup : CampaignStage.Running;
        var problems =
            !access.CanManage ? []
            : stage == CampaignStage.Setup ? await StartProblemsAsync(db, id, cancellationToken)
            : await TurnActionEndpoints.NextTurnProblemsAsync(
                db,
                id,
                await db
                    .CampaignTurns.Where(t => t.CampaignId == id && t.ClosedAt == null)
                    .Select(t => t.Id)
                    .SingleAsync(cancellationToken),
                cancellationToken
            );
        return TypedResults.Ok(new CampaignTurnsResponse(stage, open, turns, problems));
    }

    /// <summary>
    /// Where the campaign's units are, as the caller may see them (the visibility rule): the Umpire
    /// every army's, a commander their own. Without <paramref name="turn"/>, where they are now
    /// (each unit's latest Completed turn); with a closed turn, where they were after it; with the
    /// open one, their orders in it, whatever its status.
    /// </summary>
    internal static async Task<Ok<List<UnitPosition>>> ListPositionsAsync(
        Guid id,
        int? turn,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var visible = Visibility.VisibleArmyIds(db, httpContext.CampaignContext());
        var orders = db.UnitOrders.AsNoTracking().Where(o => visible.Contains(o.ArmyTurn.ArmyId));
        var closed =
            turn is { } asked
            && await db.CampaignTurns.AnyAsync(
                t => t.CampaignId == id && t.Number == asked && t.ClosedAt != null,
                cancellationToken
            );
        orders = turn switch
        {
            // The open turn: its orders, whatever its army turns' status.
            { } number when !closed => orders.Where(o => o.ArmyTurn.CampaignTurn.Number == number),
            // A closed turn: where each unit was after it (every army turn in it is Completed),
            // which for a unit placed since it was ordered is its placement.
            { } number => orders.Where(o =>
                o.ArmyTurn.CampaignTurn.Number <= number
                && !db.UnitOrders.Any(later =>
                    later.UnitId == o.UnitId
                    && later.ArmyTurn.CampaignTurn.Number <= number
                    && later.ArmyTurn.CampaignTurn.Number > o.ArmyTurn.CampaignTurn.Number
                )
            ),
            // Now: each unit's latest Completed order.
            null => orders.Where(o =>
                o.ArmyTurn.Status == ArmyTurnStatus.Completed
                && !db.UnitOrders.Any(later =>
                    later.UnitId == o.UnitId
                    && later.ArmyTurn.Status == ArmyTurnStatus.Completed
                    && later.ArmyTurn.CampaignTurn.Number > o.ArmyTurn.CampaignTurn.Number
                )
            ),
        };

        var rows = await orders
            .OrderBy(o => o.ArmyUnit.Name)
            .ThenBy(o => o.UnitId)
            .Select(o => new OrderRow(
                o.UnitId,
                o.ArmyTurn.ArmyId,
                o.ArmyTurn.CampaignTurn.Number,
                o.ArmyTurn.Status,
                o.Kind,
                o.Q,
                o.R,
                o.Path,
                o.ByUmpire,
                o.Progress,
                o.ForceMarch,
                o.LivesOffTheLand
            ))
            .ToListAsync(cancellationToken);
        // Orders need a grid; without an area there are none.
        var grid = await CampaignMaps.GridAsync(db, id, cancellationToken);
        return TypedResults.Ok(
            grid is null ? [] : rows.Select(r => Positions.Of(grid, r)).ToList()
        );
    }

    /// <summary>
    /// Places a unit in a hex of the grid (Umpire or Admin): while setting up, in turn 0; once
    /// started, a unit added since that has no position yet (it's placed where its army last was, in the
    /// army's turn in the last closed campaign turn, which can't be reverted). Inside the
    /// campaign's area, which must be set.
    /// </summary>
    internal static async Task<
        Results<Ok<UnitPosition>, ValidationProblem, ProblemHttpResult>
    > PlaceUnitAsync(
        Guid id,
        PlaceUnitRequest request,
        WwgDbContext db,
        TimeProvider time,
        CancellationToken cancellationToken
    )
    {
        var unit = await db
            .ArmyUnits.Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.ArmyId,
                u.Army.CampaignId,
            })
            .SingleOrGoneAsync(cancellationToken);
        var grid = await CampaignMaps.GridAsync(db, unit.CampaignId, cancellationToken);
        if (grid is null)
        {
            return Conflict("No map area", "Choose the map's area first, in the map settings.");
        }

        var hex = new Hex(request.Q, request.R);
        if (!grid.Contains(hex))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["q"] = ["That's outside the campaign's area."],
                }
            );
        }

        var armyTurn = await PlacementTurnAsync(
            db,
            time,
            unit.CampaignId,
            unit.ArmyId,
            id,
            cancellationToken
        );
        if (armyTurn is null)
        {
            return Conflict(
                "Already placed",
                "The campaign has started: units already on the map move by their orders."
            );
        }

        return TypedResults.Ok(
            await SavePlacementAsync(db, grid, armyTurn, unit.ArmyId, id, hex, cancellationToken)
        );
    }

    /// <summary>Takes a unit off the map again (Umpire or Admin), while setting up only.</summary>
    internal static async Task<Results<NoContent, ProblemHttpResult>> UnplaceUnitAsync(
        Guid id,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var campaignId = httpContext.CampaignContext().CampaignId;
        if (await TurnRules.HasStartedAsync(db, campaignId, cancellationToken))
        {
            return Conflict(
                "Campaign started",
                "ArmyUnits can only be taken off the map while setting up."
            );
        }

        await db
            .UnitOrders.Where(o => o.UnitId == id && o.ArmyTurn.CampaignTurn.Number == 0)
            .ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Starts the campaign (Umpire or Admin): turn 0 closes, with every army's placements as where
    /// its units are, and turn 1 opens, a Draft for every army. 409 if it has started already, or
    /// with what's stopping it (no area, no armies, an army without a side, units not placed).
    /// </summary>
    internal static async Task<
        Results<Ok<CampaignTurnsResponse>, ProblemHttpResult>
    > StartCampaignAsync(
        Guid id,
        WwgDbContext db,
        TimeProvider time,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var setup = await TurnRules.SetupTurnAsync(db, time, id, cancellationToken);
        if (setup is null)
        {
            return Conflict("Already started", "The campaign has already started.");
        }

        var problems = await StartProblemsAsync(db, id, cancellationToken);
        if (problems.Count > 0)
        {
            return Conflict("Not ready to start", string.Join(" ", problems));
        }

        var now = time.GetUtcNow().UtcDateTime;
        var first = new CampaignTurn
        {
            CampaignId = id,
            Number = 1,
            OpenedAt = now,
        };
        db.CampaignTurns.Add(first);
        foreach (
            var armyId in await db
                .Armies.Where(a => a.CampaignId == id)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken)
        )
        {
            var placements = await TurnRules.ArmyTurnAsync(db, setup, armyId, cancellationToken);
            (placements.Status, placements.CompletedAt) = (ArmyTurnStatus.Completed, now);
            db.ArmyTurns.Add(
                new ArmyTurn
                {
                    CampaignTurnId = first.Id,
                    ArmyId = armyId,
                    Status = ArmyTurnStatus.Draft,
                }
            );
        }

        setup.ClosedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return await ListTurnsAsync(id, db, httpContext, cancellationToken);
    }

    /// <summary>What stops the campaign starting, in sentences; empty when it's ready.</summary>
    internal static async Task<List<string>> StartProblemsAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var problems = new List<string>();
        if (
            !await db.CampaignMaps.AnyAsync(
                m => m.CampaignId == campaignId && m.West != null,
                cancellationToken
            )
        )
        {
            problems.Add("Choose the map's area.");
        }

        var armies = await db
            .Armies.AsNoTracking()
            .Where(a => a.CampaignId == campaignId)
            .OrderBy(a => a.Name)
            .Select(a => a.Name)
            .ToListAsync(cancellationToken);
        if (armies.Count == 0)
        {
            problems.Add("Add at least one army.");
        }

        var unplaced = await db.ArmyUnits.CountAsync(
            u =>
                u.Army.CampaignId == campaignId
                && !db.UnitOrders.Any(o => o.UnitId == u.Id && o.ArmyTurn.CampaignTurn.Number == 0),
            cancellationToken
        );
        if (unplaced > 0)
        {
            problems.Add(
                unplaced == 1 ? "Place 1 unit on the map." : $"Place {unplaced} units on the map."
            );
        }

        return problems;
    }

    /// <summary>
    /// The army turn a placement goes in: turn 0's while setting up; once started, the army's
    /// Completed turn in the last closed campaign turn (so reverting the open turn can't take it
    /// off the map), but only for a unit with no position yet. Null if it can't be placed.
    /// </summary>
    private static async Task<ArmyTurn?> PlacementTurnAsync(
        WwgDbContext db,
        TimeProvider time,
        Guid campaignId,
        Guid armyId,
        Guid unitId,
        CancellationToken cancellationToken
    )
    {
        if (await TurnRules.SetupTurnAsync(db, time, campaignId, cancellationToken) is { } setup)
        {
            return await TurnRules.ArmyTurnAsync(db, setup, armyId, cancellationToken);
        }

        var placed = await db.UnitOrders.AnyAsync(
            o => o.UnitId == unitId && o.ArmyTurn.Status == ArmyTurnStatus.Completed,
            cancellationToken
        );
        return placed
            ? null
            : await db
                .ArmyTurns.Where(t =>
                    t.ArmyId == armyId
                    && t.Status == ArmyTurnStatus.Completed
                    && t.CampaignTurn.ClosedAt != null
                )
                .OrderByDescending(t => t.CampaignTurn.Number)
                .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Saves the unit's placement as a Move order in <paramref name="armyTurn"/>.</summary>
    private static async Task<UnitPosition> SavePlacementAsync(
        WwgDbContext db,
        HexGrid grid,
        ArmyTurn armyTurn,
        Guid armyId,
        Guid id,
        Hex hex,
        CancellationToken cancellationToken
    )
    {
        var order =
            await db.UnitOrders.SingleOrDefaultAsync(
                o => o.ArmyTurnId == armyTurn.Id && o.UnitId == id,
                cancellationToken
            ) ?? db.UnitOrders.Add(new UnitOrder { ArmyTurnId = armyTurn.Id, UnitId = id }).Entity;
        order.Kind = OrderKind.Move;
        (order.Q, order.R) = (hex.Q, hex.R);
        await db.SaveChangesAsync(cancellationToken);

        var number = await db
            .CampaignTurns.Where(t => t.Id == armyTurn.CampaignTurnId)
            .Select(t => t.Number)
            .SingleAsync(cancellationToken);
        return Positions.Of(
            grid,
            new OrderRow(
                id,
                armyId,
                number,
                armyTurn.Status,
                OrderKind.Move,
                hex.Q,
                hex.R,
                [],
                false
            )
        );
    }

    private static ProblemHttpResult Conflict(string title, string detail) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: title,
            detail: detail
        );
}
