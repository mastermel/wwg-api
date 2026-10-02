using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Infrastructure.Email;

namespace Wwg.Api.Features.Turns;

/// <summary>
/// The new turn's email (step 52c, decision 0023): to each army's commander, one per army, the
/// turn's number, its day and time of day, and what's new for the army since the last turn.
/// </summary>
internal static class TurnStartedEmails
{
    /// <summary>Queues the email for every commanded army, as turn <paramref name="number"/> opens.</summary>
    public static async Task QueueAsync(
        WwgDbContext db,
        IEmailQueue emails,
        Uri map,
        Guid campaignId,
        int number,
        CancellationToken cancellationToken
    )
    {
        var commanders = await db
            .Armies.AsNoTracking()
            .Where(a => a.CampaignId == campaignId && a.CommanderId != null)
            .OrderBy(a => a.Name)
            .Select(a => new
            {
                a.Id,
                Army = a.Name,
                Campaign = a.Campaign.Name,
                To = db
                    .CampaignMembers.Where(m => m.Id == a.CommanderId)
                    .Select(m => new TurnRecipient(
                        m.User.Email ?? "",
                        m.User.FirstName,
                        m.User.LastName,
                        m.User.MutedEmails
                    ))
                    .Single(),
            })
            .ToListAsync(cancellationToken);
        commanders = [.. commanders.Where(c => c.To.Wants(EmailKind.TurnStarted))];
        if (commanders.Count == 0)
        {
            return;
        }

        var calendar = await CalendarEndpoints.LoadAsync(db, campaignId, cancellationToken);
        var when = When(TurnParts.Of(calendar.FirstTurnPart, calendar.StartDate, number));
        // The first turn has nothing before it to report.
        var news =
            number > 1
                ? await TurnNewsBuilder.ForAsync(db, campaignId, number, cancellationToken)
                : [];
        foreach (var commander in commanders)
        {
            await emails.QueueAsync(
                Create(
                    commander.To,
                    commander.Campaign,
                    commander.Army,
                    number,
                    when,
                    news.GetValueOrDefault(commander.Id) ?? TurnNews.None,
                    map
                ),
                cancellationToken
            );
        }
    }

    /// <summary>"17 June 1815, Afternoon", or just "Afternoon" without a start date.</summary>
    private static string? When((TurnPart Part, DateOnly? Date)? turn) =>
        turn switch
        {
            null => null,
            { Date: { } date } => string.Create(
                CultureInfo.InvariantCulture,
                $"{date.Day} {date.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}, {turn.Value.Part}"
            ),
            _ => turn.Value.Part.ToString(),
        };

    public static EmailMessage Create(
        TurnRecipient to,
        string campaign,
        string army,
        int number,
        string? when,
        TurnNews news,
        Uri map
    )
    {
        var at = when is null ? "" : $" ({when})";
        var opening =
            number == 1
                ? $"{campaign} has begun: turn 1{at} is open. Give {army} its first orders."
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"Turn {number} of {campaign} has started{at}. Give {army} its orders."
                );
        var html = new StringBuilder();
        var text = new StringBuilder();
        html.Append(
            CultureInfo.InvariantCulture,
            $"<p>Hi {Encode(to.FirstName)},</p>\n<p>{Encode(opening)}</p>\n"
        );
        text.Append(CultureInfo.InvariantCulture, $"Hi {to.FirstName},\n\n{opening}\n\n");
        if (number > 1)
        {
            AppendNews(html, text, news);
        }

        html.Append(
            CultureInfo.InvariantCulture,
            $"<p><a href=\"{Encode(map.ToString())}\">Give orders on the map</a></p>\n"
        );
        text.Append(CultureInfo.InvariantCulture, $"Give orders on the map: {map}\n");
        return new EmailMessage(
            ToAddress: to.Email,
            ToName: $"{to.FirstName} {to.LastName}",
            Subject: when is null
                ? $"{campaign}: turn {number} has started"
                : $"{campaign}: turn {number} has started ({when})",
            HtmlBody: html.ToString(),
            TextBody: text.ToString()
        );
    }

    private static void AppendNews(StringBuilder html, StringBuilder text, TurnNews news)
    {
        html.Append("<h3>New since the last turn</h3>\n");
        text.Append("New since the last turn\n\n");
        if (news.IsEmpty)
        {
            html.Append("<p>Nothing new since the last turn.</p>\n");
            text.Append("Nothing new since the last turn.\n\n");
            return;
        }

        AppendList(html, text, "Enemy sightings", news.Sightings);
        AppendList(html, text, "Supply", news.Supply);
        AppendList(html, text, "Attrition", news.Attrition);
        AppendList(html, text, "Boats", news.Boats);
        if (news.Reports.Count == 0)
        {
            return;
        }

        html.Append("<h4>Reports from allies</h4>\n");
        text.Append("Reports from allies\n");
        foreach (var report in news.Reports)
        {
            var with = With(report);
            html.Append(
                CultureInfo.InvariantCulture,
                $"<p><strong>From {Encode(report.From)}</strong>{Encode(with)}</p>\n"
            );
            text.Append(CultureInfo.InvariantCulture, $"From {report.From}{with}\n");
            if (report.Message is { Length: > 0 } message)
            {
                html.Append(
                    CultureInfo.InvariantCulture,
                    $"<blockquote>{Encode(message).Replace("\n", "<br>\n", StringComparison.Ordinal)}</blockquote>\n"
                );
                text.Append(
                    CultureInfo.InvariantCulture,
                    $"> {message.Replace("\n", "\n> ", StringComparison.Ordinal)}\n"
                );
            }
            text.Append('\n');
        }
    }

    /// <summary>What a report brings besides its message: " (3 sightings, their units' positions)".</summary>
    private static string With(ReportNews report)
    {
        var parts = new List<string>();
        if (report.Sightings > 0)
        {
            parts.Add(
                report.Sightings == 1
                    ? "1 sighting"
                    : string.Create(CultureInfo.InvariantCulture, $"{report.Sightings} sightings")
            );
        }
        if (report.Positions)
        {
            parts.Add("their units' positions");
        }

        return parts.Count == 0 ? "" : $" ({string.Join(", ", parts)})";
    }

    private static void AppendList(
        StringBuilder html,
        StringBuilder text,
        string heading,
        IReadOnlyList<string> items
    )
    {
        if (items.Count == 0)
        {
            return;
        }

        html.Append(CultureInfo.InvariantCulture, $"<h4>{Encode(heading)}</h4>\n<ul>\n");
        text.Append(CultureInfo.InvariantCulture, $"{heading}\n");
        foreach (var item in items)
        {
            html.Append(CultureInfo.InvariantCulture, $"<li>{Encode(item)}</li>\n");
            text.Append(CultureInfo.InvariantCulture, $"- {item}\n");
        }

        html.Append("</ul>\n");
        text.Append('\n');
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
