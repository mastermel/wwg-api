import { admin } from "./support/accounts.ts";
import { scan } from "./support/axe.ts";
import { createCampaign } from "./support/campaigns.ts";
import { desktopOnly, expect, test } from "./support/fixtures.ts";

test("an Admin gives an Umpire-less campaign a new Umpire", async ({
  signIn,
  signUp,
  isMobile,
}) => {
  test.skip(isMobile, desktopOnly);
  const umpire = await signUp("Bob");
  const successor = await signUp("Cal");
  const campaign = `Austerlitz ${String(Date.now())}`;
  await createCampaign(umpire.page, campaign);

  const page = await signIn(admin.email, admin.password);
  const nav = page.getByRole("navigation", { name: "Main" });

  // Deleting the Umpire's account leaves the campaign with no Umpire.
  await nav.getByRole("link", { name: "Users" }).click();
  await page.getByRole("searchbox", { name: "Search" }).fill(umpire.email);
  await expect(page).toHaveURL(/search=/);
  // Other tests make Bobs too: the email picks this one.
  await page.getByRole("row").filter({ hasText: umpire.email }).getByRole("link").click();
  await expect(page.getByRole("region", { name: "Campaigns" })).toContainText(campaign);
  await page.getByRole("button", { name: "Delete user" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Delete user" }).click();
  await expect(page.getByText(`Deleted ${umpire.name}.`)).toBeVisible();

  await nav.getByRole("link", { name: "All campaigns" }).click();
  await page.getByRole("checkbox", { name: "Only those without an Umpire" }).check();
  await page.getByRole("searchbox", { name: "Search" }).fill(campaign);
  // The search is debounced and kept in the URL: wait for it, not the list it replaces.
  await expect(page).toHaveURL(/search=/);
  const row = page.getByRole("row", { name: new RegExp(campaign) });
  await expect(row).toContainText(/none/i);
  await row.getByRole("link", { name: campaign }).click();

  await page.getByRole("button", { name: "Set Umpire" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("combobox", { name: "New Umpire" }).fill(successor.email);
  await dialog.getByRole("option", { name: `${successor.name} (${successor.email})` }).click();
  await dialog.getByRole("button", { name: "Set Umpire" }).click();
  await expect(page.getByText(`${successor.name} is now the Umpire.`)).toBeVisible();

  await successor.page.reload();
  const card = successor.page.getByRole("article").filter({ hasText: campaign });
  await expect(card).toContainText("Umpire");
});

test("the admin screens aren't there for anyone else", async ({ signUp }) => {
  const { page } = await signUp("Ada");

  await expect(page.getByRole("link", { name: "Users" })).toHaveCount(0);
  await page.goto("/admin/users");
  await expect(page.getByRole("heading", { level: 1, name: /not found/i })).toBeVisible();
});

test("an Admin masquerades as a Player, sees what they see, and ends it", async ({
  signIn,
  signUp,
  isMobile,
}) => {
  test.skip(isMobile, desktopOnly);
  const bob = await signUp("Bob");
  const campaign = `Masked Ball ${String(Date.now())}`;
  await createCampaign(bob.page, campaign);

  const page = await signIn(admin.email, admin.password);
  const nav = page.getByRole("navigation", { name: "Main" });
  await nav.getByRole("link", { name: "Users" }).click();
  await page.getByRole("searchbox", { name: "Search" }).fill(bob.email);
  await expect(page).toHaveURL(/search=/);
  await page.getByRole("row").filter({ hasText: bob.email }).getByRole("link").click();
  await page.getByRole("button", { name: "Masquerade as Bob" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Masquerade" }).click();

  // Bob's app: his campaigns, no admin screens, and the account button says so.
  await expect(page.getByText(`You're masquerading as ${bob.name}.`)).toBeVisible();
  await expect(page.getByRole("article").filter({ hasText: campaign })).toBeVisible();
  await expect(nav.getByRole("link", { name: "Users" })).toHaveCount(0);
  const account = page.getByRole("button", { name: "Bob (masquerade)" });
  await expect(account).toBeVisible();
  expect(await scan(page, "campaigns, masquerading")).toEqual([]);
  await page.goto("/admin/users");
  await expect(page.getByRole("heading", { level: 1, name: /not found/i })).toBeVisible();

  // A fresh load keeps the masquerade (the refresh cookie is Bob's, marked).
  await page.goto("/campaigns");
  await account.click();
  await page.getByRole("menuitem", { name: "End masquerade" }).click();

  await expect(page.getByText("Masquerade ended: you're yourself again.")).toBeVisible();
  await expect(page.getByRole("heading", { level: 1, name: "Users" })).toBeVisible();
  await expect(page.getByRole("button", { name: /masquerade/ })).toHaveCount(0);
});
