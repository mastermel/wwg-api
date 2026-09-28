import { createCampaign } from "./support/campaigns.ts";
import { expect, test } from "./support/fixtures.ts";
import { waitForServiceWorker, waitUntilSaved } from "./support/offline.ts";

test("creates, edits and deletes a campaign", async ({ signUp }) => {
  const { page } = await signUp("Ada");
  await expect(page.getByText("You're not in any campaigns yet.")).toBeVisible();

  await createCampaign(page, "The Peninsular War");
  await expect(page.getByText("You're the Umpire")).toBeVisible();

  await page.getByRole("link", { name: "Edit" }).click();
  await page.getByRole("textbox", { name: "Name" }).fill("The Hundred Days");
  await page.getByRole("textbox", { name: "Description" }).fill("Napoleon's return, 1815.");
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "The Hundred Days" })).toBeVisible();
  await expect(page.getByText("Napoleon's return, 1815.")).toBeVisible();

  // The sidebar on the desktop, the tab bar on a phone: whichever is showing.
  await page
    .getByRole("link", { name: "Campaigns", exact: true })
    .filter({ visible: true })
    .click();
  await expect(page.getByRole("link", { name: "The Hundred Days" })).toBeVisible();

  await page.getByRole("link", { name: "The Hundred Days" }).click();
  await page.getByRole("button", { name: "Delete campaign" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Delete campaign" }).click();
  await expect(page.getByText("You're not in any campaigns yet.")).toBeVisible();
});

test("shows saved campaigns offline, read-only", async ({ signUp, browserName }) => {
  // Playwright supports service workers in Chromium only; in its WebKit the worker can stay
  // "installing" forever. Offline on iOS Safari is checked by hand (DESIGN.md §3.8).
  test.skip(browserName !== "chromium", "service workers need Chromium in Playwright");
  const { page } = await signUp("Ada");
  await createCampaign(page, "The Peninsular War");
  const details = page.url();
  await waitForServiceWorker(page);
  await waitUntilSaved(page, new URL(details).pathname.replace(/^/, "/api"), "The Peninsular War");
  await page
    .getByRole("link", { name: "Campaigns", exact: true })
    .filter({ visible: true })
    .click();
  await expect(page.getByRole("link", { name: "The Peninsular War" })).toBeVisible();
  await waitUntilSaved(page, "/api/campaigns", "The Peninsular War");

  await page.context().setOffline(true);
  await page.reload();

  await expect(page.getByText("You're offline")).toBeVisible();
  await expect(page.getByRole("link", { name: "The Peninsular War" })).toBeVisible();
  await page.getByRole("link", { name: "The Peninsular War" }).click();
  await expect(page).toHaveURL(details);
  await expect(page.getByRole("button", { name: "Delete campaign" })).toBeDisabled();

  await page.context().setOffline(false);
  await expect(page.getByText("You're offline")).toBeHidden();
  await expect(page.getByRole("button", { name: "Delete campaign" })).toBeEnabled();
});
