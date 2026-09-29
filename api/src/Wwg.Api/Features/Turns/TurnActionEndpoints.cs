using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;
using Wwg.Api.Infrastructure.Email;

namespace Wwg.Api.Features.Turns;

/// <summary>
/// Moving a turn along (DESIGN.md §5.1): a commander submits their army's turn; the Umpire
/// approves it, sends it back or reverts it. Each action emails the other side.
/// </summary>
internal static class TurnActionEndpoints
{
    public static IEndpointRouteBuilder MapTurnActionEndpoints(this IEndpointRouteBuilder app)
    {
        var turn = app.MapGroup("/api/army-turns/{id:guid}").WithTags("Turns");
        turn.MapPost("/submit", SubmitTurnAsync)
            .WithName("SubmitTurn")
            .RequireCampaignAccess(CampaignAccess.OwnCommander, CampaignRouteId.ArmyTurn)
            .ProducesProblem(StatusCodes.Status409Conflict);
        turn.MapPost("/approve", ApproveTurnAsync)
            .WithName("ApproveTurn")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.ArmyTurn)
            .ProducesProblem(StatusCodes.Status409Conflict);
        turn.MapPost("/send-back", SendBackTurnAsync)
            .WithName("SendBackTurn")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.ArmyTurn)
            .ProducesProblem(StatusCodes.Status409Conflict);
        turn.MapPost("/revert", RevertTurnAsync)
            .WithName("RevertTurn")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.ArmyTurn)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>
    /// Submits the army's turn for the Umpire to approve (the army's commander): a Draft in the
    /// open turn, once every unit on the map has an order. Nothing changes while it's Submitted.
    /// </summary>
    internal static async Task<Results<NoContent, ProblemHttpResult>> SubmitTurnAsync(
        Guid id,
        WwgDbContext db,
        TimeProvider time,
        IEmailQueue emails,
        IOptions<AppOptions> appOptions,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var turn = await TurnAsync(db, id, cancellationToken);
        var without = await db
            .Units.AsNoTracking()
            .Where(u =>
                u.ArmyId == turn.ArmyId
                && db.UnitOrders.Any(o =>
                    o.UnitId == u.Id && o.ArmyTurn.Status == ArmyTurnStatus.Completed
                )
                && !db.UnitOrders.Any(o => o.UnitId == u.Id && o.ArmyTurnId == id)
            )
            .OrderBy(u => u.Name)
            .Select(u => u.Name)
            .ToListAsync(cancellationToken);
        if (turn.Status == ArmyTurnStatus.Draft && without.Count > 0)
        {
            return Conflict(
                "Orders missing",
                $"Give every unit an order first. Without one: {string.Join(", ", without)}."
            );
        }

        var action = new TurnAction(ArmyTurnEventKind.Submitted, ArmyTurnStatus.Draft);
        if (!await ActAsync(db, time, httpContext, turn, action, cancellationToken))
        {
            return NotNow(turn, "submitted", "a Draft");
        }

        var by = await NameAsync(db, httpContext, cancellationToken);
        var map = MapLink(appOptions, turn.CampaignId);
        foreach (var umpire in await UmpiresAsync(db, turn.CampaignId, cancellationToken))
        {
            await emails.QueueAsync(
                TurnEmails.Create(
                    umpire,
                    $"{turn.Campaign}: {turn.Army} submitted turn {turn.Number}",
                    $"{by} submitted {turn.Army}'s orders for turn {turn.Number} of {turn.Campaign}.",
                    "Review them on the map",
                    map
                ),
                cancellationToken
            );
        }

        return TypedResults.NoContent();
    }

    /// <summary>Approves a Submitted turn in the open turn (Umpire or Admin): it's Completed.</summary>
    internal static async Task<Results<NoContent, ProblemHttpResult>> ApproveTurnAsync(
        Guid id,
        WwgDbContext db,
        TimeProvider time,
        IEmailQueue emails,
        IOptions<AppOptions> appOptions,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var turn = await TurnAsync(db, id, cancellationToken);
        var action = new TurnAction(ArmyTurnEventKind.Approved, ArmyTurnStatus.Submitted);
        if (!await ActAsync(db, time, httpContext, turn, action, cancellationToken))
        {
            return NotNow(turn, "approved", "Submitted");
        }

        var by = await NameAsync(db, httpContext, cancellationToken);
        await EmailCommanderAsync(
            db,
            emails,
            turn,
            $"{turn.Campaign}: turn {turn.Number} approved for {turn.Army}",
            $"{by} approved {turn.Army}'s orders for turn {turn.Number} of {turn.Campaign}.",
            "See the map",
            MapLink(appOptions, turn.CampaignId),
            null,
            cancellationToken
        );
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Sends a Submitted turn in the open turn back to its commander as a Draft (Umpire or
    /// Admin), with a note on the turn and on units if wanted.
    /// </summary>
    internal static Task<
        Results<NoContent, ValidationProblem, ProblemHttpResult>
    > SendBackTurnAsync(
        Guid id,
        ReviewTurnRequest request,
        WwgDbContext db,
        TimeProvider time,
        IEmailQueue emails,
        IOptions<AppOptions> appOptions,
        HttpContext httpContext,
        CancellationToken cancellationToken
    ) =>
        ReviewAsync(
            new Review(id, request, ArmyTurnEventKind.SentBack, ArmyTurnStatus.Submitted),
            db,
            time,
            emails,
            appOptions,
            httpContext,
            cancellationToken
        );

    /// <summary>
    /// Reverts a Completed turn in the open turn to a Draft (Umpire or Admin), with a note on the
    /// turn and on units if wanted. Turns in closed campaign turns can't be reverted.
    /// </summary>
    internal static Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> RevertTurnAsync(
        Guid id,
        ReviewTurnRequest request,
        WwgDbContext db,
        TimeProvider time,
        IEmailQueue emails,
        IOptions<AppOptions> appOptions,
        HttpContext httpContext,
        CancellationToken cancellationToken
    ) =>
        ReviewAsync(
            new Review(id, request, ArmyTurnEventKind.Reverted, ArmyTurnStatus.Completed),
            db,
            time,
            emails,
            appOptions,
            httpContext,
            cancellationToken
        );

    /// <summary>An army turn, and what the emails about it say.</summary>
    private sealed record TurnInfo(
        Guid Id,
        Guid CampaignId,
        string Campaign,
        Guid ArmyId,
        string Army,
        int Number,
        bool Open,
        ArmyTurnStatus Status
    );

    private static Task<TurnInfo> TurnAsync(
        WwgDbContext db,
        Guid id,
        CancellationToken cancellationToken
    ) =>
        db
            .ArmyTurns.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TurnInfo(
                t.Id,
                t.Army.CampaignId,
                t.Army.Campaign.Name,
                t.ArmyId,
                t.Army.Name,
                t.CampaignTurn.Number,
                t.CampaignTurn.ClosedAt == null,
                t.Status
            ))
            .SingleOrGoneAsync(cancellationToken);

    /// <summary>What an action records, and the status it moves the turn from.</summary>
    private sealed record TurnAction(
        ArmyTurnEventKind Kind,
        ArmyTurnStatus From,
        string? Note = null,
        IReadOnlyList<UnitNoteDto>? UnitNotes = null
    );

    /// <summary>
    /// Moves the turn on and records it in its history, if it's still in the status the action
    /// needs, in the open campaign turn (after setup). False if not: it wasn't, or a request racing
    /// this one moved it first.
    /// </summary>
    private static async Task<bool> ActAsync(
        WwgDbContext db,
        TimeProvider time,
        HttpContext httpContext,
        TurnInfo turn,
        TurnAction action,
        CancellationToken cancellationToken
    )
    {
        var now = time.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var moved = await db
            .ArmyTurns.Where(t =>
                t.Id == turn.Id
                && t.Status == action.From
                && t.CampaignTurn.ClosedAt == null
                && t.CampaignTurn.Number > 0
            )
            .ExecuteUpdateAsync(
                set =>
                {
                    set.SetProperty(t => t.UpdatedAt, now);
                    _ = action.Kind switch
                    {
                        ArmyTurnEventKind.Submitted => set.SetProperty(
                                t => t.Status,
                                ArmyTurnStatus.Submitted
                            )
                            .SetProperty(t => t.SubmittedAt, now),
                        ArmyTurnEventKind.Approved => set.SetProperty(
                                t => t.Status,
                                ArmyTurnStatus.Completed
                            )
                            .SetProperty(t => t.CompletedAt, now),
                        _ => set.SetProperty(t => t.Status, ArmyTurnStatus.Draft)
                            .SetProperty(t => t.SubmittedAt, (DateTime?)null)
                            .SetProperty(t => t.CompletedAt, (DateTime?)null),
                    };
                },
                cancellationToken
            );
        if (moved == 0)
        {
            return false;
        }

        var historyEvent = new ArmyTurnEvent
        {
            ArmyTurnId = turn.Id,
            Kind = action.Kind,
            At = now,
            ByUserId = httpContext.User.GetUserId(),
            Note = action.Note,
        };
        historyEvent.UnitNotes.AddRange(
            (action.UnitNotes ?? []).Select(n => new UnitNote { UnitId = n.UnitId, Text = n.Text })
        );
        db.ArmyTurnEvents.Add(historyEvent);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>A send-back or revert: the turn, why, and the status it moves the turn from.</summary>
    private sealed record Review(
        Guid Id,
        ReviewTurnRequest Request,
        ArmyTurnEventKind Kind,
        ArmyTurnStatus From
    );

    /// <summary>Sends a turn back to Draft with the Umpire's notes, and emails its commander.</summary>
    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> ReviewAsync(
        Review review,
        WwgDbContext db,
        TimeProvider time,
        IEmailQueue emails,
        IOptions<AppOptions> appOptions,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var turn = await TurnAsync(db, review.Id, cancellationToken);
        var unitNotes = review.Request.UnitNotes ?? [];
        var names = await db
            .Units.AsNoTracking()
            .Where(u => u.ArmyId == turn.ArmyId)
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);
        if (
            unitNotes.Any(n => !names.ContainsKey(n.UnitId))
            || unitNotes.DistinctBy(n => n.UnitId).Count() < unitNotes.Count
        )
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["unitNotes"] = ["Each note must be on a different unit in this army."],
                }
            );
        }

        var note = string.IsNullOrEmpty(review.Request.Note) ? null : review.Request.Note;
        var action = new TurnAction(review.Kind, review.From, note, unitNotes);
        var sentBack = review.Kind == ArmyTurnEventKind.SentBack;
        if (!await ActAsync(db, time, httpContext, turn, action, cancellationToken))
        {
            return NotNow(turn, sentBack ? "sent back" : "reverted", review.From.ToString());
        }

        var by = await NameAsync(db, httpContext, cancellationToken);
        await EmailCommanderAsync(
            db,
            emails,
            turn,
            $"{turn.Campaign}: turn {turn.Number} {(sentBack ? "sent back" : "reopened")} for {turn.Army}",
            $"{by} {(sentBack ? "sent back" : "reopened")} {turn.Army}'s orders for turn {turn.Number} of {turn.Campaign}, to change and submit again.",
            "Change them on the map",
            MapLink(appOptions, turn.CampaignId),
            (note, [.. unitNotes.Select(n => (names[n.UnitId], n.Text))]),
            cancellationToken
        );
        return TypedResults.NoContent();
    }

    private static async Task EmailCommanderAsync(
        WwgDbContext db,
        IEmailQueue emails,
        TurnInfo turn,
        string subject,
        string what,
        string action,
        Uri map,
        (string? Note, List<(string, string)> UnitNotes)? notes,
        CancellationToken cancellationToken
    )
    {
        var commander = await db
            .CampaignMembers.AsNoTracking()
            .Where(m => db.Armies.Any(a => a.Id == turn.ArmyId && a.CommanderId == m.Id))
            .Select(m => new TurnRecipient(m.User.Email ?? "", m.User.FirstName, m.User.LastName))
            .SingleOrDefaultAsync(cancellationToken);
        if (commander is not null)
        {
            await emails.QueueAsync(
                TurnEmails.Create(
                    commander,
                    subject,
                    what,
                    action,
                    map,
                    notes?.Note,
                    notes?.UnitNotes
                ),
                cancellationToken
            );
        }
    }

    private static Task<List<TurnRecipient>> UmpiresAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    ) =>
        db
            .CampaignMembers.AsNoTracking()
            .Where(m => m.CampaignId == campaignId && m.Role == CampaignRole.Umpire)
            .Select(m => new TurnRecipient(m.User.Email ?? "", m.User.FirstName, m.User.LastName))
            .ToListAsync(cancellationToken);

    /// <summary>The caller's name, for the emails.</summary>
    private static Task<string> NameAsync(
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var userId = httpContext.User.GetUserId();
        return db
            .Users.Where(u => u.Id == userId)
            .Select(u => u.FirstName + " " + u.LastName)
            .SingleOrGoneAsync(cancellationToken);
    }

    private static Uri MapLink(IOptions<AppOptions> appOptions, Guid campaignId) =>
        new(
            appOptions.Value.PublicUrl!, // Required and validated at startup.
            $"/campaigns/{campaignId}/map"
        );

    /// <summary>409: the turn isn't where the action needs it (or the campaign turn has closed).</summary>
    private static ProblemHttpResult NotNow(TurnInfo turn, string done, string needs) =>
        Conflict(
            "Not now",
            turn.Open
                ? $"Only a turn that's {needs} can be {done}."
                : $"Turn {turn.Number} has closed: its turns can't be {done}."
        );

    private static ProblemHttpResult Conflict(string title, string detail) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: title,
            detail: detail
        );
}
