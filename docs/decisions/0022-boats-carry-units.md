# 0022. Units embark on boats, which go with them; boats are built in river towns

- **Date:** 2026-10-01
- **Status:** Accepted

## Context

Rivers can't be forded: they're crossed only by bridge or by boat, and are usually navigable their
whole length (Campaign, §C.4(a)). Boats are a unit type that moves along waterways (step 44,
decision 0016), but nothing yet carries troops. The rules say loading takes a full turn, and so
does unloading; a small river is crossed in two moves (§N.1(b)); Chart #11 has a boat hold 14
points, which the Umpire may judge otherwise. Almost every unit has more than 14 points, so one
boat a unit doesn't work, and gathering boats into flotillas that carry several units would make
the boats, not the units, what moves and is counted.

## Decision

- **A unit embarks on boats, which become tied to it.** It needs a boat for every `BoatCapacity`
  points or part of them (a campaign setting, 14 by default): a 20-point brigade takes two. Its
  army's free boats in its hex are tied to it by an **Embark** order, which takes the whole turn
  (the boats chosen by the app: they're all alike). A boat is tied to one unit at a time; its own
  army's only, for now.
- **Embarked, the unit moves as a boat** (along waterways and lakes, by the boats' rates, never
  overland) and its boats go with it: they have no orders or markers of their own, so the unit
  is still what's counted for concentration, contact, sight and supply. Boats are free of
  concentration by default (decision 0017).
- **Disembark** takes the whole turn too: the unit lands in its hex, across a river side of it, or
  from a lake onto a shore beside it, and its boats stay in the water hex it left, free again.
  Embarking and landing across the river is the rules' crossing in two moves.
- **Boats are tied only when embarking.** A unit whose points fall keeps its boats; one whose
  points the Umpire raises past them is flagged to the Umpire, not stopped. Boats are freed where
  the unit is when it falls to 0 points or leaves the game.
- **While embarked:** being carried is rest (no forced marches, none owed); no living off the
  land; no taking a settlement until it lands. The Umpire may show an enemy that a sighted unit
  is embarked, as a detail of the sighting.
- **Boats themselves hold nothing:** a boat, carrying or not, never takes a settlement, and boats
  count for neither side where a supply route is cut (5 enemy points unless 2:1, decision 0019).
- **Who embarks:** any land unit that's commanded (infantry, artillery, cavalry, siege artillery);
  not supply trains, nor boats.
- **Building (or procuring) boats:** a land unit in a settlement hex with a waterway, and no enemy
  units in it, works there two turns in a row (a **Build boat** order) and, as the second
  closes, a new Boat joins its army there: a generic one, made by the app, not taken from the
  library. Any other order before then loses the work. A unit may go on to build another.
- **Worked out, not stored:** who is embarked, on which boats, and what's been built so far come
  from replaying the orders turn by turn, as forced marches do (decision 0018), so reopening a
  turn can't leave them stale.

## Consequences

- New: the Embark, Disembark and Build boat orders; the campaign's boat capacity; a sighting's
  embarked detail.
- Movement gains a unit moving by its boats' class; the turn's checks skip tied boats, which
  have no orders of their own.
- Moving boats between armies, allies' units on a side's boats, and supply trains on boats are
  later work.
