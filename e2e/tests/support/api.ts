import type { Page } from "@playwright/test";

/**
 * The API as the signed-in user of `page`: for setting things up that the test isn't about
 * (e2e/CLAUDE.md). Swaps the browser's refresh cookie for an access token, as the app does.
 */
export async function apiAs(page: Page) {
  const request = page.context().request;
  const refreshed = await request.post("/api/auth/refresh");
  if (!refreshed.ok()) throw new Error(`Refreshing failed: ${String(refreshed.status())}`);
  const { accessToken } = (await refreshed.json()) as { accessToken: string };
  const headers = { Authorization: `Bearer ${accessToken}` };

  const send = async (method: "GET" | "POST" | "PUT", path: string, data?: unknown) => {
    const response = await request.fetch(path, { method, headers, data });
    if (!response.ok()) {
      throw new Error(
        `${method} ${path} failed: ${String(response.status())} ${await response.text()}`,
      );
    }
    return (await response.json()) as unknown;
  };
  return {
    get: <T>(path: string) => send("GET", path) as Promise<T>,
    post: <T>(path: string, data: unknown) => send("POST", path, data) as Promise<T>,
    put: <T>(path: string, data: unknown) => send("PUT", path, data) as Promise<T>,
  };
}

const unitTypes = [
  "HeavyInfantry",
  "LightInfantry",
  "Skirmishers",
  "HeavyCavalry",
  "LightCavalry",
  "FootArtillery",
  "HorseArtillery",
];

/** The map settings the tests use: the country around Waterloo. */
export const waterlooMap = {
  bounds: { west: 4.2, south: 50.6, east: 4.6, north: 50.8 },
  labelLanguage: "en",
  distanceUnit: "Kilometres",
  layers: { roads: true, places: true, water: true, forests: true, hills: true, contours: false },
  movementLimits: unitTypes.map((unitType) => ({ unitType, metres: 20_000 })),
};
