# 0023. Email notifications: whenever there's something to do, each one a user can turn off

- **Date:** 2026-10-02
- **Status:** Accepted

## Context

The app emails a password reset, a changed email address, and the turn's steps: a submitted turn
to the Umpire, and to a commander their turn submitted, approved, sent back or reopened by the
Umpire, and each new turn ("turn N has started"). Nothing is sent when the campaign starts, when
someone joins or is given an army, or when every army has submitted; the turn's email says
nothing of what happened as it closed; no address is ever confirmed; and no one can turn any of
it off.

## Decision

- **Whenever there's something new to do in a campaign, the person who must do it is emailed.**
- **A new turn** (the campaign's first too) emails each army's commander, one email per army: the
  turn's number, its date and time of day, and what's **new since the last turn** for the army:
  its sightings, its units out of supply (newly, then each turn after, with the turn count) and
  its depots cut off, points it lost to attrition, boats it built, and reports from allies that
  arrived (each one's message too); "Nothing new since the last turn" when there's none.
- **New emails:** a player given an army (made its commander, or given it at its making); the
  Umpires, when a player joins their campaign, and when every army has submitted the open turn,
  with what's waiting for them as they start the next (sightings to shape, couriers among the
  enemy that may be intercepted, attrition to confirm).
- **Welcome and confirmation:** registering sends a welcome with a link to confirm the address
  (Identity's email confirmation); changing it sends one to the new address. Unconfirmed accounts
  aren't held back: they sign in and get their emails as before, and the app asks them to
  confirm (and can send the link again).
- **Each user can turn off any campaign email** in their account settings, kind by kind; the
  account's own emails (welcome, confirmation, password reset, a changed address) always go.

## Consequences

- New: a user's turned-off email kinds, the confirmation link and its page, and the turn email's
  news, worked out from what the closing turn recorded.
