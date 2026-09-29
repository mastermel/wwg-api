import type { Page } from "@playwright/test";

/** The campaign map (MapLibre's canvas). */
export const campaignMap = (page: Page) => page.getByRole("region", { name: "Map", exact: true });

/** Clicks the middle of the campaign map, where it is now (read after anything that scrolls). */
export async function clickMapCentre(page: Page): Promise<void> {
  const box = await campaignMap(page).boundingBox();
  if (!box) throw new Error("The map isn't on the page.");
  await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
}
