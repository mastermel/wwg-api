# 0013. The app is called Wasatch Wargamers, with a new mark

- **Date:** 2026-09-30
- **Status:** Accepted

## Context

The app was named WWG Campaigner (decision 0005), with a drawn silhouette of Napoleon on a
rearing horse. The club bought a vector image for its brand, and wants the app to carry the
club's own name.

## Decision

- **The displayed name is "Wasatch Wargamers"** everywhere people see it: page titles, the
  header, the manifest (short name "Wargamers", as launchers truncate longer labels), the install
  and update prompts, the About page and the emails. Code, packages, images and other names no one
  sees keep `wwg-campaigner` / `wwg`.
- **The mark** is the purchased silhouette of an officer (converted from EPS to SVG through PDF),
  cropped to the figure. It's kept as a mask: the figure's shapes show and its white details
  cut through, so it takes the theme's colours in light and dark mode, as the old mark did.
- **The app icon** is the officer in white on a rounded square with a navy-to-silver gradient,
  top to bottom. The maskable and Apple icons use it without padding, since the platforms cut
  their own shape; the figure keeps to Android's safe middle circle.

## Consequences

- The original EPS stays outside the repo (it's the purchased file); `web/public/logo.svg` is the
  artwork the app uses, and `app-icon.svg` embeds the same shapes.
