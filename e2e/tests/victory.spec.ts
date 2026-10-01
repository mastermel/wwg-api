import { apiAs } from "./support/api.ts";
import { scan } from "./support/axe.ts";
import { expect, test } from "./support/fixtures.ts";
import {
  approveAndStartNext,
  holdAndSubmit,
  holdForArmy,
  startedCampaign,
} from "./support/turns.ts";

test("the enemy takes a town as the turn closes, and the scoreboard shows it", async ({
  signUp,
}) => {
  test.slow();
  const umpire = await signUp("Ada");
  const bob = await signUp("Bob");
  // The Prussians' brigade in Ligny, which the Umpire gave Bob's army.
  const { campaignUrl, campaignId, armyId, enemyArmyId } = await startedCampaign(
    umpire,
    bob,
    "Ligny",
    4828,
    { enemyAt: { q: 1, r: 0 } },
  );
  const api = await apiAs(umpire.page);
  await api.put(`/api/campaigns/${campaignId}/grid/cells/1/0`, {
    terrain: "Flat",
    forest: false,
    settlement: { size: "Town", walled: false, fortress: false, capital: "None", name: "Ligny" },
  });
  await api.put(`/api/campaigns/${campaignId}/holdings/1/0`, { armyId });

  await holdAndSubmit(bob, campaignId, armyId);
  await holdForArmy(umpire, campaignId, enemyArmyId ?? "");
  await approveAndStartNext(umpire, campaignId, armyId);

  const page = bob.page;
  await page.goto(campaignUrl);
  const score = page.getByRole("region", { name: "Victory points" });
  const sides = score.getByRole("table", { name: "Victory points by side" });
  await expect(sides.getByRole("row", { name: /French Empire/ })).toContainText("0 points");
  await expect(sides.getByRole("row", { name: /Side 2/ })).toContainText("10 points");
  await score.getByRole("button", { name: "History" }).click();
  await expect(score.getByRole("list", { name: "Changes of hands" })).toContainText(
    "Turn 1: Ligny (10 points) taken by Prussian I Corps from Armée du Nord.",
  );
  expect(await scan(page, "campaign, victory points")).toEqual([]);
});
