# 0021. Towns and victory points, held by armies

- **Date:** 2026-10-01
- **Status:** Accepted

## Context

Step 50 brings in the rule book's points for towns (Campaign, §C.3(b)): "depending on the scenario
and objectives cities may have a point value affixed to them. The last occupying troops will be
granted the points for holding a town", typically a town 10, a city 25, a walled city 35, a
fortress 50, a capital 25 more and a minor capital 10 more. The app knows each hex's settlement in
parts (a town or city, walled, with a fortress, a capital or minor capital; decision 0016). The
rules don't say how the parts combine, how a hex is taken when both sides are there, who holds
what at the start, or who knows the score.

## Decision

- **A settlement's value** is the highest of what applies (a town 10, a city 25, walled 35, a
  fortress 50), plus 25 for a capital or 10 for a minor capital. The Umpire can set another value
  for any settlement (0 for one that doesn't count), in the terrain editor.
- **Which settlements count** is the Umpire's choice for the campaign: **every settlement, by the
  rules** (the default, above), or **only those they give points**, for a scenario with a few
  objectives: a settlement is then worth nothing unless the Umpire sets its value. Values the
  Umpire set are kept when the choice changes, and the totals follow at once.
- **Settlements are held by armies** (the rules' "last occupying troops"), and totalled by side.
  The Umpire gives each its **starting holder** (an army, or no one) in the terrain editor.
- **Taking one:** as each turn closes, an army whose units are the only ones in a settlement's hex
  takes it from whoever held it (several armies of one side there: the one with the most points
  there). With both sides there, or none, it stays with its holder.
- **Who knows what:** every member sees each side's **total**; the settlements behind it (who holds
  each) are their own side's and the Umpire's. A **history** keeps each turn's changes and the
  totals after every turn: each side sees the totals, and the changes its own armies made or
  suffered; the Umpire sees everything.
- **On the map**, a flag in the holding army's colour by each settlement the viewer may know the
  holder of; the hex card says who holds it and what it's worth.

## Consequences

- New: a value override on a hex's settlement, the campaign's choice of which count, each settlement's holder, and a history of changes.
- Starting the next turn moves holdings after attrition, sightings, supply and couriers.
