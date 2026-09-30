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

test("every member sees every army and its units; only the Umpire changes them", async ({
  signUp,
}) => {
  // Three people and a long flow: longer than the default time, beside the other tests.
  test.slow();
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
  const units = umpire.page.getByRole("region", { name: "Units" });
  for (const unit of [
    { name: "1st Division", type: "Line Infantry", ff: "5", points: "30" },
    { name: "Light Division", type: "Light Infantry", ff: "6", points: "25" },
  ]) {
    await units.getByRole("button", { name: "Add unit" }).click();
    const dialog = umpire.page.getByRole("dialog");
    await dialog.getByRole("textbox", { name: "Name" }).fill(unit.name);
    await dialog.getByRole("combobox", { name: "Type" }).click();
    await dialog.getByRole("option", { name: unit.type }).click();
    await dialog.getByRole("textbox", { name: "Fighting Factor (FF)" }).fill(unit.ff);
    await dialog.getByRole("textbox", { name: "Points" }).fill(unit.points);
    await dialog.getByRole("button", { name: "Add unit" }).click();
    await expect(units.getByRole("row").filter({ hasText: unit.name })).toContainText(unit.type);
  }
  await expect(units.getByRole("rowheader", { name: "2 units" })).toBeVisible();
  await expect(units.getByRole("row", { name: /2 units/ })).toContainText("55");

  // The Umpire edits one.
  await units.getByRole("button", { name: "Edit Light Division" }).click();
  await umpire.page
    .getByRole("dialog")
    .getByRole("textbox", { name: "Fighting Factor (FF)" })
    .fill("7");
  await umpire.page.getByRole("dialog").getByRole("button", { name: "Save" }).click();
  await expect(units.getByRole("row", { name: /Light Division/ })).toContainText("7");

  // Every Player sees every army, can open it and see its units; only the Umpire changes them.
  for (const player of [commander, other]) {
    await player.page.goto(campaignUrl);
    const armies = player.page.getByRole("region", { name: "Armies" });
    await expect(armies.getByRole("link", { name: "Reserve" })).toBeVisible();
    await expect(armies).toContainText("Unassigned");
    await armies.getByRole("link", { name: "First Corps" }).click();
    await expect(player.page.getByRole("region", { name: "Units" })).toContainText(
      "Light Division",
    );
    await expect(player.page.getByRole("button", { name: "Edit army" })).toBeHidden();
    await expect(player.page.getByRole("button", { name: "Delete army" })).toBeHidden();
  }
  await expect(commander.page.getByText(`Commanded by ${commander.name} (you)`)).toBeVisible();

  // Given to Bea, First Corps is hers to command.
  await umpire.page.getByRole("combobox", { name: "Change commander" }).click();
  await umpire.page.getByRole("option", { name: other.name }).click();
  await expect(umpire.page.getByText(`${other.name} now commands First Corps.`)).toBeVisible();
  await other.page.reload();
  await expect(other.page.getByText(`Commanded by ${other.name} (you)`)).toBeVisible();

  // Edit, then delete the army and its units.
  await umpire.page.getByRole("button", { name: "Edit army" }).click();
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

test("the Umpire deletes a unit after confirming", async ({ signUp }) => {
  const umpire = await signUp("Ada");
  await createCampaign(umpire.page, "The Hundred Days");
  await addArmy(umpire, "Prussian Army");
  await umpire.page.getByRole("link", { name: "Prussian Army" }).click();
  const units = umpire.page.getByRole("region", { name: "Units" });
  await units.getByRole("button", { name: "Add unit" }).click();
  const dialog = umpire.page.getByRole("dialog");
  await dialog.getByRole("textbox", { name: "Name" }).fill("IV Corps");
  await dialog.getByRole("combobox", { name: "Type" }).click();
  await dialog.getByRole("option", { name: "Line Infantry" }).click();
  await dialog.getByRole("textbox", { name: "Fighting Factor (FF)" }).fill("5");
  await dialog.getByRole("textbox", { name: "Points" }).fill("20");
  await dialog.getByRole("button", { name: "Add unit" }).click();
  await expect(units.getByRole("row", { name: /IV Corps/ })).toBeVisible();

  await units.getByRole("button", { name: "Delete IV Corps" }).click();
  await umpire.page.getByRole("dialog").getByRole("button", { name: "Delete unit" }).click();

  await expect(umpire.page.getByText("Deleted IV Corps.")).toBeVisible();
  await expect(units.getByText("No units yet")).toBeVisible();
});

test("the Umpire adds factions and puts an army in one, with its nation", async ({ signUp }) => {
  const umpire = await signUp("Ada");
  const page = umpire.page;
  await createCampaign(page, "The War of the Sixth Coalition");
  await addArmy(umpire, "Armée du Nord");

  const factions = page.getByRole("region", { name: "Factions" });
  await expect(factions.getByText("No factions yet")).toBeVisible();
  await factions.getByRole("button", { name: "New faction" }).click();
  await page.getByRole("dialog").getByRole("textbox", { name: "Name" }).fill("French Empire");
  await page.getByRole("dialog").getByRole("button", { name: "Add faction" }).click();
  await expect(factions.getByText("French Empire")).toBeVisible();
  await expect(page.getByRole("region", { name: "Armies" })).toContainText("Unassigned");

  await page.getByRole("link", { name: "Armée du Nord" }).click();
  await page.getByRole("button", { name: "Edit army" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("combobox", { name: "Faction" }).click();
  await dialog.getByRole("option", { name: "French Empire" }).click();
  await dialog.getByRole("combobox", { name: "Nation" }).click();
  await dialog.getByRole("option", { name: "France" }).click();
  await dialog.getByRole("button", { name: "Save" }).click();
  await expect(page.getByText("Saved Armée du Nord.")).toBeVisible();

  await page.getByRole("link", { name: "The War of the Sixth Coalition" }).click();
  const row = page.getByRole("region", { name: "Armies" }).getByRole("row", { name: /Armée/ });
  await expect(row).toContainText("French Empire");
  await expect(row.getByTitle("France")).toBeVisible();
  await expect(factions).toContainText("1 army");
});
