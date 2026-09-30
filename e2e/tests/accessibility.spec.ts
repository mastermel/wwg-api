import type { Page } from "@playwright/test";
import { admin } from "./support/accounts.ts";
import { createCampaign, join, joinLink } from "./support/campaigns.ts";
import { scan } from "./support/axe.ts";
import { browserOf, closeFactionList, libraryFaction } from "./support/library.ts";
import { expect, test } from "./support/fixtures.ts";

async function visit(page: Page, url: string, heading: string) {
  await page.goto(url);
  await expect(page.getByRole("heading", { level: 1, name: heading })).toBeVisible();
}

for (const colorScheme of ["light", "dark"] as const) {
  test.describe(`${colorScheme} mode`, () => {
    test.use({ colorScheme });

    test("every page meets WCAG 2.1 AA", async ({ page, signUp, signIn }) => {
      // Many pages, three people: longer than the default time, especially beside other tests.
      test.slow();
      const violations: string[] = [];

      // Signed out.
      await visit(page, "/sign-in", "Sign in");
      violations.push(...(await scan(page, "sign-in")));
      await visit(page, "/register", "Create an account");
      violations.push(...(await scan(page, "register")));

      // A campaign with a Player, an army with units: every section has something in it.
      const umpire = await signUp("Ada");
      const player = await signUp("Bob");
      await createCampaign(umpire.page, "The Peninsular War");
      const campaignUrl = umpire.page.url();
      const link = await joinLink(umpire.page);
      await visit(page, new URL(link).pathname, "Join a campaign");
      violations.push(...(await scan(page, "join")));
      await join(player.page, link, "The Peninsular War");
      const faction = await libraryFaction(browserOf(umpire.page), "British", "Britain", [
        { name: "1st Division", type: "LineInfantry" },
        { name: "Light Division", type: "LightInfantry" },
      ]);
      await umpire.page.reload();
      await umpire.page.getByRole("button", { name: "New army" }).click();
      const dialog = umpire.page.getByRole("dialog");
      await dialog.getByRole("textbox", { name: "Name" }).fill("First Corps");
      await dialog.getByRole("combobox", { name: "Commander" }).click();
      await dialog.getByRole("option", { name: player.name }).click();
      await dialog.getByRole("combobox", { name: "Factions" }).fill(faction.name);
      await dialog.getByRole("option", { name: faction.name }).click();
      await closeFactionList(umpire.page);
      violations.push(...(await scan(umpire.page, "new army form")));
      await dialog.getByRole("button", { name: "Add army" }).click();
      await umpire.page.getByRole("link", { name: "First Corps" }).click();
      const armyUrl = umpire.page.url();
      const units = umpire.page.getByRole("region", { name: "Units" });
      await units.getByRole("button", { name: "Add units" }).click();
      const picker = umpire.page.getByRole("dialog", { name: "Add units" });
      await picker.getByRole("checkbox", { name: "1st Division" }).check();
      violations.push(...(await scan(umpire.page, "add units")));
      await picker.getByRole("button", { name: "Add 1 unit" }).click();
      await expect(units).toContainText("1st Division");
      await expect(umpire.page.getByRole("dialog")).toHaveCount(0);
      await units.getByRole("button", { name: "Edit 1st Division" }).click();
      violations.push(...(await scan(umpire.page, "unit form")));
      await umpire.page.getByRole("dialog").getByRole("button", { name: "Cancel" }).click();
      await expect(umpire.page.getByRole("dialog")).toHaveCount(0);

      // The Umpire's pages.
      for (const [label, url, heading] of [
        ["campaigns", "/campaigns", "Campaigns"],
        ["campaign", campaignUrl, "The Peninsular War"],
        ["army", armyUrl, "First Corps"],
        ["map (no area yet)", `${campaignUrl}/map`, "Map"],
        ["map settings", `${campaignUrl}/map/settings`, "Map settings"],
        ["terrain (no area yet)", `${campaignUrl}/map/terrain`, "Terrain"],
        ["new campaign", "/campaigns/new", "New campaign"],
        ["library", "/library", "Library"],
        ["library faction", `/library/${faction.id}`, faction.name],
        ["account", "/account", "Account"],
        ["about", "/about", "About"],
      ] as const) {
        await visit(umpire.page, url, heading);
        violations.push(...(await scan(umpire.page, label)));
      }

      // The Player's view of the campaign and army (fewer controls, their own army).
      await visit(player.page, campaignUrl, "The Peninsular War");
      violations.push(...(await scan(player.page, "campaign (Player)")));
      await visit(player.page, armyUrl, "First Corps");
      violations.push(...(await scan(player.page, "army (commander)")));

      // Admin screens.
      const adminPage = await signIn(admin.email, admin.password);
      for (const [label, url, heading] of [
        ["admin users", "/admin/users", "Users"],
        ["admin campaigns", "/admin/campaigns", "All campaigns"],
      ] as const) {
        await visit(adminPage, url, heading);
        violations.push(...(await scan(adminPage, label)));
      }
      await adminPage
        .getByRole("link", { name: "Users", exact: true })
        .filter({ visible: true })
        .click();
      // Other runs' users fill the first pages: search for this one.
      await adminPage.getByRole("searchbox", { name: "Search" }).fill(umpire.email);
      await expect(adminPage).toHaveURL(/search=/);
      await adminPage.getByRole("row").filter({ hasText: umpire.email }).getByRole("link").click();
      await expect(adminPage.getByRole("heading", { level: 1, name: umpire.name })).toBeVisible();
      violations.push(...(await scan(adminPage, "admin user")));

      expect(violations).toEqual([]);
    });
  });
}
