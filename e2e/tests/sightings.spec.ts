import { apiAs } from "./support/api.ts";
import { scan } from "./support/axe.ts";
import { expect, test } from "./support/fixtures.ts";
import { holdAndSubmit, holdForArmy, startedCampaign } from "./support/turns.ts";

test("the Umpire shapes a sighting as the turn starts, and the commander sees it", async ({
  signUp,
}) => {
  test.slow();
  const umpire = await signUp("Ada");
  const commander = await signUp("Bob");
  // The Prussians' brigade next to the Guard, at (0, 0).
  const { campaignUrl, campaignId, armyId, enemyArmyId } = await startedCampaign(
    umpire,
    commander,
    "Ligny",
    4828,
    { enemyAt: { q: 1, r: 0 } },
  );
  await holdAndSubmit(commander, campaignId, armyId);
  const api = await apiAs(umpire.page);
  const turns = await api.get<{ id: string; open: boolean }[]>(`/api/armies/${armyId}/turns`);
  await api.post(`/api/army-turns/${turns.find((t) => t.open)?.id ?? ""}/approve`, null);
  await holdForArmy(umpire, campaignId, enemyArmyId ?? "");

  const page = umpire.page;
  await page.goto(`${campaignUrl}/map`);
  await page.getByRole("button", { name: "Start turn 2" }).click();
  const dialog = page.getByRole("dialog", { name: "Start turn 2?" });
  const sighting = dialog.getByRole("group", { name: "Armée du Nord sees Hex (1, 0)" });
  await expect(sighting).toContainText("1st Brigade (Line Infantry, 20 points)");
  expect(await scan(page, "start turn, sightings")).toEqual([]);
  await dialog.getByRole("button", { name: "Start turn 2" }).click();
  await expect(page.getByText("Turn 2 has started.")).toBeVisible();

  await commander.page.goto(`${campaignUrl}/map`);
  await expect(commander.page.getByRole("list", { name: "Sightings" })).toContainText(
    "Hex (1, 0): Prussian I Corps: 1 line infantry, a small force.",
  );
  await expect(
    commander.page.getByRole("button", { name: /^Turn 2 \(open\):.*, enemy sighted$/ }),
  ).toBeVisible();
  expect(await scan(commander.page, "map, a sighting")).toEqual([]);
});
