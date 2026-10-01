using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Supply;

namespace Wwg.Api.Features.Turns;

/// <summary>
/// Attrition (the rules, §F; decisions 0018 and 0019): points lost to forced marches and to
/// being out of supply, by Fighting Factor,
/// for about two battalions (50 points), more for larger units and less for smaller.
/// </summary>
internal static class Attrition
{
    /// <summary>The points two battalions' scale is for.</summary>
    private const double ScalePoints = 50;

    // Sums of fractions don't always come to whole numbers exactly.
    private const double Tolerance = 1e-9;

    /// <summary>The rules' scale: 3 points for FF 1–2, 2 for 3–4, 1 for 5–6, ½ for 7 and above.</summary>
    public static double Scale(int fightingFactor) =>
        fightingFactor switch
        {
            <= 2 => 3,
            <= 4 => 2,
            <= 6 => 1,
            _ => 0.5,
        };

    /// <summary>
    /// What a unit owes: the fraction it carries plus this turn's (the scale × the multiplier ×
    /// its points ÷ 50); whole points of it (no more than it has) are lost, the rest carried on.
    /// </summary>
    public static (int Loss, double Carry) Owed(
        int fightingFactor,
        int points,
        int multiplier,
        double carry
    )
    {
        var owed = carry + (Scale(fightingFactor) * multiplier * points / ScalePoints);
        var whole = (int)Math.Floor(owed + Tolerance);
        return (Math.Min(whole, points), Math.Max(0, owed - whole));
    }

    /// <summary>What a unit owes this turn, and why.</summary>
    private sealed record Owing(
        int ForcedMarchTurns,
        int ForcedMarchMultiplier,
        int UnsuppliedTurns,
        int SupplyMultiplier
    );

    /// <summary>
    /// The attrition the open turn's orders cost each unit that owes any, by army then name: what
    /// starting the next turn needs the Umpire to confirm. Forced marches (decision 0018), doubled
    /// out of supply; and from a unit's 7th turn in a row out of supply, normal attrition besides
    /// (decision 0019).
    /// </summary>
    public static async Task<List<AttritionDueResponse>> DueAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var (before, open) = await Marches.LoadAsync(db, campaignId, null, cancellationToken);
        var supply = await SupplyData.LoadAsync(db, campaignId, cancellationToken);
        var owing = new Dictionary<Guid, Owing>();
        foreach (var (id, unit) in supply.Units)
        {
            // Without an order it rests: no forced march.
            var march = open.TryGetValue(id, out var order)
                ? before[id].After(order.Moved, order.ForceMarch)
                : MarchState.Rested;
            var unsupplied = supply.Next.GetValueOrDefault(id)?.State == SupplyState.Unsupplied;
            var turnsOut = unsupplied ? unit.UnsuppliedTurns + 1 : 0;
            var forced = march.Multiplier * (unsupplied ? 2 : 1);
            var cutOff = turnsOut > SupplyLines.GraceTurns ? 1 : 0;
            if (forced + cutOff > 0)
            {
                owing[id] = new(march.ForcedMarchTurns, forced, turnsOut, cutOff);
            }
        }

        var ids = owing.Keys.ToList();
        var units = await db
            .ArmyUnits.AsNoTracking()
            // A unit with no points left has none to lose.
            .Where(u => ids.Contains(u.Id) && u.Points > 0)
            .OrderBy(u => u.Army.Name)
            .ThenBy(u => u.Name)
            .ThenBy(u => u.Id)
            .Select(u => new
            {
                u.Id,
                u.ArmyId,
                u.Name,
                u.FightingFactor,
                u.Points,
                u.AttritionCarry,
            })
            .ToListAsync(cancellationToken);
        return
        [
            .. units.Select(u =>
            {
                var owed = owing[u.Id];
                var multiplier = owed.ForcedMarchMultiplier + owed.SupplyMultiplier;
                var (loss, _) = Owed(u.FightingFactor, u.Points, multiplier, u.AttritionCarry);
                return new AttritionDueResponse(
                    u.Id,
                    u.ArmyId,
                    u.Name,
                    u.FightingFactor,
                    u.Points,
                    owed.ForcedMarchTurns,
                    owed.ForcedMarchMultiplier,
                    owed.UnsuppliedTurns,
                    multiplier,
                    loss
                );
            }),
        ];
    }

    /// <summary>Why a unit loses points, for its history: "Forced march, ×2; out of supply, turn 7".</summary>
    private static string NoteOf(AttritionDueResponse owed) =>
        string.Join(
            "; ",
            new[]
            {
                owed.ForcedMarchMultiplier switch
                {
                    0 => null,
                    1 => "Forced march",
                    var times => $"Forced march, ×{times}",
                },
                owed.UnsuppliedTurns > SupplyLines.GraceTurns
                    ? $"out of supply, turn {owed.UnsuppliedTurns}"
                    : null,
            }.Where(part => part is not null)
        )
            is var note
        && note.Length > 0
            ? char.ToUpperInvariant(note[0]) + note[1..]
            : "Attrition";

    /// <summary>
    /// Applies the losses the Umpire confirmed for the closing turn (not saved), or says why they
    /// won't do: one for each unit that owes attrition, and no other, each no more than it has.
    /// Each unit carries what's left of a point it owed, whatever the Umpire made of the loss.
    /// </summary>
    public static async Task<string?> ApplyAsync(
        WwgDbContext db,
        Guid campaignId,
        int turn,
        IReadOnlyList<AttritionLossRequest> confirmed,
        Guid? byUserId,
        CancellationToken cancellationToken
    )
    {
        var due = await DueAsync(db, campaignId, cancellationToken);
        var given = confirmed.GroupBy(l => l.UnitId).ToDictionary(g => g.Key, g => g.Last().Points);
        var missing = due.Where(d => !given.ContainsKey(d.UnitId)).Select(d => d.Name).ToList();
        if (missing.Count > 0)
        {
            return $"Confirm the attrition for {string.Join(", ", missing)}.";
        }
        if (given.Keys.Any(id => due.All(d => d.UnitId != id)))
        {
            return "Only units that owe attrition this turn can lose points to it.";
        }
        if (due.Find(d => given[d.UnitId] > d.Points) is { } tooMuch)
        {
            return $"{tooMuch.Name} has only {tooMuch.Points} points to lose.";
        }

        var ids = due.Select(d => d.UnitId).ToList();
        var units = await db
            .ArmyUnits.Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);
        foreach (var owed in due)
        {
            var unit = units[owed.UnitId];
            var (_, carry) = Owed(
                unit.FightingFactor,
                unit.Points,
                owed.Multiplier,
                unit.AttritionCarry
            );
            var loss = given[owed.UnitId];
            unit.AttritionCarry = carry;
            unit.Points -= loss;
            if (loss > 0)
            {
                db.PointsChanges.Add(
                    new PointsChange
                    {
                        ArmyUnitId = unit.Id,
                        Turn = turn,
                        Change = -loss,
                        PointsAfter = unit.Points,
                        Reason = PointsChangeReason.Attrition,
                        Note = NoteOf(owed),
                        ByUserId = byUserId,
                    }
                );
            }
        }

        return null;
    }
}
