import { scan } from "./support/axe.ts";
import { join, joinLink } from "./support/campaigns.ts";
import { expect, test } from "./support/fixtures.ts";
import { clickMap } from "./support/map.ts";
import { startedCampaign } from "./support/turns.ts";
import { apiAs } from "./support/api.ts";

test("the Umpire places a depot, which only its army's commander sees", async ({ signUp }) => {
  // Three people, two map pages: past 30s on CI's iPhone.
  test.slow();
  const umpire = await signUp("Ada");
  const commander = await signUp("Bob");
  const other = await signUp("Cy");
  const { campaignUrl } = await startedCampaign(umpire, commander, "Charleroi");
  await join(other.page, await joinLink(umpire.page), "Charleroi");

  const page = umpire.page;
  await page.goto(`${campaignUrl}/map`);
  const depots = page.getByRole("region", { name: "Depots" });
  await depots.getByRole("button", { name: "Add depot" }).click();
  const dialog = page.getByRole("dialog", { name: "Add a depot" });
  await dialog.getByRole("textbox", { name: "Name" }).fill("Fleurus");
  expect(await scan(page, "add a depot")).toEqual([]);
  await dialog.getByRole("button", { name: "Place it on the map" }).click();
  await expect(page.getByText(/Click the map where Fleurus goes/)).toBeVisible();
  await clickMap(page);
  await expect(page.getByText("Placed Fleurus.")).toBeVisible();
  const list = depots.getByRole("list", { name: "Armée du Nord's depots" });
  await expect(list).toContainText("Fleurus · Main depot");
  expect(await scan(page, "map, depots")).toEqual([]);

  await commander.page.goto(`${campaignUrl}/map`);
  await expect(commander.page.getByRole("list", { name: "Armée du Nord's depots" })).toContainText(
    "Fleurus",
  );
  await other.page.goto(`${campaignUrl}/map`);
  await expect(other.page.getByRole("heading", { level: 1, name: "Map" })).toBeVisible();
  await expect(other.page.getByRole("region", { name: "Depots" })).toHaveCount(0);
});

test("a commander has a French unit live off the land", async ({ signUp }) => {
  const umpire = await signUp("Ada");
  const commander = await signUp("Bob");
  const { campaignUrl } = await startedCampaign(umpire, commander, "Ligny");

  const page = commander.page;
  await page.goto(`${campaignUrl}/map`);
  await page.getByRole("button", { name: "Imperial Guard, Line Infantry, Armée du Nord" }).click();
  const drawer = page.getByRole("dialog", { name: "Imperial Guard" });
  await drawer.getByRole("switch", { name: /Living off the land/ }).click();
  await expect(page.getByText("Imperial Guard will live off the land.")).toBeVisible();
  await expect(drawer).toContainText("Turn 1: Holds, living off the land");
});

test("a commander sees which units are out of supply, and how long", async ({ signUp }) => {
  const umpire = await signUp("Ada");
  const commander = await signUp("Bob");
  const { campaignUrl, armyId } = await startedCampaign(umpire, commander, "Wavre");
  // A depot far from the Guard, with no road between: it's out of supply.
  const api = await apiAs(umpire.page);
  await api.post(`/api/armies/${armyId}/depots`, { kind: "Main", name: "Namur", q: 3, r: -1 });

  const page = commander.page;
  await page.goto(`${campaignUrl}/map`);
  await expect(page.getByRole("list", { name: "Supply" })).toContainText(
    "Imperial Guard: out of supply after this turn (1 of 6 turns before attrition).",
  );
  const guard = page.getByRole("button", {
    name: "Imperial Guard, Line Infantry, Armée du Nord, out of supply",
  });
  await guard.click();
  await expect(page.getByRole("dialog", { name: "Imperial Guard" })).toContainText(
    "Out of supply; still after this turn's orders.",
  );
  expect(await scan(page, "map, out of supply")).toEqual([]);
});
