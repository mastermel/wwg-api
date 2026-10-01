import type { Page } from "@playwright/test";

/** The campaign map (MapLibre's canvas). */
export const campaignMap = (page: Page) => page.getByRole("region", { name: "Map", exact: true });

/**
 * Clicks the campaign map `right` and `down` pixels from its middle (up and left if negative),
 * where it is now (read after anything that scrolls). The map is scrolled to the middle of the
 * screen first: on a phone its middle can be under the tab bar, which would take the click.
 */
export async function clickMap(page: Page, right = 0, down = 0): Promise<void> {
  await campaignMap(page).evaluate((map) => {
    map.scrollIntoView({ block: "center" });
  });
  const box = await campaignMap(page).boundingBox();
  if (!box) throw new Error("The map isn't on the page.");
  await page.mouse.click(box.x + box.width / 2 + right, box.y + box.height / 2 + down);
}

/**
 * Clicks the campaign map `across` and `downward` its width and height from its middle (each
 * -0.5 to 0.5), the same part of the area on any screen.
 */
export async function clickMapPart(page: Page, across: number, downward: number): Promise<void> {
  const box = await campaignMap(page).boundingBox();
  if (!box) throw new Error("The map isn't on the page.");
  await clickMap(page, box.width * across, box.height * downward);
}

/** Clicks the middle of the campaign map. */
export const clickMapCentre = (page: Page) => clickMap(page);

/**
 * Drags across the campaign map with the mouse, from one part of it to another (each -0.5 to
 * 0.5 of its width and height from its middle), once it's in the middle of the screen: otherwise
 * part of it can be under the header, which takes the press.
 */
export async function dragOnMap(
  page: Page,
  from: [number, number],
  to: [number, number],
): Promise<void> {
  await campaignMap(page).evaluate((map) => {
    map.scrollIntoView({ block: "center" });
  });
  const box = await campaignMap(page).boundingBox();
  if (!box) throw new Error("The map isn't on the page.");
  const at = ([across, down]: [number, number]) =>
    [box.x + box.width * (0.5 + across), box.y + box.height * (0.5 + down)] as const;
  await page.mouse.move(...at(from));
  await page.mouse.down();
  await page.mouse.move(...at(to), { steps: 10 });
  await page.mouse.up();
}
