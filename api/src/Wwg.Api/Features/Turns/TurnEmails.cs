using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Wwg.Api.Infrastructure.Email;

namespace Wwg.Api.Features.Turns;

/// <summary>Someone a turn email goes to.</summary>
internal sealed record TurnRecipient(string Email, string FirstName, string LastName);

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
    public static EmailMessage Create(
        TurnRecipient to,
        string subject,
        string what,
        string action,
        Uri map,
        string? note = null,
        IReadOnlyList<(string ArmyUnit, string Text)>? unitNotes = null,
        IReadOnlyList<string>? umpireOrders = null
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
            html.Append("<p>Orders the Umpire set:</p>\n<ul>\n");
            text.Append("Orders the Umpire set:\n");
            foreach (var order in umpireOrders)
            {
                html.Append(CultureInfo.InvariantCulture, $"<li>{Encode(order)}</li>\n");
                text.Append(CultureInfo.InvariantCulture, $"- {order}\n");
            }

            html.Append("</ul>\n");
            text.Append('\n');
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

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
