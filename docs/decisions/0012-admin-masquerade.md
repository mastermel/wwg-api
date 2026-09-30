# 0012. Admins masquerade as other users

- **Date:** 2026-09-30
- **Status:** Accepted

## Context

To help a Player, or check what someone can see, an Admin needs to use the app as that person,
without their password. It must be exactly that person's experience (their campaigns, their
permissions, nothing of the Admin's), clearly marked, and easy to leave.

## Decision

- **Admins only**, as any other user (not themselves), and not while already masquerading.
- **A session as the user, carrying who started it.** `POST /api/admin/users/{id}/masquerade`
  issues the target's own tokens (their claims and roles, from the database as on any sign-in),
  plus three claims: the Admin's ID, the Admin's security stamp, and when the masquerade ends.
  It replaces the Admin's refresh cookie. Every check sees the target, so the permissions match
  theirs exactly; an Admin masquerading as a Player is a Player.
- **It ends after 8 hours** (`Auth:MasqueradeLifetime`): neither token outlives it, and refreshing
  stops. Refreshing also checks the Admin still exists, is still an Admin and has the same
  security stamp, besides the target's own stamp as always; otherwise the session is over.
- **Ending:** `POST /api/auth/masquerade/end` (the masquerading session) checks the same and
  issues the Admin's own tokens, without a password. Signing out while masquerading signs out
  entirely; End masquerade is the way back.
- **The app log** records each start and end, with both users. What's done while masquerading
  is recorded as the user's own (turn history, emails), as it would look to anyone else.
- **The SPA** clears its cache (memory and the saved offline copy) on starting and ending, so no
  data crosses from one person to the other, and tells other tabs to do the same. While
  masquerading, the account button changes colour and says so, and its menu has End masquerade.
  `GET /api/me` says whether the session is a masquerade, and whose.

## Consequences

- Tokens stay stateless: the masquerade lives in their claims, checked on refresh. An access
  token already issued keeps working until it expires (at most 30 minutes) even if the Admin
  loses the role, as with any access token.
- Account changes that need the user's password (email, password) stay out of reach; the rest
  of the user's account (their name, signing out everywhere) is theirs to use, and so the
  masquerader's.
- Marking actions done while masquerading, and a stored audit trail, are left for later.
