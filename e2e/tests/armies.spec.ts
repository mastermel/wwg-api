import { createCampaign, join, joinLink } from "./support/campaigns.ts";
import { expect, test, type User } from "./support/fixtures.ts";

async function addArmy(umpire: User, name: string, commander?: User) {
  const page = umpire.page;
  await page.getByRole("button", { name: "New army" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("textbox", { name: "Name" }).fill(name);
  if (commander) {
    await dialog.getByRole("combobox", { name: "Commander" }).click();
    await dialog.getByRole("option", { name: commander.name }).click();
  }
  await dialog.getByRole("button", { name: "Add army" }).click();
  await expect(page.getByText(`Added ${name}.`)).toBeVisible();
}

test("armies and units are seen only by those who should", async ({ signUp }) => {
  const umpire = await signUp("Ada");
  const commander = await signUp("Arthur");
  const other = await signUp("Bea");
  await createCampaign(umpire.page, "The Peninsular War");
  const campaignUrl = umpire.page.url();
  const link = await joinLink(umpire.page);
  await join(commander.page, link, "The Peninsular War");
  await join(other.page, link, "The Peninsular War");

  await umpire.page.reload();
  await addArmy(umpire, "First Corps", commander);
  await addArmy(umpire, "Reserve");
  await expect(
    umpire.page.getByRole("region", { name: "Members" }).getByRole("row", { name: /Arthur/ }),
  ).toContainText("First Corps");

  // The Umpire adds units to First Corps.
  await umpire.page.getByRole("link", { name: "First Corps" }).click();
  const armyUrl = umpire.page.url();
  const units = umpire.page.getByRole("region", { name: "Units" });
  for (const name of ["1st Division", "Light Division"]) {
    await units.getByRole("textbox", { name: "New unit" }).fill(name);
    await units.getByRole("button", { name: "Add unit" }).click();
    await expect(units.getByText(name)).toBeVisible();
  }

  // Every Player sees every army; only the commander can open theirs, and see its units.
  for (const player of [commander, other]) {
    await player.page.goto(campaignUrl);
    const armies = player.page.getByRole("region", { name: "Armies" });
    await expect(armies.getByText("Reserve")).toBeVisible();
    await expect(armies).toContainText("Unassigned");
  }
  await commander.page.getByRole("link", { name: "First Corps" }).click();
  await expect(commander.page.getByRole("region", { name: "Units" })).toContainText(
    "Light Division",
  );
  await expect(commander.page.getByRole("button", { name: "Delete army" })).toBeHidden();
  await expect(
    other.page.getByRole("region", { name: "Armies" }).getByRole("link", { name: "First Corps" }),
  ).toHaveCount(0);
  await other.page.goto(armyUrl);
  await expect(other.page.getByText("Not found")).toBeVisible();

  // Given to Bea, First Corps is hers to see, and no longer Arthur's.
  await umpire.page.getByRole("combobox", { name: "Change commander" }).click();
  await umpire.page.getByRole("option", { name: other.name }).click();
  await expect(umpire.page.getByText(`${other.name} now commands First Corps.`)).toBeVisible();
  await other.page.reload();
  await expect(other.page.getByRole("region", { name: "Units" })).toContainText("1st Division");
  await commander.page.reload();
  await expect(commander.page.getByText("Not found")).toBeVisible();

  // Rename, then delete the army and its units.
  await umpire.page.getByRole("button", { name: "Rename army" }).click();
  await umpire.page.getByRole("dialog").getByRole("textbox", { name: "Name" }).fill("Guards");
  await umpire.page.getByRole("dialog").getByRole("button", { name: "Save" }).click();
  await expect(umpire.page.getByRole("heading", { level: 1, name: "Guards" })).toBeVisible();
  await umpire.page.getByRole("button", { name: "Delete army" }).click();
  await umpire.page.getByRole("dialog").getByRole("button", { name: "Delete army" }).click();
  await expect(umpire.page).toHaveURL(campaignUrl);
  await expect(
    umpire.page.getByRole("region", { name: "Armies" }).getByRole("link", { name: "Guards" }),
  ).toHaveCount(0);
});
