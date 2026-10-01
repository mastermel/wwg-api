using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Turns;

/// <summary>
/// Attrition (the rules, §F; decision 0018): points lost to forced marches, by Fighting Factor,
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

    /// <summary>
    /// The attrition the open turn's orders cost each unit that owes any, by army then name: what
    /// starting the next turn needs the Umpire to confirm.
    /// </summary>
    public static async Task<List<AttritionDueResponse>> DueAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var (before, open) = await Marches.LoadAsync(db, campaignId, null, cancellationToken);
        var owing = open
            .Values.Select(order =>
                (order.UnitId, State: before[order.UnitId].After(order.Moved, order.ForceMarch))
            )
            .Where(o => o.State.Multiplier > 0)
            .ToDictionary(o => o.UnitId, o => o.State);
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
                var state = owing[u.Id];
                var (loss, _) = Owed(
                    u.FightingFactor,
                    u.Points,
                    state.Multiplier,
                    u.AttritionCarry
                );
                return new AttritionDueResponse(
                    u.Id,
                    u.ArmyId,
                    u.Name,
                    u.FightingFactor,
                    u.Points,
                    state.ForcedMarchTurns,
                    state.Multiplier,
                    loss
                );
            }),
        ];
    }

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
                        Note =
                            owed.Multiplier == 1
                                ? "Forced march"
                                : $"Forced march, ×{owed.Multiplier}",
                        ByUserId = byUserId,
                    }
                );
            }
        }

        return null;
    }
}
