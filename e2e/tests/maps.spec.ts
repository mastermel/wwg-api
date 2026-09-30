import { apiAs, waterlooMap } from "./support/api.ts";
import { scan } from "./support/axe.ts";
import { createCampaign, join, joinLink } from "./support/campaigns.ts";
import { clickMapCentre } from "./support/map.ts";
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
  await expect(page.getByText(/^The outline is the campaign's area/)).toBeVisible();
  // Mantine's switch input lies over its label; its segmented control's input is off-screen, so
  // that one takes its label.
  await page.getByRole("switch", { name: "Forests" }).click();
  await expect(page.getByRole("switch", { name: "Forests" })).not.toBeChecked();
  await expect(page.getByRole("switch", { name: "Hex grid" })).toBeChecked();
  await page.getByText("Kilometres", { exact: true }).click();
  await expect(page.getByRole("radio", { name: "Kilometres" })).toBeChecked();
  // Big hexes for the big area the view gives: the grid is drawn over it.
  await page.getByRole("textbox", { name: "Hex size, across the flats" }).fill("45");
  await page.getByRole("button", { name: "Save map settings" }).click();
  await expect(page.getByText("Saved the map settings.")).toBeVisible();
  await expect(page.getByRole("heading", { level: 1, name: "Map" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Map", exact: true })).toBeVisible();

  // Saved: the settings come back as they were left.
  await page.getByRole("link", { name: "Map settings" }).click();
  await expect(page.getByRole("switch", { name: "Forests" })).not.toBeChecked();
  await expect(page.getByRole("textbox", { name: "Hex size, across the flats" })).toHaveValue(
    "45 km",
  );

  // The Player sees the map, not its settings.
  await player.page.goto(`${campaignUrl}/map`);
  await expect(player.page.getByRole("region", { name: "Map", exact: true })).toBeVisible();
  await expect(player.page.getByRole("link", { name: "Map settings" })).toHaveCount(0);
  await player.page.goto(`${campaignUrl}/map/settings`);
  await expect(player.page.getByText("Only the Umpire can change the map")).toBeVisible();
});

test("the Umpire places the units, stacking two, and starts the campaign", async ({ signUp }) => {
  test.slow();
  const umpire = await signUp("Ada");
  const commander = await signUp("Bob");
  const other = await signUp("Cai");
  await createCampaign(umpire.page, "Waterloo 1815");
  const campaignUrl = umpire.page.url();
  const campaignId = new URL(campaignUrl).pathname.split("/").at(-1) ?? "";
  const link = await joinLink(umpire.page);
  await join(commander.page, link, "Waterloo 1815");
  await join(other.page, link, "Waterloo 1815");

  // Set up through the API: the map's area, a faction, and Bob's army with two units.
  const api = await apiAs(umpire.page);
  await api.put(`/api/campaigns/${campaignId}/map`, waterlooMap);
  const faction = await api.post<{ id: string }>(`/api/campaigns/${campaignId}/factions`, {
    name: "French Empire",
  });
  const members = await api.get<{ id: string; firstName: string }[]>(
    `/api/campaigns/${campaignId}/members`,
  );
  const bob = members.find((m) => m.firstName === "Bob");
  const army = await api.post<{ id: string }>(`/api/campaigns/${campaignId}/armies`, {
    name: "Armée du Nord",
    commanderMemberId: bob?.id ?? null,
    factionId: faction.id,
    nation: "France",
  });
  for (const [name, type] of [
    ["Imperial Guard", "LineInfantry"],
    ["Reserve Artillery", "FootArtillery"],
  ]) {
    await api.post(`/api/armies/${army.id}/units`, { name, type, fightingFactor: 6, points: 30 });
  }

  // The Umpire places one unit on the map, and the other on top of it.
  const page = umpire.page;
  await page.goto(`${campaignUrl}/map`);
  await expect(page.getByText("Place 2 units on the map.")).toBeVisible();
  const map = page.getByRole("region", { name: "Map", exact: true });
  await expect(map).toBeVisible();
  await page.getByRole("button", { name: "Place Imperial Guard" }).click();
  // On a phone the list is under the map, which scrolls back into view: find it after that.
  await expect(page.getByText(/Click the map where/)).toBeInViewport();
  await clickMapCentre(page);
  await expect(page.getByText("Placed Imperial Guard.")).toBeVisible();
  await page.getByRole("button", { name: "Place Reserve Artillery" }).click();
  await page.getByRole("button", { name: "Imperial Guard, Line Infantry, Armée du Nord" }).click();
  await expect(page.getByText("Placed Reserve Artillery.")).toBeVisible();

  const stack = page.getByRole("button", { name: "2 units: Imperial Guard, Reserve Artillery" });
  await expect(stack).toBeVisible();
  expect(await scan(page, "map, setting up")).toEqual([]);
  await stack.click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: /^Reserve Artillery/ })
    .click();
  await expect(page.getByRole("dialog", { name: "Reserve Artillery" })).toContainText(
    "Foot Artillery",
  );
  await page.keyboard.press("Escape");

  await page.getByRole("button", { name: "Start campaign" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Start campaign" }).click();
  await expect(page.getByText("The campaign has started: turn 1 is open.")).toBeVisible();
  await expect(page.getByRole("heading", { level: 2, name: "Turn 1" })).toBeVisible();

  // Bob sees his army's units; Cai, in no army, sees none.
  await commander.page.goto(`${campaignUrl}/map`);
  await expect(
    commander.page.getByRole("button", { name: "2 units: Imperial Guard, Reserve Artillery" }),
  ).toBeVisible();
  await other.page.goto(`${campaignUrl}/map`);
  await expect(other.page.getByRole("heading", { level: 2, name: "Turn 1" })).toBeVisible();
  await expect(other.page.getByRole("button", { name: /Imperial Guard/ })).toHaveCount(0);
});

test("on a phone, one finger scrolls the page past the map", async ({ signUp, isMobile }) => {
  test.skip(!isMobile, "Two-finger panning is for touch screens only.");
  const umpire = await signUp("Ada");
  await createCampaign(umpire.page, "Ligny 1815");
  const campaignId = new URL(umpire.page.url()).pathname.split("/").at(-1) ?? "";
  await (await apiAs(umpire.page)).put(`/api/campaigns/${campaignId}/map`, waterlooMap);

  await umpire.page.goto(`/campaigns/${campaignId}/map`);

  // The map leaves one-finger swipes to the page (it scrolls), and pans with two.
  await expect(umpire.page.locator(".maplibregl-canvas")).toHaveCSS("touch-action", "pan-x pan-y");
});
