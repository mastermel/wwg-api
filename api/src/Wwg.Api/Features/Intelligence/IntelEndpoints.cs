using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Intelligence;

/// <summary>Intelligence between allies, by courier (step 49c, decision 0020).</summary>
internal static class IntelEndpoints
{
    public static IEndpointRouteBuilder MapIntelEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/armies/{id:guid}/reports", SendReportAsync)
            .WithName("SendReport")
            .WithTags("Intelligence")
            .RequireCampaignAccess(CampaignAccess.Commander, CampaignRouteId.Army)
            .ProducesProblem(StatusCodes.Status409Conflict);
        app.MapGet("/api/campaigns/{id:guid}/reports", ListReportsAsync)
            .WithName("ListReports")
            .WithTags("Intelligence")
            .RequireCampaignAccess(CampaignAccess.Member);
        app.MapGet("/api/campaigns/{id:guid}/couriers", ListCouriersAsync)
            .WithName("ListCouriers")
            .WithTags("Intelligence")
            .RequireCampaignAccess(CampaignAccess.Umpire);
        app.MapPost("/api/reports/{id:guid}/stop", StopCourierAsync)
            .WithName("StopCourier")
            .WithTags("Intelligence")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Report)
            .ProducesProblem(StatusCodes.Status409Conflict);
        return app;
    }

    /// <summary>
    /// Sends an ally a report (the army's commander, or the Umpire on its behalf): any of a
    /// snapshot of the army's units, every sighting it has received so far, and a note. A courier
    /// sets out from the army's unit nearest the ally; within two turns' ride, it arrives as the
    /// next turn starts.
    /// </summary>
    internal static async Task<
        Results<Created<ReportResponse>, ValidationProblem, ProblemHttpResult>
    > SendReportAsync(
        Guid id,
        SendReportRequest request,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var campaignId = httpContext.CampaignContext().CampaignId;
        if (
            await RecipientProblemAsync(db, campaignId, id, request, cancellationToken) is
            { } problem
        )
        {
            return problem;
        }

        var open = await TurnRules.OpenTurnAsync(db, campaignId, cancellationToken);
        var (now, _) = await Whereabouts.LoadAsync(db, campaignId, cancellationToken);
        var grid = await CampaignMaps.GridAsync(db, campaignId, cancellationToken);
        if (
            open is not { Number: > 0 }
            || grid is null
            || Couriers.Ends(now, id, request.ToArmyId) is not var (from, to)
        )
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "No way to send it",
                detail: "A courier needs the campaign under way, and both armies' units on the map."
            );
        }

        var rides = await CourierRides.LoadAsync(
            db,
            grid,
            campaignId,
            [from, to],
            cancellationToken
        );
        var sightings = request.IncludesSightings
            ? await db
                .Sightings.Where(s => s.ObservingArmyId == id)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken)
            : [];
        var report = db
            .IntelReports.Add(
                Couriers.NewReport(
                    id,
                    request,
                    open.Number,
                    now,
                    sightings,
                    from,
                    rides.TurnsTo(from, to)
                )
            )
            .Entity;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created(
            $"/api/reports/{report.Id}",
            ToResponse(grid, report, sender: true)
        );
    }

    /// <summary>
    /// The reports the viewer may see, newest first: those their armies sent (without whether
    /// they've arrived), and those that have reached them; the Umpire's, all.
    /// </summary>
    internal static async Task<Ok<List<ReportResponse>>> ListReportsAsync(
        Guid id,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var context = httpContext.CampaignContext();
        var grid = await CampaignMaps.GridAsync(db, id, cancellationToken);
        var reports = await db
            .IntelReports.AsNoTracking()
            .Where(r =>
                r.FromArmy.CampaignId == id
                && (
                    context.CanManage
                    || r.FromArmy.CommanderId == context.MemberId
                    || (
                        r.ToArmy.CommanderId == context.MemberId
                        && r.Status == CourierStatus.Arrived
                    )
                )
            )
            .Select(r => new { Report = r, Sender = r.FromArmy.CommanderId == context.MemberId })
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(
            reports
                .OrderByDescending(r => r.Report.SentTurn)
                .ThenByDescending(r => r.Report.CreatedAt)
                .Select(r => ToResponse(grid, r.Report, r.Sender && !context.CanManage))
                .ToList()
        );
    }

    /// <summary>
    /// The couriers on their way (Umpire or Admin): where each is, and whether the other side's
    /// units are in its hex, for the Umpire to stop it or let it through.
    /// </summary>
    internal static async Task<Ok<List<CourierResponse>>> ListCouriersAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var grid = await CampaignMaps.GridAsync(db, id, cancellationToken);
        var reports = await db
            .IntelReports.AsNoTracking()
            .Where(r => r.FromArmy.CampaignId == id && r.Status == CourierStatus.EnRoute)
            .Select(r => new { Report = r, r.FromArmy.SideId })
            .ToListAsync(cancellationToken);
        var (now, _) = await Whereabouts.LoadAsync(db, id, cancellationToken);
        return TypedResults.Ok(
            reports
                .Where(_ => grid is not null)
                .Select(r =>
                {
                    var at = new Hex(r.Report.CourierQ, r.Report.CourierR);
                    // There's a grid: filtered just above.
                    var (latitude, longitude) = grid!.Centre(at);
                    return new CourierResponse(
                        r.Report.Id,
                        r.Report.FromArmyId,
                        r.Report.ToArmyId,
                        r.Report.SentTurn,
                        at.Q,
                        at.R,
                        latitude,
                        longitude,
                        r.Report.ArrivesNext,
                        now.Any(p => p.At == at && p.SideId != r.SideId)
                    );
                })
                .ToList()
        );
    }

    /// <summary>Stops a courier on its way (Umpire or Admin): captured or lost, it never arrives.</summary>
    internal static async Task<Results<NoContent, ProblemHttpResult>> StopCourierAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var stopped = await db
            .IntelReports.Where(r => r.Id == id && r.Status == CourierStatus.EnRoute)
            .ExecuteUpdateAsync(
                r => r.SetProperty(x => x.Status, CourierStatus.Stopped),
                cancellationToken
            );
        return stopped > 0
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Not on its way",
                detail: "That courier has already arrived or been stopped."
            );
    }

    /// <summary>Why a report to that army won't do, or null: an ally, with something to send.</summary>
    private static async Task<ValidationProblem?> RecipientProblemAsync(
        WwgDbContext db,
        Guid campaignId,
        Guid armyId,
        SendReportRequest request,
        CancellationToken cancellationToken
    )
    {
        var sides = await db
            .Armies.AsNoTracking()
            .Where(a => a.CampaignId == campaignId && (a.Id == armyId || a.Id == request.ToArmyId))
            .ToDictionaryAsync(a => a.Id, a => a.SideId, cancellationToken);
        if (
            request.ToArmyId == armyId
            || !sides.TryGetValue(request.ToArmyId, out var side)
            || side != sides[armyId]
        )
        {
            return Invalid("toArmyId", "Send it to another army of your side.");
        }

        return
            !request.IncludesSnapshot
            && !request.IncludesSightings
            && string.IsNullOrEmpty(request.Note)
            ? Invalid("note", "Send something: your units, your sightings or a note.")
            : null;
    }

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] }
        );

    private static ReportResponse ToResponse(HexGrid? grid, IntelReport report, bool sender) =>
        new(
            report.Id,
            report.FromArmyId,
            report.ToArmyId,
            report.SentTurn,
            sender ? null : report.ArrivedTurn,
            // A sender isn't told whether it got there.
            sender ? null : report.Status,
            report.Note,
            report.Snapshot is null || grid is null
                ? null
                :
                [
                    .. Couriers
                        .ReadSnapshot(report.Snapshot)
                        .Select(u =>
                        {
                            var (latitude, longitude) = grid.Centre(new Hex(u.Q, u.R));
                            return new SnapshotUnitResponse(
                                u.Name,
                                u.Type,
                                u.Q,
                                u.R,
                                latitude,
                                longitude,
                                u.Points
                            );
                        }),
                ],
            report.SightingIds.Count
        );
}
