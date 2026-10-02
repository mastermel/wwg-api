import type { Page } from "@playwright/test";
import { admin } from "./support/accounts.ts";
import { apiAs } from "./support/api.ts";
import { createCampaign, join, joinLink } from "./support/campaigns.ts";
import { scan } from "./support/axe.ts";
import { browserOf, chooseFromList, closeFactionList, libraryFaction } from "./support/library.ts";
import { desktopOnly, expect, test } from "./support/fixtures.ts";

async function visit(page: Page, url: string, heading: string) {
  await page.goto(url);
  await expect(page.getByRole("heading", { level: 1, name: heading })).toBeVisible();
}

/** Visits each page, scanning it: the violations found. */
async function scanPages(page: Page, pages: readonly (readonly [string, string, string])[]) {
  const violations: string[] = [];
  for (const [label, url, heading] of pages) {
    await visit(page, url, heading);
    violations.push(...(await scan(page, label)));
  }
  return violations;
}

// Every page, in both colour schemes, a test for each kind of visitor: one long test passed its
// time on CI's iPhone. The Umpire's and Admins' pages are a computer's (fixtures' desktopOnly).
for (const colorScheme of ["light", "dark"] as const) {
  test.describe(`${colorScheme} mode`, () => {
    test.use({ colorScheme });

    test("the signed-out pages meet WCAG 2.1 AA", async ({ page }) => {
      expect(
        await scanPages(page, [
          ["sign-in", "/sign-in", "Sign in"],
          ["register", "/register", "Create an account"],
        ]),
      ).toEqual([]);
    });

    test("a Player's pages meet WCAG 2.1 AA", async ({ page, signUp }) => {
      test.slow();
      const umpire = await signUp("Ada");
      const player = await signUp("Bob");
      await createCampaign(umpire.page, "The Peninsular War");
      const campaignUrl = umpire.page.url();
      const campaignId = new URL(campaignUrl).pathname.split("/").at(-1) ?? "";
      const link = await joinLink(umpire.page);
      const violations = await scanPages(page, [
        ["join", new URL(link).pathname, "Join a campaign"],
      ]);
      await join(player.page, link, "The Peninsular War");

      // Their army, with units: every section has something in it.
      const api = await apiAs(umpire.page);
      const [side] = await api.get<{ id: string }[]>(`/api/campaigns/${campaignId}/sides`);
      const members = await api.get<{ id: string; firstName: string }[]>(
        `/api/campaigns/${campaignId}/members`,
      );
      const faction = await libraryFaction(browserOf(umpire.page), "British", "Britain", [
        { name: "1st Division", type: "LineInfantry" },
        { name: "Light Division", type: "LightInfantry" },
      ]);
      const army = await api.post<{ id: string }>(`/api/campaigns/${campaignId}/armies`, {
        name: "First Corps",
        commanderMemberId: members.find((m) => m.firstName === player.firstName)?.id ?? null,
        sideId: side.id,
        nation: "Britain",
        factionIds: [faction.id],
      });
      await api.post(`/api/armies/${army.id}/units`, {
        unitIds: faction.units.map((unit) => unit.id),
      });

      violations.push(
        ...(await scanPages(player.page, [
          ["campaigns (Player)", "/campaigns", "Campaigns"],
          ["campaign (Player)", campaignUrl, "The Peninsular War"],
          ["army (commander)", `${campaignUrl}/armies/${army.id}`, "First Corps"],
          ["map (Player, no area yet)", `${campaignUrl}/map`, "Map"],
          ["library (Player)", "/library", "Library"],
          ["library faction (Player)", `/library/${faction.id}`, faction.name],
          ["account", "/account", "Account"],
          ["about", "/about", "About"],
        ])),
      );
      expect(violations).toEqual([]);
    });

    test("the Umpire's pages meet WCAG 2.1 AA", async ({ signUp, isMobile }) => {
      test.skip(isMobile, desktopOnly);
      test.slow();
      const umpire = await signUp("Ada");
      const page = umpire.page;
      await createCampaign(page, "The Peninsular War");
      const campaignUrl = page.url();
      const faction = await libraryFaction(browserOf(page), "British", "Britain", [
        { name: "1st Division", type: "LineInfantry" },
        { name: "Light Division", type: "LightInfantry" },
      ]);

      // The forms, as the Umpire sets up an army.
      const violations: string[] = [];
      await page.reload();
      await page.getByRole("button", { name: "New army" }).click();
      const dialog = page.getByRole("dialog");
      await dialog.getByRole("textbox", { name: "Name" }).fill("First Corps");
      await chooseFromList(dialog.getByRole("combobox", { name: "Factions" }), faction.name);
      await closeFactionList(page);
      violations.push(...(await scan(page, "new army form")));
      await dialog.getByRole("button", { name: "Add army" }).click();
      await page.getByRole("link", { name: "First Corps" }).click();
      const armyUrl = page.url();
      const units = page.getByRole("region", { name: "Units" });
      await units.getByRole("button", { name: "Add units" }).click();
      const picker = page.getByRole("dialog", { name: "Add units" });
      await picker.getByRole("checkbox", { name: "1st Division" }).check();
      violations.push(...(await scan(page, "add units")));
      await picker.getByRole("button", { name: "Add 1 unit" }).click();
      await expect(units).toContainText("1st Division");
      await expect(page.getByRole("dialog")).toHaveCount(0);
      await units.getByRole("button", { name: "Edit 1st Division" }).click();
      violations.push(...(await scan(page, "unit form")));
      await page.getByRole("dialog").getByRole("button", { name: "Cancel" }).click();
      await expect(page.getByRole("dialog")).toHaveCount(0);

      violations.push(
        ...(await scanPages(page, [
          ["campaigns", "/campaigns", "Campaigns"],
          ["campaign", campaignUrl, "The Peninsular War"],
          ["edit campaign", `${campaignUrl}/edit`, "Edit campaign"],
          ["army", armyUrl, "First Corps"],
          ["map (no area yet)", `${campaignUrl}/map`, "Map"],
          ["map settings", `${campaignUrl}/map/settings`, "Map settings"],
          ["terrain (no area yet)", `${campaignUrl}/map/terrain`, "Terrain"],
          ["new campaign", "/campaigns/new", "New campaign"],
          ["library faction", `/library/${faction.id}`, faction.name],
        ])),
      );
      expect(violations).toEqual([]);
    });

    test("the Admins' pages meet WCAG 2.1 AA", async ({ signUp, signIn, isMobile }) => {
      test.skip(isMobile, desktopOnly);
      const user = await signUp("Ada");
      const adminPage = await signIn(admin.email, admin.password);
      const violations = await scanPages(adminPage, [
        ["admin users", "/admin/users", "Users"],
        ["admin campaigns", "/admin/campaigns", "All campaigns"],
      ]);
      await adminPage
        .getByRole("link", { name: "Users", exact: true })
        .filter({ visible: true })
        .click();
      // The campaigns list has a search box too: wait for this page's before typing.
      await expect(adminPage.getByRole("heading", { level: 1, name: "Users" })).toBeVisible();
      // Other runs' users fill the first pages: search for this one.
      await adminPage.getByRole("searchbox", { name: "Search" }).fill(user.email);
      await expect(adminPage).toHaveURL(/search=/);
      await adminPage.getByRole("row").filter({ hasText: user.email }).getByRole("link").click();
      await expect(adminPage.getByRole("heading", { level: 1, name: user.name })).toBeVisible();
      violations.push(...(await scan(adminPage, "admin user")));
      expect(violations).toEqual([]);
    });
  });
}
