import { scan } from "./support/axe.ts";
import { expect, test } from "./support/fixtures.ts";
import {
  approveAndStartNext,
  holdAndSubmit,
  holdForArmy,
  startedCampaign,
} from "./support/turns.ts";

test("a commander sends an ally a report, which a courier brings the next turn", async ({
  signUp,
}) => {
  test.slow();
  const umpire = await signUp("Ada");
  const bob = await signUp("Bob");
  const cy = await signUp("Cy");
  // Grouchy's wing next to the Guard: close by, so the report arrives next turn.
  const { campaignUrl, campaignId, armyId, allyArmyId } = await startedCampaign(
    umpire,
    bob,
    "Wavre",
    4828,
    { ally: { commander: cy, at: { q: 1, r: 0 } } },
  );

  const page = bob.page;
  await page.goto(`${campaignUrl}/map`);
  const intelligence = page.getByRole("region", { name: "Intelligence" });
  await intelligence.getByRole("button", { name: "Send a report" }).click();
  const dialog = page.getByRole("dialog", { name: "Send a report" });
  await dialog.getByRole("textbox", { name: "Message" }).fill("Come to Ligny by the Wavre road.");
  expect(await scan(page, "send a report")).toEqual([]);
  await dialog.getByRole("button", { name: "Send by courier" }).click();
  await expect(page.getByText("Sent a report to Grouchy's Wing by courier.")).toBeVisible();
  await expect(intelligence.getByRole("list", { name: "Reports sent" })).toContainText(
    "To Grouchy's Wing, turn 1.",
  );

  // Not there yet: the courier is still riding.
  await cy.page.goto(`${campaignUrl}/map`);
  await expect(cy.page.getByRole("region", { name: "Intelligence" })).toContainText(
    "No reports yet.",
  );

  await holdAndSubmit(bob, campaignId, armyId);
  await holdForArmy(umpire, campaignId, allyArmyId ?? "");
  await approveAndStartNext(umpire, campaignId, armyId);

  await cy.page.reload();
  const received = cy.page.getByRole("list", { name: "Reports received" });
  await expect(received).toContainText("From Armée du Nord, sent turn 1, arrived turn 2.");
  await expect(received).toContainText("Come to Ligny by the Wavre road.");
  await received.getByRole("button", { name: "Show their 2 units" }).click();
  await expect(received.getByRole("button", { name: "Hide their units" })).toBeVisible();
  expect(await scan(cy.page, "map, a report")).toEqual([]);
});
