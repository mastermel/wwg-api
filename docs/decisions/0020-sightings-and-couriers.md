# 0020. Sightings the Umpire shapes, and intelligence by courier between allies

- **Date:** 2026-10-01
- **Status:** Accepted (refines 0010's visibility rule)

## Context

Step 49 brings in the rule book's reconnaissance (Campaign, §K) and communication (§L). Troops see
into the next flat hexes, further from hills (low 2, high 3, mountain 4), less into hills; what
they see at a distance is "of a general nature only", detail only in the same hex, and the Umpire
judges. Light troops can screen; scouting parties roll to get through; spies are the scenario's.
Information between players further apart than "two moves" goes by courier, by horse, foot or
boat, at the normal movement speed. Until now a commander saw only their own army.

## Decision

- **Enemy sightings are the Umpire's to shape.** When the next turn starts, the app lists, for
  each army, the hexes of the other side's units in sight of its units where the closing turn left
  them. Sight runs by the map's terrain (never the actual terrain rolled for a hex) in elevation
  steps: from flat 1 hex, a low hill 2, a high hill 3, a mountain 4; a hill hex blocks the view
  beyond it unless the observer stands higher. One **sighting per enemy hex**, prefilled with
  suggested details, which the Umpire changes or skips: **the hex** (or only roughly where),
  **army and nation**, **unit types**, and **strength** (exact, or a rough size). The prompt flags
  a possible **screen** (the observed side's light infantry or light or medium cavalry in
  between); the Umpire decides what's seen. The Umpire can **add** a sighting of any enemy hex for
  any army (spies, scouting parties).
- **A sighting belongs to its turn**, the turn it was made for and received on, and is **kept**:
  on that turn it's drawn in full; for the **3 turns after**, it's drawn **faded**, as past
  positions are; after that it's gone from the open turn's map, but choosing its turn in the turn
  history draws it again (with the 3 turns before it, faded). Turns with sightings are marked in
  the turn history. A sighting never carries on by itself: units still in sight after both sides
  have moved bring a new prompt, and so a new sighting, on the next turn.
- **Allies are not sighted.** A commander learns of their allies only by **intelligence they
  send**: a report with any of a **snapshot** of their army's units (hexes and points), **all the
  sightings** they've received so far (each with its turn), and a **note** of up to 1,000
  characters. Once it arrives, the ally has those sightings too, on their own turns in the turn
  history (marked as the sender's), by the same rule of turns.
- **A courier carries each report**, tracked behind the scenes, not drawn as a unit: from the
  sender's unit nearest the recipient towards the recipient's unit nearest it (re-aimed each
  turn), riding as light cavalry by the campaign's movement table over the terrain and roads,
  every turn, nights too. Within two turns' ride when sent, it arrives at the start of the next
  turn. The Umpire sees every courier, is warned when one is in a hex with enemy units, and can
  stop it (it never arrives) or let it through.
- **Received reports are kept**: an Intelligence list of every report (from whom, sent on turn N,
  arrived on turn M), each viewable on the map (its snapshot) with its note; its sightings join
  the recipient's own, on their turns.
- The Umpire sees everything, as before.

## Consequences

- The visibility rule gains sightings and received reports; positions and orders stay their
  army's (and the Umpire's).
- Starting the next turn also takes the Umpire's sightings, beside attrition.
- New: sightings (per observing army and turn), reports and their couriers.
