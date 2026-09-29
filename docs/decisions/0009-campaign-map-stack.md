# 0009. The campaign map: MapLibre, OpenFreeMap, Mapterhorn and a server-side geocoder

- **Date:** 2026-09-28
- **Status:** Accepted

## Context

Each campaign gets an interactive map of its area, on which Players move their units turn by
turn (decision 0010). The Umpire sets its bounds, finding the area by searching for a place. The
map should show only what matters to a Napoleonic campaign (main roads, towns and cities,
rivers, forests and hills), in the app's own look, in light and dark mode.

Options for the map: Google Maps (an API key and billing, styling limited to Google's options,
no self-hosting, no tile caching), Leaflet with image tiles (can't remove or restyle anything
in a tile), and MapLibre GL JS with vector tiles.

## Decision

- **MapLibre GL JS** (open source, WebGL) draws the map, from React through `react-map-gl`.
  Vector tiles carry the data; our own style decides what's drawn, so layers can be left out,
  filtered (e.g. by road class) and coloured per colour scheme.
- **OpenFreeMap** serves the tiles (OpenMapTiles schema, from OpenStreetMap): free, no API key,
  no usage limits. The OpenStreetMap and OpenFreeMap attribution stays on the map.
- **Mapterhorn** elevation tiles (free, no key) give hillshading; AWS Terrain Tiles are the
  fallback. Hills are 2D shaded relief, not a 3D view.
- **Unit icons** are NATO military symbols drawn by `milsymbol` (APP-6 / MIL-STD-2525, SVG, no
  dependencies), one per unit type, filled with the army's colour.
- **Place search** goes through our own Umpire-only endpoint, which calls a geocoding service
  from the server: MapTiler (a key, 100,000 searches a month free) or Photon (no key, fair use).
  The key stays on the server, the endpoint is rate-limited, and the browser's CSP doesn't
  change for it. Not Google: its terms don't allow showing its geocoding results on another
  map. Not Nominatim's public server: it doesn't allow search-as-you-type.
- The map is **online only** for now: the service worker doesn't cache tiles.

## Consequences

- The CSP (§3.11) allows the tile, glyph, sprite and elevation hosts in `connect-src`, and
  `blob:` in `worker-src` and `img-src` (MapLibre's workers and images).
- OpenFreeMap is one person's donation-funded service. If it becomes a concern, or when the map
  should work offline, the fallback is a **Protomaps** PMTiles extract of the campaign region,
  hosted by the app itself (a different tile schema, so the style needs adapting).
- OpenStreetMap is today's world. The style leaves out what the period didn't have (motorways,
  railways, modern borders, suburbs), but modern reservoirs still show as lakes, and names are
  modern (in the language the Umpire chooses).
- MapLibre needs WebGL, which jsdom lacks: component tests stub the map; the e2e suite's
  Chromium renders the real one.
