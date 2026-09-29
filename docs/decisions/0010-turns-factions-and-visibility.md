# 0010. Turns in step, per army; orders; factions; one visibility rule

- **Date:** 2026-09-28
- **Status:** Accepted

## Context

Campaigns progress in turns: Players move their armies' units on the campaign map (decision
0009) and the Umpire approves each move. The maintainer described how the club runs campaigns:
everyone moves turn 4 before anyone moves turn 5; armies belong to sides; at the start everyone
knows every army and its units, but not where they are.

## Decision

- **Turns are per army**, not per Player: armies can have no commander, or change commanders,
  and their history must survive that. Colours are per army too.
- **Turns stay in step.** A campaign turn (a number) is open or closed; each army has its own
  turn for it (Draft, Submitted, Completed). The next turn opens only when the Umpire presses
  **Start turn N+1**, once every army's turn N is Completed. An army with no commander can't
  submit, so it holds the campaign up: the Umpire has to find a commander.
- **Turn 0 is setup.** The Umpire places every unit, then presses **Start campaign**: every army
  must have a faction and every unit a position.
- **Each unit gets an order per turn:** Move (to a position) or Hold. No order yet is allowed in
  a Draft; a turn can be submitted once every unit has one. Undo removes the order.
- A unit's **current position** is its position in the latest Completed turn.
- The Umpire can **send back** a Submitted turn, or **revert** a Completed one to Draft, but
  only in the open campaign turn, and with a note for the turn and for particular units. Later
  turns never exist then (the next turn isn't open), so nothing else is deleted.
- **Factions:** the Umpire creates them, and assigns each army to one. A new army has none
  ("Unassigned"). Every army needs a faction before the campaign starts.
- **Every Player sees every army and its units** (read-only), whatever their faction: the
  club shares that at the start of a campaign. Only **positions** are private.
- **One visibility rule** decides who sees positions: the Umpire and Admins see all; a
  commander sees their own army's. Every position read goes through it, so the planned
  intelligence sharing (allies' positions for a past turn) and scouting (chosen enemy units for
  a chosen turn) become records that grant more, not changes to every endpoint.
- At most **8 armies per campaign**, the size of the colour palette.
- After the campaign starts: an army or unit can still be added (the Umpire places it before
  the next turn opens), but not deleted, which would erase its history. "Destroyed" comes later.

## Consequences

- The §5.2 permission matrix changes: viewing any army's units is open to every member.
- Every turn action emails the other side (the Umpire, or the army's commander).
- Left for later: the Umpire editing anything in any turn, destroyed units, other ways past a
  commanderless army, intelligence sharing and scouting, turn deadlines, turning emails off.
