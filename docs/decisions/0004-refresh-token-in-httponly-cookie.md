# 0004. Refresh token in an HttpOnly cookie, access token in memory

- **Date:** 2026-09-27
- **Status:** Accepted

## Context

§3.4 had login return both tokens in the response body, for the SPA to keep
(the plan was localStorage). Anything in localStorage can be read by injected
script, so an XSS bug would leak the long-lived refresh token, which works
from anywhere until it expires. The app is also becoming an installable PWA
where people stay signed in for weeks, which makes that token more valuable.

Options considered:
- **Both tokens in localStorage:** simplest; XSS steals both.
- **Access token in memory, refresh token in localStorage:** XSS still steals
  the refresh token.
- **Access token in memory, refresh token in an HttpOnly cookie:** script
  can't read the refresh token; an attacker can only act while the page is
  open. A small API change.
- **Cookie session only (no tokens):** nothing in JS at all, but replaces the
  bearer design and needs CSRF protection on every endpoint.

## Decision

- The **access token** (30 minutes) is returned in the response body and
  kept **in memory** by the SPA, sent as `Authorization: Bearer …`.
- The **refresh token** is set as a cookie, `__Secure-wwg-refresh`, with
  `HttpOnly; Secure; SameSite=Strict; Path=/api/auth`. It's never in a
  response body.
- Refresh tokens last **30 days, sliding**: each refresh issues a new one, so
  a session lasts as long as the app is used at least once every 30 days.
  Users always stay signed in (no "remember me").
- `POST /api/auth/refresh` reads the cookie. A new `POST /api/auth/logout`
  expires it, since script can't delete an HttpOnly cookie.
- The bearer scheme stays, so Swagger UI and any future non-browser client
  keep working with the access token.

## Consequences

- After a reload the SPA has no access token; it calls refresh on start-up.
  A network failure there must mean "offline", not "signed out".
- CSRF: the cookie only goes to `/api/auth/*` and only on same-site requests,
  and the refresh response can't be read cross-origin. No antiforgery token
  is needed for the supported browsers.
- Old refresh tokens stay valid until they expire (they aren't stored
  server-side), so parallel refreshes from several tabs don't conflict.
  "Sign out everywhere" and password/email changes still revoke everything
  through the security stamp.
- In development, Chrome accepts `Secure` cookies on `http://localhost`;
  Safari may not, so test Safari against HTTPS.
