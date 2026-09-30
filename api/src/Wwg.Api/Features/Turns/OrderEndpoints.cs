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
            .RequireCampaignAccess(CampaignAccess.Commander, CampaignRouteId.ArmyTurn)
            .ProducesProblem(StatusCodes.Status409Conflict);
        orders
            .MapDelete("", UndoOrderAsync)
            .WithName("UndoOrder")
            .RequireCampaignAccess(CampaignAccess.Commander, CampaignRouteId.ArmyTurn)
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
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var grid = await CampaignMaps.GridAsync(
            db,
            httpContext.CampaignContext().CampaignId,
            cancellationToken
        );
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
        var orders = await OrdersAsync(db, id, cancellationToken);
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
                    grid is null
                        ? []
                        :
                        [
                            .. orders[t.Id]
                                .Select(o =>
                                    Positions.Of(
                                        grid,
                                        new OrderRow(
                                            o.UnitId,
                                            id,
                                            t.Number,
                                            t.Status,
                                            o.Kind,
                                            o.Q,
                                            o.R,
                                            o.Path,
                                            o.ByUmpire
                                        )
                                    )
                                ),
                        ],
                    [.. history[t.Id]]
                ))
                .ToList()
        );
    }

    /// <summary>An army's orders in all its turns, by army turn.</summary>
    private static async Task<ILookup<Guid, ArmyOrder>> OrdersAsync(
        WwgDbContext db,
        Guid armyId,
        CancellationToken cancellationToken
    ) =>
        (
            await db
                .UnitOrders.AsNoTracking()
                .Where(o => o.ArmyTurn.ArmyId == armyId)
                .OrderBy(o => o.Unit.Name)
                .ThenBy(o => o.UnitId)
                .Select(o => new ArmyOrder(
                    o.ArmyTurnId,
                    o.UnitId,
                    o.Kind,
                    o.Q,
                    o.R,
                    o.Path,
                    o.ByUmpire
                ))
                .ToListAsync(cancellationToken)
        ).ToLookup(o => o.ArmyTurnId);

    private sealed record ArmyOrder(
        Guid ArmyTurnId,
        Guid UnitId,
        OrderKind Kind,
        int Q,
        int R,
        List<Hex> Path,
        bool ByUmpire
    );

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
                        e.UnitNotes.OrderBy(n => n.CreatedAt)
                            .ThenBy(n => n.Id)
                            .Select(n => new UnitNoteDto(n.UnitId, n.Text))
                            .ToList()
                    ),
                })
                .ToListAsync(cancellationToken)
        ).ToLookup(e => e.ArmyTurnId, e => e.Event);

    /// <summary>
    /// Gives a unit its order for the turn: Move along a path of adjacent hexes from its current
    /// one, inside the grid (and for the commander, one its movement class can afford this turn:
    /// DESIGN.md §5.2); or Hold, where it is. Replaces any order it had. The army's commander gives orders while the turn is a
    /// Draft in the open campaign turn; the Umpire and Admins while it's a Draft or Submitted, on
    /// the commander's behalf (decision 0011), which goes in the turn's history.
    /// </summary>
    internal static async Task<
        Results<Ok<UnitPosition>, ValidationProblem, ProblemHttpResult>
    > GiveOrderAsync(
        Guid id,
        Guid unitId,
        GiveOrderRequest request,
        WwgDbContext db,
        TimeProvider time,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var byUmpire = httpContext.CampaignContext().CanManage;
        var turn = await EditableTurnAsync(db, id, byUmpire, cancellationToken);
        if (turn is null)
        {
            return NotEditable(byUmpire);
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

        // A placed unit's campaign has an area.
        var grid = (await CampaignMaps.GridAsync(db, unit.CampaignId, cancellationToken))!;
        var path = request.Kind == OrderKind.Hold ? [] : request.Path ?? [];
        if (
            PathProblem(
                grid,
                Movement.ClassOf(unit.Type),
                current.Value,
                request.Kind,
                path,
                byUmpire
            ) is
            { } problem
        )
        {
            return Invalid("path", problem);
        }

        var at = path.Count == 0 ? current.Value : path[^1];
        var saved = await StageOrderAsync(db, grid, turn, unitId, request.Kind, at, path, byUmpire);
        if (byUmpire)
        {
            var text = request.Kind == OrderKind.Hold ? "Set to hold." : "Set to move.";
            await RecordEditAsync(db, time, httpContext, id, unitId, text, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(saved);
    }

    /// <summary>
    /// Takes back a unit's order for the turn: the army's commander while it's a Draft; the
    /// Umpire and Admins while it's a Draft or Submitted, which goes in the turn's history.
    /// </summary>
    internal static async Task<Results<NoContent, ProblemHttpResult>> UndoOrderAsync(
        Guid id,
        Guid unitId,
        WwgDbContext db,
        TimeProvider time,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var byUmpire = httpContext.CampaignContext().CanManage;
        if (await EditableTurnAsync(db, id, byUmpire, cancellationToken) is null)
        {
            return NotEditable(byUmpire);
        }

        var removed = await db
            .UnitOrders.Where(o => o.ArmyTurnId == id && o.UnitId == unitId)
            .ExecuteDeleteAsync(cancellationToken);
        if (byUmpire && removed > 0)
        {
            await RecordEditAsync(
                db,
                time,
                httpContext,
                id,
                unitId,
                "Order taken back.",
                cancellationToken
            );
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.NoContent();
    }

    /// <summary>Adds or changes the unit's order (not saved), and says where it leaves the unit.</summary>
    private static async Task<UnitPosition> StageOrderAsync(
        WwgDbContext db,
        HexGrid grid,
        EditableTurn turn,
        Guid unitId,
        OrderKind kind,
        Hex at,
        IReadOnlyList<Hex> path,
        bool byUmpire
    )
    {
        var order =
            await db.UnitOrders.SingleOrDefaultAsync(o =>
                o.ArmyTurnId == turn.Id && o.UnitId == unitId
            ) ?? db.UnitOrders.Add(new UnitOrder { ArmyTurnId = turn.Id, UnitId = unitId }).Entity;
        (order.Kind, order.Q, order.R, order.Path, order.ByUmpire) = (
            kind,
            at.Q,
            at.R,
            [.. path],
            byUmpire
        );
        return Positions.Of(
            grid,
            new OrderRow(
                unitId,
                turn.ArmyId,
                turn.Number,
                turn.Status,
                kind,
                at.Q,
                at.R,
                order.Path,
                byUmpire
            )
        );
    }

    /// <summary>
    /// Records the Umpire's change to a unit's order in the turn's history (not saved): in the
    /// turn's latest event if it's an Edited one of theirs, so a run of changes is one event, with
    /// one note per unit (the latest change to it); otherwise in a new one.
    /// </summary>
    private static async Task RecordEditAsync(
        WwgDbContext db,
        TimeProvider time,
        HttpContext httpContext,
        Guid armyTurnId,
        Guid unitId,
        string text,
        CancellationToken cancellationToken
    )
    {
        var userId = httpContext.User.GetUserId();
        var now = time.GetUtcNow().UtcDateTime;
        var latest = await db
            .ArmyTurnEvents.Include(e => e.UnitNotes)
            .Where(e => e.ArmyTurnId == armyTurnId)
            .OrderByDescending(e => e.At)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var edit =
            latest is { Kind: ArmyTurnEventKind.Edited } && latest.ByUserId == userId
                ? latest
                : db
                    .ArmyTurnEvents.Add(
                        new ArmyTurnEvent
                        {
                            ArmyTurnId = armyTurnId,
                            Kind = ArmyTurnEventKind.Edited,
                            ByUserId = userId,
                        }
                    )
                    .Entity;
        edit.At = now;
        var note = edit.UnitNotes.FirstOrDefault(n => n.UnitId == unitId);
        if (note is null)
        {
            // Added to the set, not the event's list: our entities make their own IDs, so EF takes
            // one found only through a tracked event's list for a row that's already there.
            db.UnitNotes.Add(
                new UnitNote
                {
                    ArmyTurnEventId = edit.Id,
                    UnitId = unitId,
                    Text = text,
                }
            );
        }
        else
        {
            note.Text = text;
        }
    }

    private sealed record EditableTurn(Guid Id, Guid ArmyId, int Number, ArmyTurnStatus Status);

    /// <summary>
    /// The army turn, if its orders can change now: in the open campaign turn (after setup), and
    /// a Draft, or for the Umpire a Draft or Submitted. Else null.
    /// </summary>
    private static Task<EditableTurn?> EditableTurnAsync(
        WwgDbContext db,
        Guid id,
        bool byUmpire,
        CancellationToken cancellationToken
    ) =>
        db
            .ArmyTurns.AsNoTracking()
            .Where(t =>
                t.Id == id
                && (
                    t.Status == ArmyTurnStatus.Draft
                    || (byUmpire && t.Status == ArmyTurnStatus.Submitted)
                )
                && t.CampaignTurn.ClosedAt == null
                && t.CampaignTurn.Number > 0
            )
            .Select(t => new EditableTurn(t.Id, t.ArmyId, t.CampaignTurn.Number, t.Status))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Why a Move's path won't do, or null if it will: it needs a step; each step is next to the
    /// last (the first to the unit's hex) and inside the grid; and unless the Umpire gives it, the
    /// unit's class can afford it this turn. A Hold has no path.
    /// </summary>
    private static string? PathProblem(
        HexGrid grid,
        MovementClass movementClass,
        Hex from,
        OrderKind kind,
        IReadOnlyList<Hex> path,
        bool byUmpire
    )
    {
        if (kind == OrderKind.Hold)
        {
            return null;
        }

        if (path.Count == 0)
        {
            return "Say where the unit moves to.";
        }

        var at = from;
        foreach (var step in path)
        {
            if (!at.IsNextTo(step))
            {
                return "Each step of a move must be to the next hex.";
            }

            if (!grid.Contains(step))
            {
                return "That's outside the campaign's area.";
            }

            at = step;
        }

        return byUmpire || Movement.Affordable(Movement.PathCost(movementClass, from, path))
            ? null
            : "That's further than the unit can move in a turn.";
    }

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] }
        );

    private static ProblemHttpResult NotEditable(bool byUmpire) =>
        byUmpire
            ? Conflict(
                "Not now",
                "Orders can only change while the army's turn is a draft or submitted, in the "
                    + "open turn. Reopen an approved turn first."
            )
            : Conflict(
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
