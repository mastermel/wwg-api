import { apiAs, waterlooMap } from "./support/api.ts";
import { scan } from "./support/axe.ts";
import { createCampaign, join, joinLink } from "./support/campaigns.ts";
import { browserOf, libraryFaction } from "./support/library.ts";
import { clickMap, clickMapCentre, dragOnMap } from "./support/map.ts";
import { desktopOnly, expect, test, type User } from "./support/fixtures.ts";

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

test("the Umpire draws the area corner to corner, and sees how many hexes it holds", async ({
  signUp,
  isMobile,
}) => {
  test.skip(isMobile, desktopOnly);
  const umpire = await signUp("Ada");
  await createCampaign(umpire.page, "Wagram 1809");
  const page = umpire.page;
  await page.goto(`${page.url()}/map/settings`);
  await expect(page.getByRole("region", { name: "Map", exact: true })).toBeVisible();
  await page.getByRole("textbox", { name: "Hex size, across the flats" }).fill("45");

  // A tap (or click) on one corner, then on the opposite one: as on a phone.
  await page.getByRole("button", { name: "Draw the area" }).click();
  await expect(page.getByText(/Drag a rectangle on the map/)).toBeVisible();
  await clickMap(page, -60, -40);
  await clickMap(page, 60, 40);

  await expect(page.getByText(/^The outline is the campaign's area/)).toBeVisible();
  await expect(
    page.getByText(/^(About )?[\d,]+ hex(es)? in the area: [\d,]+ across by [\d,]+ down\.$/),
  ).toBeVisible();
  await page.getByRole("button", { name: "Save map settings" }).click();
  await expect(page.getByText("Saved the map settings.")).toBeVisible();
});

test("on a computer, the Umpire drags the area's rectangle", async ({ signUp, isMobile }) => {
  test.skip(isMobile, desktopOnly);
  const umpire = await signUp("Ada");
  await createCampaign(umpire.page, "Aspern 1809");
  const page = umpire.page;
  await page.goto(`${page.url()}/map/settings`);
  await expect(page.getByRole("region", { name: "Map", exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Draw the area" }).click();
  await dragOnMap(page, [-0.2, -0.2], [0.2, 0.2]);

  await expect(page.getByText(/^The outline is the campaign's area/)).toBeVisible();
  await expect(page.getByRole("button", { name: "Draw the area" })).toBeVisible();
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

  // Set up through the API: the map's area, a side's name, and Bob's army with two library units.
  const api = await apiAs(umpire.page);
  await api.put(`/api/campaigns/${campaignId}/map`, waterlooMap);
  const [side] = await api.get<{ id: string }[]>(`/api/campaigns/${campaignId}/sides`);
  await api.put(`/api/sides/${side.id}`, { name: "French Empire" });
  const members = await api.get<{ id: string; firstName: string }[]>(
    `/api/campaigns/${campaignId}/members`,
  );
  const bob = members.find((m) => m.firstName === "Bob");
  const faction = await libraryFaction(browserOf(umpire.page), "French", "France", [
    { name: "Imperial Guard", type: "LineInfantry" },
    { name: "Reserve Artillery", type: "FootArtillery" },
  ]);
  const army = await api.post<{ id: string }>(`/api/campaigns/${campaignId}/armies`, {
    name: "Armée du Nord",
    commanderMemberId: bob?.id ?? null,
    sideId: side.id,
    nation: "France",
    factionIds: [faction.id],
  });
  await api.post(`/api/armies/${army.id}/units`, {
    unitIds: faction.units.map((unit) => unit.id),
  });

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

test("a Player hides map layers, and the map remembers it", async ({ signUp }) => {
  const umpire = await signUp("Ada");
  const player = await signUp("Bob");
  await createCampaign(umpire.page, "Ligny");
  const campaignUrl = umpire.page.url();
  const campaignId = new URL(campaignUrl).pathname.split("/").at(-1) ?? "";
  await join(player.page, await joinLink(umpire.page), "Ligny");
  // The Umpire leaves contours off: they're not on offer.
  await (await apiAs(umpire.page)).put(`/api/campaigns/${campaignId}/map`, waterlooMap);

  const page = player.page;
  await page.goto(`${campaignUrl}/map`);
  await page.getByRole("button", { name: "Map layers" }).click();
  const real = page.getByRole("group", { name: "Real map" });
  await expect(real.getByRole("switch", { name: "Forests" })).toBeChecked();
  await expect(real.getByRole("switch", { name: "Contours" })).toHaveCount(0);
  await expect(page.getByRole("group", { name: "Game map" })).toBeVisible();
  // The panel fades in (a sheet on a phone): a half-shown one fails axe's contrast check.
  await expect(page.getByRole("dialog")).toHaveCSS("opacity", "1");
  expect(await scan(page, "map, layers")).toEqual([]);
  await real.getByRole("switch", { name: "Forests" }).click();
  await expect(real.getByRole("switch", { name: "Forests" })).not.toBeChecked();
  // The game map's own switch hides it whole, its layers kept as they were.
  const game = page.getByRole("group", { name: "Game map" });
  await game.getByRole("switch", { name: "Game map" }).click();
  await expect(game.getByRole("switch", { name: "Grid" })).toBeDisabled();
  await expect(game.getByRole("switch", { name: "Grid" })).toBeChecked();

  await page.reload();
  // The reload keeps the page's scroll, which can leave the button under the header.
  await page.evaluate(() => {
    window.scrollTo(0, 0);
  });
  await page.getByRole("button", { name: "Map layers" }).click();
  await expect(
    page.getByRole("group", { name: "Real map" }).getByRole("switch", { name: "Forests" }),
  ).not.toBeChecked();
  await expect(
    page.getByRole("group", { name: "Game map" }).getByRole("switch", { name: "Game map" }),
  ).not.toBeChecked();
  await page.getByRole("button", { name: "Show everything again" }).click();
  await expect(
    page.getByRole("group", { name: "Real map" }).getByRole("switch", { name: "Forests" }),
  ).toBeChecked();
  await expect(
    page.getByRole("group", { name: "Game map" }).getByRole("switch", { name: "Game map" }),
  ).toBeChecked();
});

/** A campaign whose middle hex has low hills, a walled town and a good road north; the Player on its map. */
async function wavre(signUp: (firstName: string) => Promise<User>) {
  const umpire = await signUp("Ada");
  const player = await signUp("Bob");
  await createCampaign(umpire.page, "Wavre");
  const campaignUrl = umpire.page.url();
  const campaignId = new URL(campaignUrl).pathname.split("/").at(-1) ?? "";
  await join(player.page, await joinLink(umpire.page), "Wavre");
  const api = await apiAs(umpire.page);
  await api.put(`/api/campaigns/${campaignId}/map`, waterlooMap);
  await api.put(`/api/campaigns/${campaignId}/grid/cells/0/0`, {
    terrain: "LowHill",
    forest: false,
    settlement: { size: "Town", walled: true, fortress: false, capital: "None", name: "Wavre" },
  });
  await api.put(`/api/campaigns/${campaignId}/grid/edges/0/0/N`, {
    road: "Good",
    river: false,
    bridge: false,
    waterway: "None",
  });
  await player.page.goto(`${campaignUrl}/map`);
  await expect(player.page.getByRole("region", { name: "Map", exact: true })).toBeVisible();
  return player.page;
}

test("with a mouse, a label follows it over the hexes", async ({ signUp, isMobile }) => {
  test.skip(isMobile, "A phone has no hover.");
  const page = await wavre(signUp);

  const box = await page.getByRole("region", { name: "Map", exact: true }).boundingBox();
  await page.mouse.move(
    (box?.x ?? 0) + (box?.width ?? 0) / 2,
    (box?.y ?? 0) + (box?.height ?? 0) / 2,
  );

  await expect(page.getByRole("tooltip", { name: "Hex (0, 0), Wavre" })).toContainText("Low hills");
});

test("a click or tap on a hex shows everything known of it", async ({ signUp }) => {
  const page = await wavre(signUp);

  await clickMap(page);

  const card = page.getByRole("dialog", { name: "Hex (0, 0), Wavre" });
  await expect(card).toContainText("Wavre: Walled town.");
  await expect(card).toContainText("Good road to the north.");
  expect(await scan(page, "map, a hex's card")).toEqual([]);
});

test("on a computer, the map fills the screen and comes back", async ({ signUp, isMobile }) => {
  test.skip(isMobile, "A phone's map is most of its screen already.");
  const page = await wavre(signUp);
  const map = page.getByRole("region", { name: "Map", exact: true });
  const viewport = page.viewportSize() ?? { width: 0, height: 0 };
  const before = await map.boundingBox();

  await page.getByRole("button", { name: "Full screen" }).click();
  await expect
    .poll(async () => (await map.boundingBox())?.height ?? 0)
    .toBeGreaterThan((before?.height ?? 0) + 100);
  const full = await map.boundingBox();
  expect(full?.width ?? 0).toBeGreaterThan(viewport.width - 40);

  await page.getByRole("button", { name: "Exit full screen" }).click();
  await expect(page.getByRole("button", { name: "Full screen" })).toBeVisible();
  await expect.poll(async () => (await map.boundingBox())?.height).toBe(before?.height);

  // Esc leaves it too.
  await page.getByRole("button", { name: "Full screen" }).click();
  await expect(page.getByRole("button", { name: "Exit full screen" })).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("button", { name: "Full screen" })).toBeVisible();
});
