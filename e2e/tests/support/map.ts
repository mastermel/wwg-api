import type { Page } from "@playwright/test";

/** The campaign map (MapLibre's canvas). */
export const campaignMap = (page: Page) => page.getByRole("region", { name: "Map", exact: true });

/**
 * Clicks the campaign map `right` and `down` pixels from its middle (up and left if negative),
 * where it is now (read after anything that scrolls).
 */
export async function clickMap(page: Page, right = 0, down = 0): Promise<void> {
  const box = await campaignMap(page).boundingBox();
  if (!box) throw new Error("The map isn't on the page.");
  await page.mouse.click(box.x + box.width / 2 + right, box.y + box.height / 2 + down);
}

/** Clicks the middle of the campaign map. */
export const clickMapCentre = (page: Page) => clickMap(page);
