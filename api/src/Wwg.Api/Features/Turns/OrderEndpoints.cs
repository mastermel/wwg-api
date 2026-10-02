using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Boats;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Supply;
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
                                .Select(o => Positions.Of(grid, o.Row(id, t.Number, t.Status))),
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
                .OrderBy(o => o.ArmyUnit.Name)
                .ThenBy(o => o.UnitId)
                .Select(o => new ArmyOrder(
                    o.ArmyTurnId,
                    o.UnitId,
                    o.Kind,
                    o.Q,
                    o.R,
                    o.Path,
                    o.ByUmpire,
                    o.Progress,
                    o.ForceMarch,
                    o.LivesOffTheLand,
                    o.Boats,
                    o.CarrierId
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
        bool ByUmpire,
        double? Progress,
        bool ForceMarch,
        bool LivesOffTheLand,
        List<Guid> Boats,
        Guid? CarrierId
    )
    {
        public OrderRow Row(Guid armyId, int turn, ArmyTurnStatus status) =>
            new(
                UnitId,
                armyId,
                turn,
                status,
                Kind,
                Q,
                R,
                Path,
                ByUmpire,
                Progress,
                ForceMarch,
                LivesOffTheLand,
                Boats,
                CarrierId
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
    /// DESIGN.md §5.2; by boat while it's on boats); Hold, where it is; Embark on the army's free
    /// boats in its hex, or Disembark from them (decision 0022), each the whole turn. A boat tied
    /// to a unit has no orders of its own: its follow that unit's. Replaces any order it had. The army's commander gives orders while the turn is a
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

        var (ordered, refused) = await OrderedAsync(db, unitId, turn.ArmyId, cancellationToken);
        if (ordered is null)
        {
            return refused!; // Set whenever there's no unit to order.
        }

        var (unit, state, embarked) = ordered;

        // A placed unit's campaign has an area.
        var grid = (await CampaignMaps.GridAsync(db, unit.CampaignId, cancellationToken))!;
        var aboard = embarked.Now.GetValueOrDefault(unitId);
        var move = MoveFor(grid, unit, turn, state, request, byUmpire, aboard);
        if (await OrderProblemAsync(db, move, cancellationToken) is { } why)
        {
            return Invalid(why.Field, why.Message);
        }

        var plan = await PlanAsync(db, move, cancellationToken);
        if (plan.Problem is { } problem)
        {
            return Invalid("path", problem);
        }

        var boats =
            move.Kind == OrderKind.Embark
                ? await BoatsToBoardAsync(db, turn, unitId, move, embarked, cancellationToken)
                : (Boats: [.. move.Aboard ?? []], Problem: null);
        if (boats.Problem is { } tooFew)
        {
            return Invalid("kind", tooFew);
        }

        var staged = StagedOrder.Of(move, plan, boats.Boats);
        var saved = await StageOrderAsync(db, grid, turn, unitId, staged);
        await TieBoatsAsync(db, turn, unitId, staged, state.At, cancellationToken);
        if (byUmpire)
        {
            await RecordEditAsync(
                db,
                time,
                httpContext,
                id,
                unitId,
                EditNote(move),
                cancellationToken
            );
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

        var embarked = await Embarkation.LoadAsync(
            db,
            httpContext.CampaignContext().CampaignId,
            cancellationToken
        );
        if (embarked.CarrierNow(unitId) is { } carrier)
        {
            return await TiedAsync(db, carrier, cancellationToken);
        }

        // Its boats' orders follow it.
        var removed = await db
            .UnitOrders.Where(o =>
                o.ArmyTurnId == id && (o.UnitId == unitId || o.CarrierId == unitId)
            )
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

    /// <summary>An order as it's staged: where the unit ends up, how it got there, and on what boats.</summary>
    private sealed record StagedOrder(
        OrderKind Kind,
        Hex At,
        IReadOnlyList<Hex> Path,
        double? Progress,
        bool ByUmpire,
        bool ForceMarch,
        bool LivesOffTheLand,
        IReadOnlyList<Guid> Boats
    )
    {
        public static StagedOrder Of(MoveRequest move, MovePlan plan, IReadOnlyList<Guid> boats) =>
            new(
                move.Kind,
                plan.At,
                move.Path,
                plan.Progress,
                move.ByUmpire,
                move.ForceMarch,
                move.LivesOffTheLand,
                boats
            );
    }

    /// <summary>Adds or changes the unit's order (not saved), and says where it leaves the unit.</summary>
    private static async Task<UnitPosition> StageOrderAsync(
        WwgDbContext db,
        HexGrid grid,
        EditableTurn turn,
        Guid unitId,
        StagedOrder staged
    )
    {
        var order =
            await db.UnitOrders.SingleOrDefaultAsync(o =>
                o.ArmyTurnId == turn.Id && o.UnitId == unitId
            ) ?? db.UnitOrders.Add(new UnitOrder { ArmyTurnId = turn.Id, UnitId = unitId }).Entity;
        (order.Kind, order.Q, order.R, order.Path, order.Progress, order.ByUmpire) = (
            staged.Kind,
            staged.At.Q,
            staged.At.R,
            [.. staged.Path],
            staged.Progress,
            staged.ByUmpire
        );
        (order.ForceMarch, order.LivesOffTheLand) = (staged.ForceMarch, staged.LivesOffTheLand);
        (order.Boats, order.CarrierId) = ([.. staged.Boats], null);
        return Positions.Of(
            grid,
            new OrderRow(
                unitId,
                turn.ArmyId,
                turn.Number,
                turn.Status,
                staged.Kind,
                staged.At.Q,
                staged.At.R,
                order.Path,
                staged.ByUmpire,
                staged.Progress,
                staged.ForceMarch,
                staged.LivesOffTheLand,
                order.Boats
            )
        );
    }

    /// <summary>
    /// Writes the orders of the boats tied to a unit with its own (not saved; decision 0022): they
    /// go where it goes, and stay where it was while it embarks, holds or lands. A boat no longer
    /// its loses its order.
    /// </summary>
    private static async Task TieBoatsAsync(
        WwgDbContext db,
        EditableTurn turn,
        Guid unitId,
        StagedOrder staged,
        Hex from,
        CancellationToken cancellationToken
    )
    {
        var boats = staged.Boats.ToList();
        var orders = await db
            .UnitOrders.Where(o =>
                o.ArmyTurnId == turn.Id && (o.CarrierId == unitId || boats.Contains(o.UnitId))
            )
            .ToListAsync(cancellationToken);
        db.UnitOrders.RemoveRange(orders.Where(o => !boats.Contains(o.UnitId)));
        var moves = staged.Kind == OrderKind.Move;
        foreach (var boat in boats)
        {
            var order =
                orders.Find(o => o.UnitId == boat)
                ?? db.UnitOrders.Add(new UnitOrder { ArmyTurnId = turn.Id, UnitId = boat }).Entity;
            var at = moves ? staged.At : from;
            (order.Kind, order.Q, order.R) = (moves ? OrderKind.Move : OrderKind.Hold, at.Q, at.R);
            (order.Path, order.Progress) = (
                moves ? [.. staged.Path] : [],
                moves ? staged.Progress : null
            );
            (order.ByUmpire, order.ForceMarch, order.LivesOffTheLand) = (
                staged.ByUmpire,
                false,
                false
            );
            (order.Boats, order.CarrierId) = ([], unitId);
        }
    }

    /// <summary>
    /// The army's free boats in the unit's hex it embarks on: as many as its points need, by the
    /// campaign's capacity, none tied to another unit or boarded by one this turn. Or why there
    /// aren't enough.
    /// </summary>
    private static async Task<(List<Guid> Boats, string? Problem)> BoatsToBoardAsync(
        WwgDbContext db,
        EditableTurn turn,
        Guid unitId,
        MoveRequest move,
        Embarked embarked,
        CancellationToken cancellationToken
    )
    {
        var capacity = (
            await CalendarEndpoints.LoadAsync(db, move.CampaignId, cancellationToken)
        ).BoatCapacity;
        var needed = BoatRules.Needed(move.Points, capacity);
        var claimed = (
            await db
                .UnitOrders.AsNoTracking()
                .Where(o =>
                    o.ArmyTurnId == turn.Id && o.Kind == OrderKind.Embark && o.UnitId != unitId
                )
                .Select(o => o.Boats)
                .ToListAsync(cancellationToken)
        )
            .SelectMany(boats => boats)
            .ToHashSet();
        // Where each of the army's boats is now: its order in the last closed turn.
        var free = (
            await db
                .UnitOrders.AsNoTracking()
                .Where(o =>
                    o.ArmyUnit.ArmyId == turn.ArmyId
                    && o.ArmyUnit.Type == UnitType.Boat
                    && o.ArmyTurn.CampaignTurn.ClosedAt != null
                )
                .Select(o => new
                {
                    o.UnitId,
                    o.ArmyUnit.Name,
                    o.ArmyTurn.CampaignTurn.Number,
                    o.Q,
                    o.R,
                })
                .ToListAsync(cancellationToken)
        ).GroupBy(o => o.UnitId).Select(g => g.MaxBy(o => o.Number)!) // A group has at least one row.
        .Where(o => new Hex(o.Q, o.R) == move.State.At && embarked.CarrierNow(o.UnitId) is null && !claimed.Contains(o.UnitId)).OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ThenBy(o => o.UnitId).Select(o => o.UnitId).ToList();
        return free.Count >= needed
            ? ([.. free.Take(needed)], null)
            : (
                [],
                $"It needs {Boats(needed)} ({move.Points} points, {capacity} a boat); "
                    + $"{(free.Count == 0 ? "none" : free.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))} free here."
            );
    }

    private static string Boats(int count) =>
        count == 1 ? "1 boat" : FormattableString.Invariant($"{count} boats");

    /// <summary>409: a boat tied to a unit goes by that unit's orders, never its own.</summary>
    private static async Task<ProblemHttpResult> TiedAsync(
        WwgDbContext db,
        Guid carrierId,
        CancellationToken cancellationToken
    )
    {
        var name = await db
            .ArmyUnits.AsNoTracking()
            .Where(u => u.Id == carrierId)
            .Select(u => u.Name)
            .SingleAsync(cancellationToken);
        return Conflict(
            "Carrying a unit",
            $"This boat carries {name}: it goes where {name} does, by that unit's orders."
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

    /// <summary>A unit's order to be planned: where it is, and where it's told to go.</summary>
    private sealed record MoveRequest(
        HexGrid Grid,
        Guid CampaignId,
        Guid ArmyId,
        UnitType Type,
        Nation Nation,
        int Turn,
        UnitState State,
        OrderKind Kind,
        IReadOnlyList<Hex> Path,
        bool ByUmpire,
        bool ForceMarch,
        bool LivesOffTheLand,
        int Points,
        IReadOnlyList<Guid>? Aboard
    )
    {
        /// <summary>How it moves: by its boats while it's on them (decision 0022), else by its type.</summary>
        public MovementClass Class => Aboard is null ? Movement.ClassOf(Type) : MovementClass.Boat;
    }

    /// <summary>
    /// Where an order leaves the unit, or why it won't do. The Umpire's moves get where they're
    /// going (decision 0011); a commander's go by the terrain and the campaign's table, and may
    /// stop part of the way into a hex that takes more than a turn.
    /// </summary>
    private static async Task<MovePlan> PlanAsync(
        WwgDbContext db,
        MoveRequest move,
        CancellationToken cancellationToken
    )
    {
        var table = await MovementTable.LoadAsync(db, move.CampaignId, cancellationToken);
        var movement = new MoveCheck(
            move.Grid,
            table,
            await PathTerrain.LoadAsync(
                db,
                move.CampaignId,
                [move.State.At, .. move.Path],
                cancellationToken
            ),
            move.Class
        );
        if (PathProblem(movement, move.State.At, move.Kind, move.Path) is { } problem)
        {
            return MovePlan.Refused(problem);
        }

        if (move.Kind == OrderKind.Disembark)
        {
            return Embarkation.LandingProblem(movement.Terrain, move.State.At, move.Path) is { } why
                ? MovePlan.Refused(why)
                : new MovePlan(move.Path.Count == 0 ? move.State.At : move.Path[0], null, null);
        }

        return move.ByUmpire || move.Kind is not OrderKind.Move
            ? new MovePlan(move.Path.Count == 0 ? move.State.At : move.Path[^1], null, null)
            : Movement.Plan(
                movement.Table,
                movement.Terrain,
                movement.Class,
                move.State.At,
                move.Path,
                move.State,
                await BudgetAsync(db, move, table, cancellationToken)
            );
    }

    private static MoveRequest MoveFor(
        HexGrid grid,
        OrderedUnit unit,
        EditableTurn turn,
        UnitState state,
        GiveOrderRequest request,
        bool byUmpire,
        IReadOnlyList<Guid>? aboard
    ) =>
        new(
            grid,
            unit.CampaignId,
            turn.ArmyId,
            unit.Type,
            unit.Nation,
            turn.Number,
            state,
            request.Kind,
            request.Kind is OrderKind.Move or OrderKind.Disembark ? request.Path ?? [] : [],
            byUmpire,
            request.ForceMarch && request.Kind == OrderKind.Move,
            request.LivesOffTheLand,
            unit.Points,
            aboard
        );

    /// <summary>The Umpire's change to an order, in the turn's history.</summary>
    private static string EditNote(MoveRequest move) =>
        (
            move.Kind switch
            {
                OrderKind.Hold => "Set to hold.",
                OrderKind.Embark => "Set to embark.",
                OrderKind.Disembark => "Set to land.",
                OrderKind.BuildBoat => "Set to build a boat.",
                _ when move.ForceMarch => "Set to force march.",
                _ => "Set to move.",
            }
        ) + (move.LivesOffTheLand ? " Living off the land." : "");

    /// <summary>A unit that can be given an order: what it is, where, and who's on boats.</summary>
    private sealed record Ordered(OrderedUnit Unit, UnitState State, Embarked Embarked);

    /// <summary>
    /// The army's unit to order, or why it can't have one: not the army's (404), not placed yet,
    /// or a boat tied to a unit, which goes by that unit's orders (409).
    /// </summary>
    private static async Task<(Ordered? Ordered, ProblemHttpResult? Problem)> OrderedAsync(
        WwgDbContext db,
        Guid unitId,
        Guid armyId,
        CancellationToken cancellationToken
    )
    {
        var unit = await OrderedUnitAsync(db, unitId, armyId, cancellationToken);
        if (unit is null)
        {
            return (
                null,
                TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    detail: "That unit isn't in this army."
                )
            );
        }

        if (await TurnRules.CurrentStateAsync(db, unitId, cancellationToken) is not { } state)
        {
            return (
                null,
                Conflict("Not placed", "The Umpire hasn't placed this unit on the map yet.")
            );
        }

        var embarked = await Embarkation.LoadAsync(db, unit.CampaignId, cancellationToken);
        return embarked.CarrierNow(unitId) is { } carrier
            ? (null, await TiedAsync(db, carrier, cancellationToken))
            : (new Ordered(unit, state, embarked), null);
    }

    /// <summary>The unit being ordered: its type, campaign, the nation it marches as, and its points.</summary>
    private sealed record OrderedUnit(UnitType Type, Guid CampaignId, Nation Nation, int Points);

    /// <summary>The army's unit, or null if it isn't one of the army's.</summary>
    private static Task<OrderedUnit?> OrderedUnitAsync(
        WwgDbContext db,
        Guid unitId,
        Guid armyId,
        CancellationToken cancellationToken
    ) =>
        db
            .ArmyUnits.AsNoTracking()
            .Where(u => u.Id == unitId && u.ArmyId == armyId)
            .Select(u => new OrderedUnit(
                u.Type,
                u.Army.CampaignId,
                // Its faction's nation (step 45), or its army's if the faction has none (or it
                // has no faction: a boat built in the campaign).
                u.Unit != null
                && u.Unit.Faction.Nation != Nation.None
                    ? u.Unit.Faction.Nation
                    : u.Army.Nation,
                u.Points
            ))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>The unit's movement this turn, by its time of day and the unit's nation (step 45).</summary>
    private static async Task<double> BudgetAsync(
        WwgDbContext db,
        MoveRequest move,
        MovementTable table,
        CancellationToken cancellationToken
    )
    {
        var calendar = await CalendarEndpoints.LoadAsync(db, move.CampaignId, cancellationToken);
        var part = TurnParts.Of(calendar.FirstTurnPart, calendar.StartDate, move.Turn)?.Part;
        var movementClass = move.Class;
        var budget = Movement.BudgetFor(table, movementClass, move.Nation, part, calendar);
        // A force march goes a flat hex's worth further (decision 0018).
        return move.ForceMarch ? budget + (1 / table.Rate(movementClass, Ground.Flat)) : budget;
    }

    /// <summary>
    /// What's wrong with the order besides its path, and on which field, or null: a force march
    /// it can't make, or living off the land when its nation may not (decision 0019).
    /// </summary>
    private static async Task<(string Field, string Message)?> OrderProblemAsync(
        WwgDbContext db,
        MoveRequest move,
        CancellationToken cancellationToken
    )
    {
        if (BoatProblem(move) is { } boats)
        {
            return boats;
        }

        if (
            move.Kind == OrderKind.BuildBoat
            && await BoatBuilding.SiteProblemAsync(
                db,
                move.CampaignId,
                move.ArmyId,
                move.State.At,
                cancellationToken
            )
                is { } site
        )
        {
            return ("kind", site);
        }

        if (move.ForceMarch && await ForceMarchProblemAsync(db, move, cancellationToken) is { } why)
        {
            return ("forceMarch", why);
        }

        if (!move.LivesOffTheLand)
        {
            return null;
        }

        var campaign = await CalendarEndpoints.LoadAsync(db, move.CampaignId, cancellationToken);
        var nations = SupplySettingsEndpoints.OffTheLandNations(campaign);
        return nations.Contains(move.Nation)
            ? null
            : (
                "livesOffTheLand",
                nations.Count == 0
                    ? "No one lives off the land in this campaign."
                    : $"Only these nations' units can live off the land here: {string.Join(", ", nations)}."
            );
    }

    /// <summary>
    /// What's wrong with an order about boats (decision 0022), and on which field, or null:
    /// embarking twice, or as a boat or supply train; landing when not aboard; force marching or
    /// living off the land on the water.
    /// </summary>
    private static (string Field, string Message)? BoatProblem(MoveRequest move) =>
        move switch
        {
            { Kind: OrderKind.Embark, Aboard: not null } => ("kind", "It's on its boats already."),
            { Kind: OrderKind.Embark } when !BoatRules.CanEmbark(move.Type) => (
                "kind",
                "Boats and supply trains don't board boats."
            ),
            { Kind: OrderKind.Disembark, Aboard: null } => ("kind", "It isn't on boats."),
            { Kind: OrderKind.BuildBoat, Aboard: not null } => (
                "kind",
                "Land first: a unit on boats can't build them."
            ),
            { Kind: OrderKind.BuildBoat, Type: UnitType.Boat } => (
                "kind",
                "Boats don't build boats."
            ),
            { ForceMarch: true, Aboard: not null } => (
                "forceMarch",
                "No forced marches on boats: being carried is rest."
            ),
            { LivesOffTheLand: true, Kind: OrderKind.Embark }
            or { LivesOffTheLand: true, Aboard: not null, Kind: not OrderKind.Disembark } => (
                "livesOffTheLand",
                "A unit on boats can't live off the land."
            ),
            _ => null,
        };

    /// <summary>
    /// Why the unit can't force march this turn, or null if it can: only by day (by night, moving
    /// counts towards a forced march anyway), and only a class that moves over flat ground.
    /// </summary>
    private static async Task<string?> ForceMarchProblemAsync(
        WwgDbContext db,
        MoveRequest unit,
        CancellationToken cancellationToken
    )
    {
        var calendar = await CalendarEndpoints.LoadAsync(db, unit.CampaignId, cancellationToken);
        var part = TurnParts.Of(calendar.FirstTurnPart, calendar.StartDate, unit.Turn)?.Part;
        if (part == TurnPart.Night)
        {
            return "Force march by day only: by night, moving counts towards a forced march anyway.";
        }

        var table = await MovementTable.LoadAsync(db, unit.CampaignId, cancellationToken);
        return table.Rate(unit.Class, Ground.Flat) > 0
            ? null
            : "This unit can't force march: it doesn't move over land.";
    }

    /// <summary>What a move is checked against: the grid, the campaign's table and the terrain.</summary>
    private sealed record MoveCheck(
        HexGrid Grid,
        MovementTable Table,
        PathTerrain Terrain,
        MovementClass Class
    );

    /// <summary>
    /// Why a Move's path won't do, or null if it will: it needs a step, and each step is next to
    /// the last (the first to the unit's hex) and inside the grid. (What it costs is the move's
    /// plan.) A Hold has no path, nor embarking; a landing may have one step (the plan checks it).
    /// </summary>
    private static string? PathProblem(
        MoveCheck movement,
        Hex from,
        OrderKind kind,
        IReadOnlyList<Hex> path
    )
    {
        if (kind is OrderKind.Hold or OrderKind.Embark or OrderKind.BuildBoat)
        {
            return null;
        }

        if (path.Count == 0 && kind == OrderKind.Move)
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

            if (!movement.Grid.Contains(step))
            {
                return "That's outside the campaign's area.";
            }

            at = step;
        }

        return null;
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
