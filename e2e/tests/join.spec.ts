import { password, uniqueEmail } from "./support/accounts.ts";
import { createCampaign, join, joinLink } from "./support/campaigns.ts";
import { expect, test } from "./support/fixtures.ts";

test("a signed-out visitor registers from a join link and joins", async ({ browser, signUp }) => {
  const umpire = await signUp("Ada");
  await createCampaign(umpire.page, "The Peninsular War");
  const link = await joinLink(umpire.page);

  const visitor = await (await browser.newContext()).newPage();
  await visitor.goto(link);
  await expect(visitor.getByText("You've been invited to join")).toContainText(
    `The Peninsular War, run by ${umpire.name}.`,
  );
  await visitor.getByRole("link", { name: "Create an account" }).click();
  await visitor.getByRole("textbox", { name: "First name" }).fill("Arthur");
  await visitor.getByRole("textbox", { name: "Last name" }).fill("Wellesley");
  await visitor.getByRole("textbox", { name: "Email" }).fill(uniqueEmail("arthur"));
  await visitor.getByRole("textbox", { name: "Password" }).fill(password);
  await visitor.getByRole("button", { name: "Create account" }).click();

  // Back on the join page after registering.
  await visitor.getByRole("button", { name: "Join as a Player" }).click();
  await expect(
    visitor.getByRole("heading", { level: 1, name: "The Peninsular War" }),
  ).toBeVisible();
  await expect(visitor.getByText("You're a Player")).toBeVisible();
  await expect(visitor.getByRole("region", { name: "Join link" })).toBeHidden();

  await umpire.page.reload();
  await expect(
    umpire.page.getByRole("region", { name: "Members" }).getByText("Arthur Wellesley"),
  ).toBeVisible();
  await visitor.context().close();
});

test("a new join link stops the old one working", async ({ signUp }) => {
  const umpire = await signUp("Ada");
  const player = await signUp("Bea");
  await createCampaign(umpire.page, "The Peninsular War");
  const oldLink = await joinLink(umpire.page);

  await umpire.page.getByRole("button", { name: "Make a new link" }).click();
  await umpire.page.getByRole("dialog").getByRole("button", { name: "Make a new link" }).click();
  await expect(umpire.page.getByText("New join link made")).toBeVisible();
  const newLink = await joinLink(umpire.page);
  expect(newLink).not.toBe(oldLink);

  await player.page.goto(oldLink);
  await expect(player.page.getByText("This join link doesn't work")).toBeVisible();
  await join(player.page, newLink, "The Peninsular War");
});

test("a Player leaves, and the Umpire removes another", async ({ signUp }) => {
  const umpire = await signUp("Ada");
  const leaver = await signUp("Bea");
  const removed = await signUp("Cal");
  await createCampaign(umpire.page, "The Peninsular War");
  const link = await joinLink(umpire.page);
  await join(leaver.page, link, "The Peninsular War");
  await join(removed.page, link, "The Peninsular War");

  await leaver.page.getByRole("button", { name: "Leave campaign" }).click();
  await leaver.page.getByRole("dialog").getByRole("button", { name: "Leave campaign" }).click();
  await expect(leaver.page.getByText("You're not in any campaigns yet.")).toBeVisible();

  await umpire.page.reload();
  const members = umpire.page.getByRole("region", { name: "Members" });
  await expect(members.getByText(leaver.name)).toBeHidden();
  await members.getByRole("button", { name: `Remove ${removed.name}` }).click();
  await umpire.page.getByRole("dialog").getByRole("button", { name: "Remove" }).click();
  await expect(umpire.page.getByText(`Removed ${removed.name}.`)).toBeVisible();

  await removed.page.reload();
  await expect(removed.page.getByText("Not found")).toBeVisible();
});
