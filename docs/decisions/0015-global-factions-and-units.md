# 0015. Factions and units are the club's, shared by every campaign

- **Date:** 2026-09-30
- **Status:** Accepted

## Context

Units lived inside an army, and armies inside a campaign; factions were a campaign's sides.
The club reuses its units (its painted figures) from one campaign to the next, and wants to set
up a campaign by choosing them from one shared pool, grouped by faction. Armies change from
campaign to campaign, and so do alliances: Austria fought beside France in 1812 and against it
in 1813.

## Decision

- **A library of factions and units, shared by the whole app.** A **faction** is a collection
  of units (French, British, Prussian…), with a name and, optionally, a nation for its flag. A
  **unit** belongs to one faction and has a name, type, FF and points. Duplicates are allowed.
- **Sides stay per campaign.** What Phase 8 called a campaign's factions are now its **sides**
  (Coalition, French Empire…): the Umpire creates them, each army is on one, and every army needs
  one before the start. Everything Phase 8 said of factions (visibility later by side, "Put X on
  a side") applies to sides.
- **Armies stay per campaign, and take units from the library.** A unit joins a campaign as an
  **army unit**: a roster entry in one army, with the unit's name, type, FF and points *copied*
  at that moment. What happens in the campaign (losses, attrition, the Umpire's edits) changes the
  army unit only; editing the library unit changes the campaigns it joins afterwards. A library
  unit is in at most one army per campaign, but can be in several campaigns at once.
- **Armies can mix factions.** The Umpire picks a faction to browse when adding units, but an army
  may take units from any (allied contingents).
- **Who manages the library:** anyone who is the Umpire of a campaign, and Admins, can create and
  edit factions and units; deleting one is refused (409) while any campaign uses it. Everyone
  signed in can browse it.
- **Adding units:** from an army, the Umpire chooses a faction and ticks the units to add (those
  already in the campaign are shown as taken), or creates a new unit, which goes into the library
  and the army at once. Removing an army unit, before the start, leaves the library unit; after
  the start army units can't be removed, as units couldn't before.
- Orders, positions, turn notes and the visibility rule refer to **army units**: the history of a
  campaign is its own.

## Consequences

- Phase 8's `Faction` becomes `Side` (table, routes, UI); a new global `Faction` and `Unit`
  arrive; the old `Unit` becomes `ArmyUnit`, with the library unit it came from. The migration
  puts every existing unit into the library, in a faction named after its army's nation (or
  "Unsorted" for an army with none), so nothing is lost and nothing has to be re-entered.
- Unit ownership (whose figures a unit is), and saving a campaign's changes back to the library,
  are left for later.
