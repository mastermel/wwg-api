# 0011. The Umpire edits orders on a commander's behalf

- **Date:** 2026-09-29
- **Status:** Accepted

## Context

Decision 0010 made orders the commander's alone and left "the Umpire editing anything in any
turn" for later, along with a way past an army with no commander (which holds the campaign up).
At the table an Umpire often fixes an order (a slip of the map, a ruling, a forced march), or
moves an army whose Player is away. Sending the turn back for every fix is slow.

## Decision

- **The open turn only.** The Umpire (and Admins) can give, change and take back orders for any
  army's units in the open campaign turn. Closed turns stay as they happened: later turns' orders
  are absolute positions measured from them, so rewriting the past would make units jump.
- **Draft or Submitted.** A Submitted turn stays Submitted when the Umpire edits it (the fix
  before approving); an approved one must be reopened first. The commander still edits only a
  Draft.
- **Submit on the army's behalf:** the Umpire can submit a Draft, with the same rule (every unit
  on the map has an order). That is the way past an army with no commander. Approving also
  checks the rule, since the Umpire can take an order back from a Submitted turn.
- **Limits:** the Umpire's moves stay inside the campaign's area but may go past the unit's
  movement limit (the UI warns first); the API doesn't hold the Umpire to the limit.
- **Marked and recorded:** an order records whether the Umpire set it, and the commander's panel
  says so. Each change goes in the army turn's history as an **Edited** event, with a note per
  unit; changes in a row by the same person add to one event rather than a new one each.
- **Emails:** no email per edit. The approve, send-back and (when the Umpire submits) submit
  emails list the orders the Umpire set.
- Last write wins when the commander and the Umpire change the same draft at once; each sees
  the other's change when their page refreshes.

## Consequences

- `GiveOrder`, `UndoOrder` and `SubmitTurn` move from `OwnCommander` to `Commander` access (the
  army's commander, the Umpire, Admins); the handlers apply the status and limit rules by role.
  `OwnCommander` has no users left and goes.
- `UnitOrder` gains `ByUmpire`, and `UnitPosition` with it; `ArmyTurnEventKind` gains `Edited`.
- Editing closed turns, and anything about a unit beyond its order, stay for later.
