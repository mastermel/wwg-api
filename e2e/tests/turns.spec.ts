import { scan } from "./support/axe.ts";
import { expect, test } from "./support/fixtures.ts";
import { latestEmailText } from "./support/mailpit.ts";
import { apiAs } from "./support/api.ts";
import { clickMap } from "./support/map.ts";
import { approveAndStartNext, holdAndSubmit, startedCampaign } from "./support/turns.ts";

test("a commander moves one unit, holds another and submits the turn", async ({ signUp }) => {
  test.slow();
  const umpire = await signUp("Ada");
  const commander = await signUp("Bob");
  const { campaignUrl } = await startedCampaign(umpire, commander, "Ligny 1815", 5000);

  const page = commander.page;
  await page.goto(`${campaignUrl}/map`);
  const panel = page.getByRole("region", { name: "Turn 1" });
  await expect(panel.getByRole("button", { name: "Submit turn 1" })).toBeDisabled();

  // Move: choose the unit on the map, then Move, then a point inside its range, then Confirm.
  await page.getByRole("button", { name: "Imperial Guard, Heavy Infantry, Armée du Nord" }).click();
  await page
    .getByRole("dialog", { name: "Imperial Guard" })
    .getByRole("button", { name: "Move" })
    .click();
  await expect(page.getByText(/Tap the map inside the circle/)).toBeInViewport();
  // The drawer's overlay fades out: wait, or the click lands on it.
  await expect(page.getByRole("dialog")).toHaveCount(0);
  // The unit is in the middle of the map: a little to the right of it is well inside its range.
  await clickMap(page, 60, -30);
  await expect(page.getByText(/to here\?/)).toBeVisible();
  await page.getByRole("button", { name: "Confirm" }).click();
  await expect(page.getByText("Imperial Guard will move.")).toBeVisible();
  await expect(panel.getByText(/^Moves \d+(\.\d)? km$/)).toBeVisible();

  // Hold: from the turn panel's list, the other way in.
  await panel.getByRole("button", { name: "Reserve Artillery" }).click();
  await page
    .getByRole("dialog", { name: "Reserve Artillery" })
    .getByRole("button", { name: "Hold" })
    .click();
  await expect(page.getByText("Reserve Artillery will hold.")).toBeVisible();
  await expect(panel.getByText("Holds")).toBeVisible();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(await scan(page, "map, a commander's draft")).toEqual([]);

  await panel.getByRole("button", { name: "Submit turn 1" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Submit" }).click();
  await expect(page.getByText("Submitted Armée du Nord's turn 1.")).toBeVisible();
  await expect(panel.getByText("Submitted: the Umpire reviews it next.")).toBeVisible();
  await expect(panel.getByRole("button", { name: /Undo/ })).toHaveCount(0);

  // The Umpire hears of it.
  expect(await latestEmailText(umpire.email)).toContain(
    "Bob Tester submitted Armée du Nord's orders for turn 1",
  );
});

test("the Umpire sends a turn back, approves it resubmitted, and starts the next", async ({
  signUp,
}) => {
  test.slow();
  const umpire = await signUp("Ada");
  const commander = await signUp("Bob");
  const { campaignUrl, campaignId, armyId } = await startedCampaign(
    umpire,
    commander,
    "Wavre 1815",
  );
  await holdAndSubmit(commander, campaignId, armyId);

  // The Umpire sends it back, with a note on the turn and one on a unit.
  const page = umpire.page;
  await page.goto(`${campaignUrl}/map`);
  const panel = page.getByRole("region", { name: "Turn 1" });
  await expect(panel.getByText(/^0 moves, 2 holds\. Submitted/)).toBeVisible();
  expect(await scan(page, "map, the Umpire's review")).toEqual([]);
  await panel.getByRole("button", { name: "Send back Armée du Nord's turn" }).click();
  const dialog = page.getByRole("dialog", { name: "Send back Armée du Nord's turn 1" });
  await dialog.getByRole("textbox", { name: "Note" }).fill("Push on to Ligny.");
  await dialog.getByRole("textbox", { name: "Imperial Guard" }).fill("Take the ridge.");
  await dialog.getByRole("button", { name: "Send back" }).click();
  await expect(page.getByText("Sent back Armée du Nord's turn 1.")).toBeVisible();
  await expect(panel.getByText("Bob Tester is giving orders.")).toBeVisible();

  // Bob hears why, sees the notes, and submits again.
  await expect
    .poll(() => latestEmailText(commander.email), { timeout: 15_000 })
    .toContain("Push on to Ligny.");
  const bob = commander.page;
  await bob.goto(`${campaignUrl}/map`);
  const bobsPanel = bob.getByRole("region", { name: "Turn 1" });
  await expect(bobsPanel.getByRole("status", { name: "Sent back" })).toContainText(
    "Push on to Ligny.",
  );
  await expect(bobsPanel.getByText("Umpire: Take the ridge.")).toBeVisible();
  await bobsPanel.getByRole("button", { name: "Submit turn 1" }).click();
  await bob.getByRole("dialog").getByRole("button", { name: "Submit" }).click();
  await expect(bob.getByText("Submitted Armée du Nord's turn 1.")).toBeVisible();

  // The Umpire approves it, and starts turn 2.
  await page.reload();
  await panel.getByRole("button", { name: "Approve Armée du Nord's turn" }).click();
  await expect(page.getByText("Approved Armée du Nord's turn 1.")).toBeVisible();
  await panel.getByRole("button", { name: "Start turn 2" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Start turn 2" }).click();
  await expect(page.getByText("Turn 2 has started.")).toBeVisible();
  await expect(page.getByRole("region", { name: "Turn 2" })).toBeVisible();

  // Bob is told, and gives orders for turn 2.
  await expect
    .poll(() => latestEmailText(commander.email), { timeout: 15_000 })
    .toContain("Turn 2 of Wavre 1815 has started.");
  await bob.reload();
  await expect(bob.getByRole("region", { name: "Turn 2" }).getByText("No order yet")).toHaveCount(
    2,
  );
});

test("a commander steps back through the turns; the Umpire picks out an army", async ({
  signUp,
}) => {
  test.slow();
  const umpire = await signUp("Ada");
  const commander = await signUp("Bob");
  const { campaignUrl, campaignId, armyId } = await startedCampaign(
    umpire,
    commander,
    "Quatre Bras",
  );
  // Turn 1: the Imperial Guard marches north (about 5.5 km), and the turn is approved.
  await holdAndSubmit(commander, campaignId, armyId, {
    "Imperial Guard": { latitude: 50.75, longitude: 4.4 },
  });
  await approveAndStartNext(umpire, campaignId, armyId);
  // A second army joins, with a unit of its own on the map.
  const api = await apiAs(umpire.page);
  const prussians = await api.post<{ id: string }>(`/api/campaigns/${campaignId}/armies`, {
    name: "Prussian I Corps",
    commanderMemberId: null,
    nation: "Prussia",
  });
  const brigade = await api.post<{ id: string }>(`/api/armies/${prussians.id}/units`, {
    name: "1st Brigade",
    type: "LightInfantry",
    fightingFactor: 4,
    points: 20,
  });
  await api.put(`/api/units/${brigade.id}/placement`, { latitude: 50.66, longitude: 4.52 });

  // Bob steps back to the setup, and forward to turn 1: the Guard is further north after it.
  const page = commander.page;
  await page.goto(`${campaignUrl}/map`);
  const guard = page.getByRole("button", { name: "Imperial Guard, Heavy Infantry, Armée du Nord" });
  const turns = page.getByRole("list", { name: "Turns" });
  await turns.getByRole("button", { name: "Turn 0: Setup" }).click();
  await expect(page.getByText("Showing where the Umpire placed the units.")).toBeVisible();
  await expect(guard).toBeVisible();
  const atSetup = await guard.boundingBox();
  expect(await scan(page, "map, a past turn")).toEqual([]);
  await page.keyboard.press("ArrowUp");
  await expect(page.getByText("Showing where the units were after turn 1.")).toBeVisible();
  await expect(turns.getByRole("button", { name: /^Turn 1:/ })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  await expect
    .poll(async () => (await guard.boundingBox())?.y ?? 0)
    .toBeLessThan((atSetup?.y ?? 0) - 20);
  await page.getByRole("button", { name: "Back to now" }).click();
  await expect(page.getByRole("region", { name: "Turn 2" })).toBeVisible();

  // The Umpire picks out Bob's army: the Prussians' unit fades.
  await umpire.page.goto(`${campaignUrl}/map`);
  const brigadeMarker = umpire.page.getByRole("button", {
    name: "1st Brigade, Light Infantry, Prussian I Corps",
  });
  await expect(brigadeMarker).toHaveCSS("opacity", "1");
  await umpire.page.getByRole("button", { name: /^Armée du Nord:/ }).click();
  await expect(brigadeMarker).toHaveCSS("opacity", "0.3");
  await expect(
    umpire.page.getByRole("button", { name: "Imperial Guard, Heavy Infantry, Armée du Nord" }),
  ).toHaveCSS("opacity", "1");
  expect(await scan(umpire.page, "map, an army picked out")).toEqual([]);
});
