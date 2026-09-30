import { admin } from "./support/accounts.ts";
import { scan } from "./support/axe.ts";
import { createCampaign } from "./support/campaigns.ts";
import { addFromLibrary, browserOf, chooseFaction, libraryFaction } from "./support/library.ts";
import { expect, test } from "./support/fixtures.ts";

test("an Admin makes a Manager, who builds the library that everyone sees", async ({
  signIn,
  signUp,
  isMobile,
}) => {
  test.slow();
  const manager = await signUp("Mia");
  const player = await signUp("Pat");
  // Every run shares the library: a faction of this test's own.
  const faction = `French ${String(Date.now())}`;

  // Not a Manager yet: the library is there to view, not to change.
  await manager.page.goto("/library");
  await expect(manager.page.getByRole("heading", { level: 1, name: "Library" })).toBeVisible();
  await expect(manager.page.getByRole("button", { name: "New faction" })).toHaveCount(0);

  const adminPage = await signIn(admin.email, admin.password);
  await adminPage
    .getByRole("navigation", { name: isMobile ? "Main tabs" : "Main" })
    .getByRole("link", { name: "Users" })
    .click();
  await adminPage.getByRole("searchbox", { name: "Search" }).fill(manager.email);
  await expect(adminPage).toHaveURL(/search=/);
  await adminPage.getByRole("row").filter({ hasText: manager.email }).getByRole("link").click();
  await adminPage.getByRole("switch", { name: "Manager" }).click();
  await expect(adminPage.getByText("Mia is now a Manager.")).toBeVisible();

  // At once, without signing in again.
  const page = manager.page;
  await page.reload();
  await page.getByRole("button", { name: "New faction" }).click();
  const factionForm = page.getByRole("dialog");
  await factionForm.getByRole("textbox", { name: "Name" }).fill(faction);
  await factionForm.getByRole("combobox", { name: "Nation" }).click();
  await factionForm.getByRole("option", { name: "France" }).click();
  await factionForm.getByRole("button", { name: "Add faction" }).click();
  await expect(page.getByRole("heading", { level: 1, name: faction })).toBeVisible();

  await page.getByRole("button", { name: "Add unit" }).click();
  const unitForm = page.getByRole("dialog");
  await unitForm.getByRole("textbox", { name: "Name" }).fill("Imperial Guard");
  await unitForm.getByRole("combobox", { name: "Type" }).click();
  await unitForm.getByRole("option", { name: "Line Infantry" }).click();
  await unitForm.getByRole("textbox", { name: "Fighting Factor (FF)" }).fill("7");
  await unitForm.getByRole("textbox", { name: "Points" }).fill("40");
  await unitForm.getByRole("button", { name: "Add unit" }).click();
  await expect(page.getByRole("region", { name: "Units" })).toContainText("Imperial Guard");
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(await scan(page, "library faction (Manager)")).toEqual([]);

  // Anyone signed in sees it, and can't change it.
  await player.page.goto("/library");
  await player.page.getByRole("link", { name: faction }).click();
  await expect(player.page.getByRole("region", { name: "Units" })).toContainText("Imperial Guard");
  await expect(player.page.getByRole("button", { name: "Add unit" })).toHaveCount(0);
  await expect(player.page.getByRole("button", { name: "Edit faction" })).toHaveCount(0);
});

test("an Umpire takes a library unit into two campaigns, but only once into each", async ({
  signUp,
}) => {
  test.slow();
  const umpire = await signUp("Uma");
  const page = umpire.page;
  const faction = await libraryFaction(browserOf(page), "French", "France", [
    { name: "Imperial Guard", type: "LineInfantry" },
  ]);

  for (const campaign of ["Austerlitz 1805", "Jena 1806"]) {
    await createCampaign(page, campaign);
    const campaignUrl = page.url();
    for (const army of ["Grande Armée", "Reserve"]) {
      await page.goto(campaignUrl);
      await page.getByRole("button", { name: "New army" }).click();
      await page.getByRole("dialog").getByRole("textbox", { name: "Name" }).fill(army);
      await page.getByRole("dialog").getByRole("button", { name: "Add army" }).click();
      await page.getByRole("link", { name: army }).click();
      await chooseFaction(page, faction.name);
    }

    // The Reserve's page: the Guard joins it, and the Grande Armée can't have it too.
    await addFromLibrary(page, ["Imperial Guard"]);
    await expect(page.getByRole("region", { name: "Units" })).toContainText("Imperial Guard");
    await page.goto(campaignUrl);
    await page.getByRole("link", { name: "Grande Armée" }).click();
    await page.getByRole("button", { name: "Add units" }).click();
    const picker = page.getByRole("dialog", { name: "Add units" });
    await expect(picker.getByRole("checkbox", { name: "Imperial Guard" })).toBeDisabled();
    await expect(picker).toContainText("In Reserve");
    await picker.getByRole("button", { name: "Cancel" }).click();
    await expect(page.getByRole("dialog")).toHaveCount(0);
  }
});
