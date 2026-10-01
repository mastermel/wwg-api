using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Victory;

/// <summary>
/// A campaign's victory points as a viewer may see them (decision 0021): every side's total; their
/// own side's settlements and changes; the Umpire's, everything.
/// </summary>
internal sealed class Scoreboard
{
    private sealed record ArmyRow(Guid Id, Guid SideId, Guid? CommanderId);

    private sealed record SideRow(Guid Id, string Name);

    private readonly Dictionary<Hex, Settlement> _settlements;
    private readonly List<ArmyRow> _armies;
    private readonly List<SideRow> _sides;
    private readonly Dictionary<Hex, Guid?> _held;
    private readonly List<HoldingChange> _changes;
    private readonly int _openTurn;
    private readonly HexGrid? _grid;
    private readonly bool _everything;
    private readonly HashSet<Guid> _mySides;

    private Scoreboard(
        Dictionary<Hex, Settlement> settlements,
        List<ArmyRow> armies,
        List<SideRow> sides,
        Dictionary<Hex, Guid?> held,
        List<HoldingChange> changes,
        int openTurn,
        HexGrid? grid,
        CampaignContext viewer
    )
    {
        (_settlements, _armies, _sides, _held, _changes, _openTurn, _grid) = (
            settlements,
            armies,
            sides,
            held,
            changes,
            openTurn,
            grid
        );
        _everything = viewer.CanManage;
        // The viewer's side: their armies'.
        _mySides = [.. armies.Where(a => a.CommanderId == viewer.MemberId).Select(a => a.SideId)];
    }

    public static async Task<Scoreboard> LoadAsync(
        WwgDbContext db,
        Guid campaignId,
        CampaignContext viewer,
        CancellationToken cancellationToken
    )
    {
        var armies = await db
            .Armies.AsNoTracking()
            .Where(a => a.CampaignId == campaignId)
            .Select(a => new ArmyRow(a.Id, a.SideId, a.CommanderId))
            .ToListAsync(cancellationToken);
        var sides = await db
            .Sides.AsNoTracking()
            .Where(s => s.CampaignId == campaignId)
            // As the sides list has them: by name.
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Select(s => new SideRow(s.Id, s.Name))
            .ToListAsync(cancellationToken);
        var held = await db
            .Holdings.AsNoTracking()
            .Where(h => h.CampaignId == campaignId && h.ArmyId != null)
            .ToDictionaryAsync(h => new Hex(h.Q, h.R), h => h.ArmyId, cancellationToken);
        var changes = await db
            .HoldingChanges.AsNoTracking()
            .Where(c => c.CampaignId == campaignId)
            .OrderBy(c => c.Turn)
            .ThenBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);
        var open = await TurnRules.OpenTurnAsync(db, campaignId, cancellationToken);
        return new Scoreboard(
            await Holdings.SettlementsAsync(db, campaignId, cancellationToken),
            armies,
            sides,
            held,
            changes,
            open?.Number ?? 0,
            await CampaignMaps.GridAsync(db, campaignId, cancellationToken),
            viewer
        );
    }

    public ScoreboardResponse Response() => new(SideScores(), Settlements(), Turns(), Changes());

    private int Worth(Hex at) => _settlements.TryGetValue(at, out var s) ? s.Value : 0;

    private Guid? SideOf(Guid? armyId) => _armies.Find(a => a.Id == armyId)?.SideId;

    private bool Mine(Guid? armyId) => SideOf(armyId) is { } side && _mySides.Contains(side);

    private int Total(IReadOnlyDictionary<Hex, Guid?> holdings, Func<Guid, bool> counts) =>
        holdings.Where(h => h.Value is { } army && counts(army)).Sum(h => Worth(h.Key));

    private List<SideScoreResponse> SideScores() =>
        [
            .. _sides.Select(s => new SideScoreResponse(
                s.Id,
                s.Name,
                Total(_held, army => SideOf(army) == s.Id),
                [
                    .. _armies
                        .Where(a => a.SideId == s.Id)
                        .Select(a => new ArmyScoreResponse(
                            a.Id,
                            Total(_held, army => army == a.Id)
                        )),
                ]
            )),
        ];

    private List<SettlementScoreResponse> Settlements() =>
        _grid is null
            ? []
            :
            [
                .. _settlements
                    .Values.Where(s => _everything || Mine(_held.GetValueOrDefault(s.At)))
                    .OrderBy(s => s.At.R)
                    .ThenBy(s => s.At.Q)
                    .Select(s =>
                    {
                        var (latitude, longitude) = _grid.Centre(s.At);
                        return new SettlementScoreResponse(
                            s.At.Q,
                            s.At.R,
                            latitude,
                            longitude,
                            s.Name,
                            s.Value,
                            _held.GetValueOrDefault(s.At)
                        );
                    }),
            ];

    /// <summary>Each side's total after every turn, replaying the changes (at today's values).</summary>
    private List<TurnScoreResponse> Turns()
    {
        var turns = new List<TurnScoreResponse>();
        var replay = new Dictionary<Hex, Guid?>();
        for (var turn = 0; turn <= _openTurn; turn++)
        {
            foreach (var change in _changes.Where(c => c.Turn == turn))
            {
                replay[new Hex(change.Q, change.R)] = change.ToArmyId;
            }

            turns.Add(
                new TurnScoreResponse(
                    turn,
                    [
                        .. _sides.Select(s => new SideTotalResponse(
                            s.Id,
                            Total(replay, army => SideOf(army) == s.Id)
                        )),
                    ]
                )
            );
        }

        return turns;
    }

    private List<HoldingChangeResponse> Changes() =>
        [
            .. _changes
                .Where(c => _everything || Mine(c.FromArmyId) || Mine(c.ToArmyId))
                .Select(c => new HoldingChangeResponse(
                    c.Turn,
                    c.Q,
                    c.R,
                    _settlements.GetValueOrDefault(new Hex(c.Q, c.R))?.Name,
                    Worth(new Hex(c.Q, c.R)),
                    c.FromArmyId,
                    c.ToArmyId,
                    c.ByUmpire
                )),
        ];
}
