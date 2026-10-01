import { scan } from "./support/axe.ts";
import { join, joinLink } from "./support/campaigns.ts";
import { expect, test } from "./support/fixtures.ts";
import { clickMap } from "./support/map.ts";
import { startedCampaign } from "./support/turns.ts";

test("the Umpire places a depot, which only its army's commander sees", async ({ signUp }) => {
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
