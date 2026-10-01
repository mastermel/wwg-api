# 0018. Forced marches, attrition, and each unit's points history

- **Date:** 2026-10-01
- **Status:** Accepted (refines 0014)

## Context

Step 47 brings in the rule book's forced marches (Campaign, §E.2) and attrition (§F). A unit force
marches by moving three turns in a row (which takes in a night), or by moving a hex further in two
day turns; one forced march is free, and each further turn of it without rest costs attrition,
normal, then doubled, the doubling going on; it must rest as many turns as it force marched.
Attrition is points lost by Fighting Factor, for about two battalions (≈50 points): 3 for FF 1–2,
2 for 3–4, 1 for 5–6, ½ for 7–8, more for larger forces and less for smaller. The rules don't say
how the two ways combine, how partial rest counts, what FF 9 loses, or how half a point is lost
when points are whole numbers. In the rules the commander subtracts the points and the Umpire
checks; here the app works them out.

## Decision

- **A force-march order** is a move with "force march" set: a flat hex's worth further (as the
  Morning march), in a Morning or an Afternoon turn only. A night move needs no order: it counts
  through the moves in a row.
- **One count per unit**, worked out from its orders turn by turn (nothing new is stored for it, so
  it stays right when turns are reopened). In a run of moving turns (any Move, even part of the way
  into a hex), the **first forced march** is made by the third move, or by the second force-march
  order, whichever comes first: that's one turn of forced march, and free. **Each moving turn after
  it** is another turn of forced march: the second costs normal attrition, the third double, then
  ×4, ×8. **Each Hold works off one turn** of forced march; once none are left the run is over. A
  move before then carries the run on (and costs, by the count it has reached).
- **The loss** is the scale's figure × the turn's multiplier × the unit's points ÷ 50; FF 9 is as
  FF 7–8. Points stay whole: each unit **carries its fraction** over and loses a whole point when
  it adds up to one (so FF 7–8 lose one point every other turn, as the rules say).
- **The Umpire confirms each loss**: starting the next turn first lists the attrition the closing
  turn's orders cost, each unit's loss editable, and applies what the Umpire confirms. The API
  refuses to start the turn without a figure for each unit that owes one.
- **Each unit's points have a history**: every change, with its turn and reason (attrition, or the
  Umpire's edit), for every member to see, as they see the points. The march count shows where its
  army's moves do: to its commander and the Umpire.
- Attrition for staying concentrated too long, or fighting without rest, waits for battle records
  (46d); for supply and sieges, steps 48 and 51. The history's reasons leave room for them.

## Consequences

- `UnitOrder` gains `ForceMarch`; `ArmyUnit` an attrition carry; a new points-change table.
- Starting the next turn takes the confirmed losses.
