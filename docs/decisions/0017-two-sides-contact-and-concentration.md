# 0017. Two sides a campaign, contact between them, and concentration by side

- **Date:** 2026-10-01
- **Status:** Accepted (refines 0010 and 0015)

## Context

Step 46 shows the Umpire where opposing armies meet and where too many troops crowd a hex. The
rule book (Campaign, §H) limits a hex to 200 points of infantry or 160 of cavalry, except for the
three turns before and after a battle; large cities and fortresses allow double; staying
concentrated longer costs the whole force attrition. It doesn't say whose troops count together,
which types count as infantry or cavalry, or who "opposing" is when a campaign has several sides,
or armies on none.

## Decision

- **Every campaign has exactly two sides**, made with it (Side 1 and Side 2, renamable); they
  can't be added or deleted, and **every army is on one** (no "Unassigned"). Opposing always means
  the other side. Existing campaigns keep their first two sides by name, any others are merged
  into the second, and armies on none go to the first, for the Umpire to move.
- **Concentration is counted per side, per hex**: each side's points of the types the campaign
  counts towards each limit. The Umpire chooses, on the campaign's settings, which unit types
  count towards the infantry limit and which towards the cavalry limit (by default line and light
  infantry, engineers, partisans and foot artillery; light, medium and heavy cavalry, scouts and
  horse artillery); the rest (supply trains, siege artillery, boats, by default) are free. The
  limits are the Umpire's too (200 and 160 by default), each judged on its own in a mixed hex, and
  doubled in a large city (a City) or a fortress. Walled towns don't double them.
- **Contact** is a hex holding units of both sides. The Umpire sees contact and concentration in
  the open turn from the orders as given (where units will be), and on past turns from where they
  ended up. Both are warnings: nothing is refused, and battles stay at the table.
- **Battles are recorded later**, by the Umpire: the hex, the units involved, and their points
  afterwards. Until then every hex over a limit is warned of; once they exist, concentration
  within three turns of a battle in that hex is allowed, as the rules say.
- Units whose paths cross during a turn (meeting mid-move) are left for later consideration.

## Consequences

- Side creation and deletion go; `Army.SideId` becomes required, and the army forms must choose a
  side. The start check "Put X on a side" goes with it.
- The campaign gains its concentration settings (types and limits), beside its calendar.
- Attrition for staying concentrated comes with step 47; French living off the land at half
  concentration, with supply (step 48).
