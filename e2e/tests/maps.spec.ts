import { createCampaign, join, joinLink } from "./support/campaigns.ts";
import { expect, test } from "./support/fixtures.ts";

// Place search isn't driven here: it calls a geocoding service over the internet (the component
// tests cover it). The Umpire frames the area by the view instead.
test("the Umpire sets the map's area, and a Player sees the map inside it", async ({ signUp }) => {
  test.slow();
  const umpire = await signUp("Ada");
  const player = await signUp("Bob");
  await createCampaign(umpire.page, "The Hundred Days");
  const campaignUrl = umpire.page.url();
  await join(player.page, await joinLink(umpire.page), "The Hundred Days");

  const page = umpire.page;
  await page.getByRole("link", { name: "Map", exact: true }).click();
  await expect(page.getByText("No map yet")).toBeVisible();
  await page.getByRole("link", { name: "Map settings" }).first().click();
  await expect(page.getByRole("heading", { level: 1, name: "Map settings" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Map", exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Use this view" }).click();
  await expect(page.getByText("The outline is the campaign's area.")).toBeVisible();
  // Mantine's switch input lies over its label; its segmented control's input is off-screen, so
  // that one takes its label.
  await page.getByRole("switch", { name: "Forests" }).click();
  await expect(page.getByRole("switch", { name: "Forests" })).not.toBeChecked();
  await page.getByText("Kilometres", { exact: true }).click();
  await expect(page.getByRole("radio", { name: "Kilometres" })).toBeChecked();
  await page.getByRole("textbox", { name: "Light Cavalry" }).fill("45");
  await page.getByRole("button", { name: "Save map settings" }).click();
  await expect(page.getByText("Saved the map settings.")).toBeVisible();
  await expect(page.getByRole("heading", { level: 1, name: "Map" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Map", exact: true })).toBeVisible();

  // Saved: the settings come back as they were left.
  await page.getByRole("link", { name: "Map settings" }).click();
  await expect(page.getByRole("switch", { name: "Forests" })).not.toBeChecked();
  await expect(page.getByRole("textbox", { name: "Light Cavalry" })).toHaveValue("45 km");

  // The Player sees the map, not its settings.
  await player.page.goto(`${campaignUrl}/map`);
  await expect(player.page.getByRole("region", { name: "Map", exact: true })).toBeVisible();
  await expect(player.page.getByRole("link", { name: "Map settings" })).toHaveCount(0);
  await player.page.goto(`${campaignUrl}/map/settings`);
  await expect(player.page.getByText("Only the Umpire can change the map")).toBeVisible();
});
