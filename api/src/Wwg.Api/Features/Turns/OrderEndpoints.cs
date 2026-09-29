using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Turns;

internal static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/armies/{id:guid}/turns", ListArmyTurnsAsync)
            .WithName("ListArmyTurns")
            .WithTags("Turns")
            .RequireCampaignAccess(CampaignAccess.Commander, CampaignRouteId.Army);

        var orders = app.MapGroup("/api/army-turns/{id:guid}/orders/{unitId:guid}")
            .WithTags("Turns");
        orders
            .MapPut("", GiveOrderAsync)
            .WithName("GiveOrder")
            .RequireCampaignAccess(CampaignAccess.OwnCommander, CampaignRouteId.ArmyTurn)
            .ProducesProblem(StatusCodes.Status409Conflict);
        orders
            .MapDelete("", UndoOrderAsync)
            .WithName("UndoOrder")
            .RequireCampaignAccess(CampaignAccess.OwnCommander, CampaignRouteId.ArmyTurn)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>
    /// An army's turns, newest first, with their orders and history (its commander, the Umpire and
    /// Admins: the visibility rule).
    /// </summary>
    internal static async Task<Ok<List<ArmyTurnDetails>>> ListArmyTurnsAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        // Three flat queries, put together here: SQLite can't run the nested lists as one.
        var turns = await db
            .ArmyTurns.AsNoTracking()
            .Where(t => t.ArmyId == id)
            .OrderByDescending(t => t.CampaignTurn.Number)
            .Select(t => new
            {
                t.Id,
                t.CampaignTurn.Number,
                Open = t.CampaignTurn.ClosedAt == null,
                t.Status,
                t.SubmittedAt,
                t.CompletedAt,
            })
            .ToListAsync(cancellationToken);
        var orders = (
            await db
                .UnitOrders.AsNoTracking()
                .Where(o => o.ArmyTurn.ArmyId == id)
                .OrderBy(o => o.Unit.Name)
                .ThenBy(o => o.UnitId)
                .Select(o => new
                {
                    o.ArmyTurnId,
                    o.UnitId,
                    o.Kind,
                    o.Latitude,
                    o.Longitude,
                })
                .ToListAsync(cancellationToken)
        ).ToLookup(o => o.ArmyTurnId);
        var history = await HistoryAsync(db, id, cancellationToken);

        return TypedResults.Ok(
            turns
                .Select(t => new ArmyTurnDetails(
                    t.Id,
                    t.Number,
                    t.Open,
                    t.Status,
                    t.SubmittedAt,
                    t.CompletedAt,
                    [
                        .. orders[t.Id]
                            .Select(o => new UnitPosition(
                                o.UnitId,
                                id,
                                t.Number,
                                t.Status,
                                o.Kind,
                                o.Latitude,
                                o.Longitude
                            )),
                    ],
                    [.. history[t.Id]]
                ))
                .ToList()
        );
    }

    /// <summary>What happened to an army's turns (submitted, sent back...), by army turn.</summary>
    private static async Task<ILookup<Guid, ArmyTurnEventDto>> HistoryAsync(
        WwgDbContext db,
        Guid armyId,
        CancellationToken cancellationToken
    ) =>
        (
            await db
                .ArmyTurnEvents.AsNoTracking()
                .Where(e => e.ArmyTurn.ArmyId == armyId)
                .OrderBy(e => e.At)
                .ThenBy(e => e.Id)
                .Select(e => new
                {
                    e.ArmyTurnId,
                    Event = new ArmyTurnEventDto(
                        e.Kind,
                        e.At,
                        e.ByUser == null ? null : e.ByUser.FirstName + " " + e.ByUser.LastName,
                        e.Note,
                        e.UnitNotes.Select(n => new UnitNoteDto(n.UnitId, n.Text)).ToList()
                    ),
                })
                .ToListAsync(cancellationToken)
        ).ToLookup(e => e.ArmyTurnId, e => e.Event);

    /// <summary>
    /// Gives a unit its order for the turn (the army's commander, while the turn is a Draft in
    /// the open campaign turn): Move, inside the campaign's area and within the unit type's limit
    /// of where it is (a straight line); or Hold, where it is. Replaces any order it had.
    /// </summary>
    internal static async Task<
        Results<Ok<UnitPosition>, ValidationProblem, ProblemHttpResult>
    > GiveOrderAsync(
        Guid id,
        Guid unitId,
        GiveOrderRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var turn = await OpenDraftAsync(db, id, cancellationToken);
        if (turn is null)
        {
            return NotADraft();
        }

        var unit = await db
            .Units.AsNoTracking()
            .Where(u => u.Id == unitId && u.ArmyId == turn.ArmyId)
            .Select(u => new { u.Type, u.Army.CampaignId })
            .SingleOrDefaultAsync(cancellationToken);
        if (unit is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "That unit isn't in this army."
            );
        }

        var current = await TurnRules.CurrentPositionAsync(db, unitId, cancellationToken);
        if (current is null)
        {
            return Conflict("Not placed", "The Umpire hasn't placed this unit on the map yet.");
        }

        var (problem, at) = await TargetAsync(
            db,
            request,
            unit.CampaignId,
            unit.Type,
            current.Value,
            cancellationToken
        );
        if (problem is not null)
        {
            return Invalid("latitude", problem);
        }

        return TypedResults.Ok(
            await SaveOrderAsync(db, id, turn, unitId, request.Kind, at, cancellationToken)
        );
    }

    /// <summary>Takes back a unit's order for the turn (the army's commander, while it's a Draft).</summary>
    internal static async Task<Results<NoContent, ProblemHttpResult>> UndoOrderAsync(
        Guid id,
        Guid unitId,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (await OpenDraftAsync(db, id, cancellationToken) is null)
        {
            return NotADraft();
        }

        await db
            .UnitOrders.Where(o => o.ArmyTurnId == id && o.UnitId == unitId)
            .ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<UnitPosition> SaveOrderAsync(
        WwgDbContext db,
        Guid armyTurnId,
        OpenDraft turn,
        Guid unitId,
        OrderKind kind,
        (double Latitude, double Longitude) at,
        CancellationToken cancellationToken
    )
    {
        var order =
            await db.UnitOrders.SingleOrDefaultAsync(
                o => o.ArmyTurnId == armyTurnId && o.UnitId == unitId,
                cancellationToken
            )
            ?? db.UnitOrders.Add(new UnitOrder { ArmyTurnId = armyTurnId, UnitId = unitId }).Entity;
        (order.Kind, order.Latitude, order.Longitude) = (kind, at.Latitude, at.Longitude);
        await db.SaveChangesAsync(cancellationToken);
        return new UnitPosition(
            unitId,
            turn.ArmyId,
            turn.Number,
            ArmyTurnStatus.Draft,
            kind,
            at.Latitude,
            at.Longitude
        );
    }

    private sealed record OpenDraft(Guid ArmyId, int Number);

    /// <summary>The army turn, if it's a Draft in the open campaign turn (after setup); else null.</summary>
    private static Task<OpenDraft?> OpenDraftAsync(
        WwgDbContext db,
        Guid id,
        CancellationToken cancellationToken
    ) =>
        db
            .ArmyTurns.AsNoTracking()
            .Where(t =>
                t.Id == id
                && t.Status == ArmyTurnStatus.Draft
                && t.CampaignTurn.ClosedAt == null
                && t.CampaignTurn.Number > 0
            )
            .Select(t => new OpenDraft(t.ArmyId, t.CampaignTurn.Number))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Where the order leaves the unit: for a Hold, where it is; for a Move, where it goes, if
    /// that's allowed. Else why not.
    /// </summary>
    private static async Task<(
        string? Problem,
        (double Latitude, double Longitude) At
    )> TargetAsync(
        WwgDbContext db,
        GiveOrderRequest request,
        Guid campaignId,
        UnitType type,
        (double Latitude, double Longitude) current,
        CancellationToken cancellationToken
    )
    {
        if (request.Kind == OrderKind.Hold)
        {
            return (null, current);
        }

        if (request is not { Latitude: { } lat, Longitude: { } lon })
        {
            return ("Say where the unit moves to.", current);
        }

        return (
            await MoveProblemAsync(db, campaignId, type, current, (lat, lon), cancellationToken),
            (lat, lon)
        );
    }

    /// <summary>Why a move isn't allowed (outside the area, or too far), or null if it is.</summary>
    private static async Task<string?> MoveProblemAsync(
        WwgDbContext db,
        Guid campaignId,
        UnitType type,
        (double Latitude, double Longitude) from,
        (double Latitude, double Longitude) to,
        CancellationToken cancellationToken
    )
    {
        var map = await db
            .CampaignMaps.AsNoTracking()
            .SingleOrDefaultAsync(m => m.CampaignId == campaignId, cancellationToken);
        if (
            map is { West: { } west, South: { } south, East: { } east, North: { } north }
            && (
                to.Latitude < south
                || to.Latitude > north
                || to.Longitude < west
                || to.Longitude > east
            )
        )
        {
            return "That's outside the campaign's area.";
        }

        var limit =
            await db
                .MovementLimits.Where(l => l.CampaignId == campaignId && l.UnitType == type)
                .Select(l => (int?)l.Metres)
                .SingleOrDefaultAsync(cancellationToken)
            ?? CampaignMaps.DefaultMetres[type];
        // A metre's grace, for rounding between the browser and here.
        return Geo.Metres(from.Latitude, from.Longitude, to.Latitude, to.Longitude) > limit + 1
            ? "That's further than the unit can move in a turn."
            : null;
    }

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] }
        );

    private static ProblemHttpResult NotADraft() =>
        Conflict(
            "Not a draft",
            "Orders can only change while the army's turn is a draft in the open turn."
        );

    private static ProblemHttpResult Conflict(string title, string detail) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: title,
            detail: detail
        );
}
