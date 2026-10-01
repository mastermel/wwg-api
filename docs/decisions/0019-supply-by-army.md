# 0019. Supply: each army's depots, routes by road and waterway, living off the land

- **Date:** 2026-10-01
- **Status:** Accepted (refines 0017 and 0018)

## Context

Step 48 brings in the rule book's supply (Campaign, §G). Each side sets up depots in safe rear
areas; supply runs from an army's depot along roads and rivers to its forces; an enemy force of 5
points or more in a hex on the line cuts it, and reopening it takes twice the enemy's strength.
Intermediate depots supply the troops ahead of them for 15 turns once cut off. Troops cut off
for 6 turns suffer attrition each turn after, may only defend, and a forced march costs them
double. Some troops are exempt (partisans, light infantry, cavalry on reconnaissance, scouts), and
French forces living off the land can't be out of supply but must spread out to half the
concentration allowed. The rules don't say how close to a road a unit must be, which rivers carry
supply, whose depots an army may use, or who manages them.

## Decision

- **Depots belong to armies**, not sides: an army's units are supplied only by its own depots
  (§G.1's "an army's depot"; §G.2(a)'s side "establishes" them). A depot is **main** or
  **intermediate**. For now the **Umpire** places, moves, captures and destroys them, on the
  commanders' behalf; the app warns the Umpire when enemy units end a turn in a depot's hex.
- **Routes** are roads (good or poor) and **waterways**, hex to hex. Rivers along hexsides are
  obstacles, not routes. A unit is supplied when it's within the campaign's **supply reach** (the
  Umpire's setting: 0 to 3 hexes, 1 by default) of a hex its army's route reaches from a depot that
  supplies.
- **A hex cuts a route** when it holds 5 or more enemy points, unless the army's side has at least
  twice as many points there. Anything closer is the Umpire's to settle at the table.
- **An intermediate depot** supplies while its own route reaches a main depot of its army, and for
  15 turns after it's cut off.
- **Unsupplied** turns are counted per unit, worked out turn by turn from where units ended each
  turn, as forced marches are. From the 7th in a row, each costs normal attrition (the rules'
  scale, × points ÷ 50, as in decision 0018), added to any forced march's, which is **doubled while
  unsupplied**. It goes in the same list the Umpire confirms when starting the next turn.
- **Exempt types** are a campaign setting (partisans, light infantry, scouts and light cavalry by
  default).
- **Living off the land** is a state on a unit, set with its turn's order by its commander (or the
  Umpire), for units of the nations a campaign setting names (France by default). Such a unit is
  never out of supply; a side's hex holding any unit living off the land is held to **half** the
  concentration limits (decision 0017).
- **Who sees it:** an army's depots and its units' supply are its commander's and the Umpire's,
  as its moves are.

## Consequences

- New: depots (per army), supply settings on the campaign (reach, exempt types, the nations that
  live off the land), and `UnitOrder.LivesOffTheLand`.
- Contact and concentration (step 46) halves a side's limits in a hex with a unit living off the
  land; the attrition list gains supply's reason.
