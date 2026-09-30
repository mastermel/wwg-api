import { apiAs, waterlooMap } from "./support/api.ts";
import { scan } from "./support/axe.ts";
import { createCampaign, join, joinLink } from "./support/campaigns.ts";
import { expect, test } from "./support/fixtures.ts";
import { clickMapCentre } from "./support/map.ts";

test("the Umpire sets a hex's terrain and an edge, and a Player can't", async ({ signUp }) => {
  test.slow();
  const umpire = await signUp("Ada");
  const player = await signUp("Bob");
  await createCampaign(umpire.page, "Ligny 1815");
  const campaignUrl = umpire.page.url();
  const campaignId = new URL(campaignUrl).pathname.split("/").at(-1) ?? "";
  await join(player.page, await joinLink(umpire.page), "Ligny 1815");
  await (await apiAs(umpire.page)).put(`/api/campaigns/${campaignId}/map`, waterlooMap);

  const page = umpire.page;
  await page.goto(`${campaignUrl}/map`);
  await page.getByRole("link", { name: "Terrain" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Terrain" })).toBeVisible();
  await expect(page.getByText(/Click a hex on the map/)).toBeVisible();
  await clickMapCentre(page);

  // The hex: low hills and a walled town.
  const hex = page.getByRole("region", { name: /^Hex \(/ });
  await expect(hex).toContainText("Flat, with nothing on it.");
  await hex.getByRole("combobox", { name: "Ground" }).click();
  await hex.getByRole("option", { name: "Low hills" }).click();
  await hex.getByRole("combobox", { name: "Town or city" }).click();
  await hex.getByRole("option", { name: "Town", exact: true }).click();
  await hex.getByRole("switch", { name: "Walled" }).click();
  await hex.getByRole("textbox", { name: "Name" }).fill("Mont-Saint-Jean");
  await hex.getByRole("button", { name: "Save hex" }).click();
  await expect(page.getByText(/^Saved Hex \(/)).toBeVisible();
  await expect(hex).toContainText("Low hills, Walled town: Mont-Saint-Jean");
  await expect(hex.getByText("Set by you")).toBeVisible();

  // Its south edge: a good road over a bridged river.
  const edge = page.getByRole("region", { name: "Edge" });
  await edge.getByRole("combobox", { name: "Side" }).click();
  await edge.getByRole("option", { name: "South", exact: true }).click();
  await edge.getByRole("combobox", { name: "Road across it" }).click();
  await edge.getByRole("option", { name: "Good road" }).click();
  await edge.getByRole("switch", { name: "River along it" }).click();
  await edge.getByRole("switch", { name: "Bridge" }).click();
  await edge.getByRole("button", { name: "Save edge" }).click();
  await expect(page.getByText("Saved the south edge.")).toBeVisible();
  expect(await scan(page, "terrain, a hex chosen")).toEqual([]);

  // Kept: the same hex, chosen again after a reload, has them.
  await page.reload();
  await clickMapCentre(page);
  await expect(hex).toContainText("Low hills, Walled town: Mont-Saint-Jean");
  await edge.getByRole("combobox", { name: "Side" }).click();
  await edge.getByRole("option", { name: "South", exact: true }).click();
  await expect(edge.getByRole("combobox", { name: "Road across it" })).toHaveValue("Good road");
  await expect(edge.getByRole("switch", { name: "Bridge" })).toBeChecked();

  // The Player sees the map, but not the editor.
  await player.page.goto(`${campaignUrl}/map`);
  await expect(player.page.getByRole("region", { name: "Map", exact: true })).toBeVisible();
  await expect(player.page.getByRole("link", { name: "Terrain" })).toHaveCount(0);
  await player.page.goto(`${campaignUrl}/map/terrain`);
  await expect(player.page.getByText("Only the Umpire can change the terrain")).toBeVisible();
});

test("the Umpire infers the terrain from the map's tiles", async ({ signUp }) => {
  const umpire = await signUp("Ada");
  await createCampaign(umpire.page, "Wavre 1815");
  const campaignUrl = umpire.page.url();
  const campaignId = new URL(campaignUrl).pathname.split("/").at(-1) ?? "";
  await (await apiAs(umpire.page)).put(`/api/campaigns/${campaignId}/map`, waterlooMap);
  // Not the internet's tiles (e2e/CLAUDE.md): an empty build of them, so the flow is tested
  // without depending on what they hold. The unit tests cover what's inferred from data.
  const page = umpire.page;
  await page.route("https://tiles.openfreemap.org/planet", (route) =>
    route.fulfill({
      json: { tiles: ["https://tiles.openfreemap.org/empty/{z}/{x}/{y}.pbf"], maxzoom: 14 },
    }),
  );
  await page.route("https://tiles.openfreemap.org/empty/**", (route) =>
    route.fulfill({ status: 404 }),
  );
  await page.route("https://tiles.mapterhorn.com/**", (route) => route.fulfill({ status: 404 }));

  await page.goto(`${campaignUrl}/map/terrain`);
  await page.getByRole("button", { name: "Infer terrain" }).click();

  await expect(
    page.getByText("Inferred the terrain: 0 hexes and 0 edges with something on them."),
  ).toBeVisible();
  expect(await scan(page, "terrain, inferred")).toEqual([]);
});
