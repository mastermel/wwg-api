import { scan } from "./support/axe.ts";
import { createCampaign } from "./support/campaigns.ts";
import { chooseFromList } from "./support/library.ts";
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

  // The sidebar on the desktop, the tab bar on a phone: whichever is showing (the page's back
  // link has the same name).
  await page
    .getByRole("navigation")
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
    .getByRole("navigation")
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

test("the Umpire sets the calendar, and the turns are labelled with their days", async ({
  signUp,
}) => {
  const umpire = await signUp("Ada");
  await createCampaign(umpire.page, "Waterloo 1815");
  const page = umpire.page;
  await page.getByRole("link", { name: "Edit", exact: true }).click();

  const calendar = page.getByRole("region", { name: "Calendar" });
  await calendar.getByLabel(/The first turn's day/).fill("1815-06-18");
  await calendar.getByText("Afternoon (14:00–22:00)").click();
  await calendar.getByRole("button", { name: "Save calendar" }).click();
  await expect(page.getByText("Saved the calendar.")).toBeVisible();
  expect(await scan(page, "edit campaign, the calendar")).toEqual([]);

  // Kept: it comes back as it was left.
  await page.reload();
  await expect(calendar.getByLabel(/The first turn's day/)).toHaveValue("1815-06-18");
  await expect(calendar.getByRole("radio", { name: "Afternoon (14:00–22:00)" })).toBeChecked();
});

test("the Umpire sets the concentration limits, and which unit types count", async ({ signUp }) => {
  const umpire = await signUp("Ada");
  await createCampaign(umpire.page, "Waterloo 1815");
  const page = umpire.page;
  await page.getByRole("link", { name: "Edit", exact: true }).click();

  const concentration = page.getByRole("region", { name: "Concentration" });
  await expect(concentration).toContainText("Supply Train, Siege Artillery, Boat.");
  await concentration.getByRole("textbox", { name: /Cavalry limit/ }).fill("120");
  // Supply trains count as infantry here; then they're no longer free.
  await chooseFromList(
    concentration.getByRole("combobox", { name: "Counted as infantry" }),
    "Supply Train",
  );
  await page.keyboard.press("Escape");
  await expect(concentration).toContainText(
    "Free (counted towards neither): Siege Artillery, Boat.",
  );
  await concentration.getByRole("button", { name: "Save concentration" }).click();
  await expect(page.getByText("Saved the concentration settings.")).toBeVisible();
  expect(await scan(page, "edit campaign, concentration")).toEqual([]);

  // Kept: it comes back as it was left.
  await page.reload();
  await expect(concentration.getByRole("textbox", { name: /Cavalry limit/ })).toHaveValue("120");
  await expect(concentration).toContainText(
    "Free (counted towards neither): Siege Artillery, Boat.",
  );
});

test("the Umpire sets the supply reach, kept for next time", async ({ signUp }) => {
  const umpire = await signUp("Ada");
  await createCampaign(umpire.page, "Waterloo 1815");
  const page = umpire.page;
  await page.getByRole("link", { name: "Edit", exact: true }).click();

  const supply = page.getByRole("region", { name: "Supply" });
  await supply.getByRole("textbox", { name: /Supply reach/ }).fill("2");
  await supply.getByRole("button", { name: "Save supply" }).click();
  await expect(page.getByText("Saved the supply settings.")).toBeVisible();

  await page.reload();
  await expect(supply.getByRole("textbox", { name: /Supply reach/ })).toHaveValue("2");
});
