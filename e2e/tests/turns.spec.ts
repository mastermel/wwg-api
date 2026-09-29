import { scan } from "./support/axe.ts";
import { expect, test } from "./support/fixtures.ts";
import { latestEmailText } from "./support/mailpit.ts";
import { clickMap } from "./support/map.ts";
import { startedCampaign } from "./support/turns.ts";

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
