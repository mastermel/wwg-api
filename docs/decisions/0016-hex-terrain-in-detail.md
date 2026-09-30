# 0016. A hex's map terrain, its settlement in parts, its actual terrain, and river courses

- **Date:** 2026-09-30
- **Status:** Accepted (refines 0014)

## Context

Decision 0014 gave each hex one terrain, a forest flag and one settlement, and each edge a road,
a river and a bridge (step 42). The rule book's Campaign section (XII.C, pp. 56–57) asks for more:

- **Two levels of terrain.** The map shows each hex's general character (flat, low hill, high
  hill, mountain, forest), which movement and visibility use. On a player's request the Umpire
  finds a hex's *actual* terrain by shaking three dice: the red die for the landscape ("rolling,
  sm. village and woods"), plus 1 for low hills or forests and 2 for high hills or mountains; the
  white die for a dominant feature (small castle, weak or strong farmhouse); the green die for
  favourability, only when both sides come onto a battlefield together. The result binds, and is
  given to the players who asked (later to all). The hex keeps its map character for movement
  (§C.3(d)).
- **Settlements combine.** A town or city can be walled, a fortress can stand in a city, and a
  capital or minor capital scores more (§C.4(b): town 10, city 25, walled city 35, fortress 50,
  capital +25, minor capital +10). One value can't say "a walled capital city".
- **Rivers do two things.** They're unfordable, crossed only by bridge (§C.4(a)), so they lie
  between hexes; and boats travel along them, 4 hexes a turn downstream, 2 upstream (§E.1), so
  they also run from hex to hex, as the legend draws them, "like good roads".

## Decision

- **A hex's map terrain stays one relief** (Flat, Low hill, High hill, Mountain, Water) **and a
  forest flag**: the movement table needs exactly one.
- **Its settlement is in parts:** a size (None, Town, City), Walled, Fortress, a capital status
  (None, Minor, Capital) and an optional name. Walled and a capital need a town or city; a
  fortress can stand alone.
- **A hex's actual terrain is its own record**, made when a player asks: the landscape's relief
  (Flat, Rolling, Hilly, High hills) and any of its features (scrub, village, woods, forest,
  farms, fields, streams); a dominant feature (none, small castle, weak or strong farmhouse); and
  favourability (favourable, neutral, unfavourable, or not rolled), for the army that asked. The
  app rolls the dice for the Umpire, with the rules' modifier from the hex's map terrain (the
  larger one when a hex has two, and the red die kept to 0–7; the Umpire may take one off a flat
  hex), fills the record from the table, and keeps the dice. The Umpire can change any of it,
  and reveals it to armies, or to all. Movement and visibility ignore it.
- **Edges carry two kinds of river:** a **river along the edge** (it separates the two hexes;
  only a bridge crosses it, as before), and a **waterway across the edge** (a navigable course
  from one hex to the next, with the way it flows), which boats will follow.
- **Everyone sees the same map terrain.** The Umpire's secret changes to their own map
  (§C.1(a)) are left for later.

## Consequences

- `HexCell.Settlement` becomes `SettlementSize`, `Walled`, `Fortress`, `Capital` and `Name`;
  `HexEdge` gains `Waterway` (None, or the way it flows: from the hex into the one across, or back).
  The actual terrain is a new `HexDetail`, with the armies it's shown to.
- Inference (step 43) marks a river's course where its line crosses an edge (a waterway) and
  puts the river along the edges nearest its line, where it lies between hexes.
- Boats' movement comes with costed movement; scoring towns, with objectives, later.
