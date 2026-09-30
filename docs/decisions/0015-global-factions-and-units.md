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
- **An army selects its factions**, one or more (so it can mix them: allied contingents), and
  takes its units only from those. A faction can't be taken off an army while it has units from it.
- **A new role, Manager.** Managers and Admins create, edit and delete the library's factions and
  units, in the library's own pages; deleting one is refused (409) while a campaign uses it.
  Admins make users Managers, and stop, on the user's admin page. Everyone signed in can view the
  library.
- **Campaigns only choose from the library.** From an army, the Umpire ticks the units to add,
  from the army's factions only (those already in the campaign are shown as taken). Nothing in a
  campaign creates or edits library items. Removing an army unit, before the start, leaves the
  library unit; after the start army units can't be removed, as units couldn't before.
- Orders, positions, turn notes and the visibility rule refer to **army units**: the history of a
  campaign is its own.

## Consequences

- Phase 8's `Faction` becomes `Side` (table, routes, UI); a new global `Faction` and `Unit`
  arrive, and `ArmyFaction` links armies to the factions they select; the old `Unit` becomes
  `ArmyUnit`, with the library unit it came from. A `Manager` Identity role joins `Admin`.
- The migration puts every existing unit into the library, in a faction named after its army's
  nation (or "Unsorted" for an army with none), and selects that faction for the army, so nothing
  is lost and nothing has to be re-entered.
- Unit ownership (whose figures a unit is), and saving a campaign's changes back to the library,
  are left for later.
