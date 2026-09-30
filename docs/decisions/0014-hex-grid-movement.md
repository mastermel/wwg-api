# 0014. A hex grid, terrain and the rules' movement replace free movement

- **Date:** 2026-09-30
- **Status:** Accepted

## Context

Phase 8 (decision 0010) moved units anywhere, as far as a straight-line limit per unit type. The
club's rule book (*Wasatch Wargame Club rules*, section XII, Campaign) plays campaigns on a hex
map: 3-mile hexes of flat, hilly, mountain or forest terrain, with roads, rivers and towns; units
move a number of hexes a turn by troop type, terrain and road; turns are 8 hours, three to a day;
and French and Russian or Austrian infantry march differently at certain times of day. The
maintainer chose to adopt this, and to keep battles (their execution and aftermath) off the app:
they're played out in person.

## Decision

- **The hex grid replaces free movement entirely.** Every campaign has one: flat-topped hexes,
  3 miles (4,828 m) across the flats by default, set per campaign while setting up. It's laid
  over the campaign's area in a local flat projection centred on it (accurate enough at campaign
  scale), hex (0, 0) at the centre, with axial coordinates (q, r). A unit is in a hex; positions
  and orders are hexes, not points.
- **Terrain per hex, inferred and then the Umpire's.** Each hex has a terrain (Flat, Low hill,
  High hill, Mountain, Water), a forest flag and a settlement (none, small city, large city,
  walled city, fortress); each edge between hexes may carry a road (good or poor), a river and a
  bridge. The app infers these from the map data it already draws (Mapterhorn elevation for
  relief, OpenFreeMap's land cover, water, places, roads and rivers), in the Umpire's browser,
  and the Umpire corrects any of it. What the Umpire sets is kept when inference runs again.
- **Movement by the rules' table.** The unit types follow the rules' movement classes (below).
  Entering a hex costs a share of the turn: 1 ÷ the class's rate for the hex's terrain, or for
  the road when the move follows one along the edge (a good road in high hills or mountains
  counts as poor). A forest hex moves as low hills. Rivers without a bridge, water, and terrain
  a class can't cross (the table's "–") are closed. A hex that costs more than a turn is entered
  over several turns. The Umpire may exceed the table (decision 0011), with a warning.
- **Time of day.** Turns are 8 hours: Morning (06–14), Afternoon (14–22) and Night (22–06),
  from a start date and time of day set per campaign; every turn is played (Night too). The
  time of day applies the rules' modifiers: French infantry and most of their allies (not
  Westphalians, Saxons, Spanish, Portuguese, Neapolitans or Danes) move one hex further each
  Morning; Russian and Austrian infantry and foot artillery one hex less each Afternoon. Moving
  by night counts towards forced marches (a later step).
- **Unit types**, by the rules' classes:

  | Class (rules' row) | Types |
  |---|---|
  | Infantry and foot artillery | Line infantry, Foot artillery, Engineers |
  | Light infantry, partisans | Light infantry, Partisans |
  | Light cavalry, scouts | Light cavalry, Scouts |
  | Medium and heavy cavalry | Medium cavalry, Heavy cavalry, Horse artillery |
  | Supply and siege artillery | Supply train, Siege artillery |

  Heavy infantry becomes Line infantry; Skirmishers become Light infantry (the rules have no
  separate class). Boats, leaders and couriers come with the steps that need them.
- **Battles stay off the app.** It may point out contact (opposing units in one hex) to the
  Umpire, but fighting, losses and their aftermath happen at the table; the Umpire edits units
  afterwards as needed.

## Consequences

- Movement limits in metres, the range circle and straight-line checks go; the map settings
  gain the hex size (and later the movement table), and the map shows the grid, the hexes a
  unit can reach this turn, and its path.
- Existing positions are converted to the hex that contains them, once, when the grid arrives.
- The C# and TypeScript sides each implement the grid's arithmetic; both are tested against the
  same figures so they agree.
- Later steps build on the grid (DESIGN.md §7, Phase 11): terrain, costed movement
  and time of day, contact and concentration, forced marches, attrition and supply, visibility
  by hex, towns and victory points, and engineering and sieges.
