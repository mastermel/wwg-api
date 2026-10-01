import { useState } from "react";
import type { MapBounds, MapLayers } from "@/api/generated/model";
import { distanceMetres } from "@/features/maps/geo";

/**
 * What each viewer shows on the map (the Map page's Layers panel): the real map's layers, from
 * OpenStreetMap, and the game map's, which the app draws from the campaign's terrain. A viewer
 * can hide what the campaign's map settings show, never show what they hide. The game map is
 * drawn only once the view is close enough to read it.
 */

export const realLayers = [
  { key: "roads", label: "Roads" },
  { key: "places", label: "Place names" },
  { key: "water", label: "Water" },
  { key: "forests", label: "Forests" },
  { key: "hills", label: "Hills" },
  { key: "contours", label: "Contours" },
] as const;

export type RealLayer = (typeof realLayers)[number]["key"];

export const gameLayers = [
  { key: "grid", label: "Grid" },
  { key: "terrain", label: "Terrain" },
  { key: "roads", label: "Roads" },
  { key: "rivers", label: "Rivers & waterways" },
  { key: "towns", label: "Towns & cities" },
  { key: "bridges", label: "Bridges" },
  { key: "warnings", label: "Contact & concentration" },
] as const;

export type GameLayer = (typeof gameLayers)[number]["key"];

/** What a viewer has hidden. */
export interface HiddenLayers {
  real: RealLayer[];
  game: GameLayer[];
}

const nothingHidden: HiddenLayers = { real: [], game: [] };

/** The game map is drawn once the view's longest side spans this many hexes or fewer. */
export const gameMaxHexesAcross = 20;

/** How many hexes the view's longest side spans. */
export function hexesAcross(view: MapBounds, hexSize: number) {
  const middle = (view.south + view.north) / 2;
  const across = distanceMetres(
    { latitude: middle, longitude: view.west },
    { latitude: middle, longitude: view.east },
  );
  const down = distanceMetres(
    { latitude: view.south, longitude: view.west },
    { latitude: view.north, longitude: view.west },
  );
  return Math.max(across, down) / hexSize;
}

/** The real map's layers the campaign shows, less those the viewer hid. */
export const shownRealLayers = (campaign: MapLayers, hidden: HiddenLayers): MapLayers => ({
  ...campaign,
  ...Object.fromEntries(
    realLayers.map(({ key }) => [key, campaign[key] && !hidden.real.includes(key)]),
  ),
});

const storageKey = (campaignId: string) => `wwg:map-layers:${campaignId}`;

/** What the viewer hid on this campaign's map, as this device remembers it. */
export function loadHidden(campaignId: string): HiddenLayers {
  try {
    const saved = JSON.parse(
      localStorage.getItem(storageKey(campaignId)) ?? "null",
    ) as Partial<HiddenLayers> | null;
    const real = new Set<string>(realLayers.map((l) => l.key));
    const game = new Set<string>(gameLayers.map((l) => l.key));
    return {
      real: (saved?.real ?? []).filter((key): key is RealLayer => real.has(key)),
      game: (saved?.game ?? []).filter((key): key is GameLayer => game.has(key)),
    };
  } catch {
    // Storage may be blocked or hold something else: show everything.
    return nothingHidden;
  }
}

function saveHidden(campaignId: string, hidden: HiddenLayers) {
  try {
    localStorage.setItem(storageKey(campaignId), JSON.stringify(hidden));
  } catch {
    // Not remembered on this device: it still applies until the page closes.
  }
}

/** The viewer's hidden layers on a campaign's map, remembered on this device. */
export function useHiddenLayers(campaignId: string) {
  const [hidden, setHidden] = useState(() => loadHidden(campaignId));
  const change = (next: HiddenLayers) => {
    setHidden(next);
    saveHidden(campaignId, next);
  };
  return {
    hidden,
    toggleReal: (key: RealLayer) => {
      change({
        ...hidden,
        real: hidden.real.includes(key)
          ? hidden.real.filter((k) => k !== key)
          : [...hidden.real, key],
      });
    },
    toggleGame: (key: GameLayer) => {
      change({
        ...hidden,
        game: hidden.game.includes(key)
          ? hidden.game.filter((k) => k !== key)
          : [...hidden.game, key],
      });
    },
    reset: () => {
      change(nothingHidden);
    },
  };
}
