using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure.Email;

namespace Wwg.Api.Features.Turns;

/// <summary>Someone a campaign email goes to, and the kinds they've turned off (decision 0023).</summary>
internal sealed record TurnRecipient(
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyCollection<EmailKind> Muted
)
{
    /// <summary>Whether they get emails of this kind.</summary>
    public bool Wants(EmailKind kind) => !Muted.Contains(kind);
}

/// <summary>
/// The emails every turn action sends to the other side (DESIGN.md §3.13): the Umpire when a
/// commander submits; the commander when the Umpire submits for them, approves, sends back or
/// reverts (listing the orders the Umpire set); every commander when a turn starts.
/// </summary>
internal static class TurnEmails
{
    /// <param name="to">Who it's for.</param>
    /// <param name="subject">The subject line.</param>
    /// <param name="what">What happened, as a sentence.</param>
    /// <param name="action">The link's text: what to do next.</param>
    /// <param name="map">The campaign's map.</param>
    /// <param name="note">The Umpire's note on the turn, if any.</param>
    /// <param name="unitNotes">The Umpire's notes on units, by unit name.</param>
    /// <param name="umpireOrders">The orders the Umpire set for the commander ("Name: move").</param>
    /// <param name="waiting">What's waiting for the Umpire as they start the next turn.</param>
    public static EmailMessage Create(
        TurnRecipient to,
        string subject,
        string what,
        string action,
        Uri map,
        string? note = null,
        IReadOnlyList<(string ArmyUnit, string Text)>? unitNotes = null,
        IReadOnlyList<string>? umpireOrders = null,
        IReadOnlyList<string>? waiting = null
    )
    {
        var html = new StringBuilder();
        var text = new StringBuilder();
        html.Append(
            CultureInfo.InvariantCulture,
            $"<p>Hi {Encode(to.FirstName)},</p>\n<p>{Encode(what)}</p>\n"
        );
        text.Append(CultureInfo.InvariantCulture, $"Hi {to.FirstName},\n\n{what}\n\n");
        if (note is not null)
        {
            html.Append(CultureInfo.InvariantCulture, $"<p>Their note: {Encode(note)}</p>\n");
            text.Append(CultureInfo.InvariantCulture, $"Their note: {note}\n\n");
        }

        if (unitNotes is { Count: > 0 })
        {
            html.Append("<ul>\n");
            foreach (var (unit, unitNote) in unitNotes)
            {
                html.Append(
                    CultureInfo.InvariantCulture,
                    $"<li><strong>{Encode(unit)}:</strong> {Encode(unitNote)}</li>\n"
                );
                text.Append(CultureInfo.InvariantCulture, $"- {unit}: {unitNote}\n");
            }

            html.Append("</ul>\n");
            text.Append('\n');
        }

        if (umpireOrders is { Count: > 0 })
        {
            AppendList(html, text, "Orders the Umpire set:", umpireOrders);
        }

        if (waiting is not null)
        {
            AppendWaiting(html, text, waiting);
        }

        html.Append(
            CultureInfo.InvariantCulture,
            $"<p><a href=\"{Encode(map.ToString())}\">{Encode(action)}</a></p>\n"
        );
        text.Append(CultureInfo.InvariantCulture, $"{action}: {map}\n");

        return new EmailMessage(
            ToAddress: to.Email,
            ToName: $"{to.FirstName} {to.LastName}",
            Subject: subject,
            HtmlBody: html.ToString(),
            TextBody: text.ToString()
        );
    }

    /// <summary>What's waiting for the Umpire, or that nothing is.</summary>
    private static void AppendWaiting(
        StringBuilder html,
        StringBuilder text,
        IReadOnlyList<string> waiting
    )
    {
        if (waiting.Count == 0)
        {
            html.Append("<p>Nothing else is waiting for you as you start the next turn.</p>\n");
            text.Append("Nothing else is waiting for you as you start the next turn.\n\n");
            return;
        }

        AppendList(html, text, "Waiting for you as you start the next turn:", waiting);
    }

    private static void AppendList(
        StringBuilder html,
        StringBuilder text,
        string heading,
        IReadOnlyList<string> items
    )
    {
        html.Append(CultureInfo.InvariantCulture, $"<p>{Encode(heading)}</p>\n<ul>\n");
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
