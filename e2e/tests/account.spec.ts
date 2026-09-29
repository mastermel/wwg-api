import { password, uniqueEmail } from "./support/accounts.ts";
import { expect, test } from "./support/fixtures.ts";
import { latestEmailText } from "./support/mailpit.ts";

test("changes the name and email; the old address is told, other devices signed out", async ({
  signUp,
  signIn,
}) => {
  const user = await signUp("Ada");
  const otherDevice = await signIn(user.email, password);
  const page = user.page;
  await page.goto("/account");

  const name = page.getByRole("region", { name: "Your name" });
  await name.getByRole("textbox", { name: "First name" }).fill("Adeline");
  await name.getByRole("button", { name: "Save name" }).click();
  await expect(page.getByText("Your name was saved.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Adeline" })).toBeVisible();

  const newEmail = uniqueEmail("adeline");
  const email = page.getByRole("region", { name: "Email" });
  await email.getByRole("textbox", { name: "New email" }).fill(newEmail);
  await email.getByRole("textbox", { name: "Current password" }).fill(password);
  await email.getByRole("button", { name: "Change email" }).click();
  await expect(page.getByText(`You'll sign in with ${newEmail} from now on.`)).toBeVisible();
  await expect(page.getByText(`Signed in as ${newEmail}`)).toBeVisible();

  expect(await latestEmailText(user.email)).toContain(newEmail);
  await otherDevice.reload();
  await expect(otherDevice.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();
});

test("changes the password, and signs in with the new one", async ({ page, signUp }) => {
  const user = await signUp("Bea");
  await user.page.goto("/account");

  const section = user.page.getByRole("region", { name: "Password" });
  await section.getByRole("textbox", { name: "Current password" }).fill(password);
  await section.getByRole("textbox", { name: "New password", exact: true }).fill("a new password");
  await section.getByRole("textbox", { name: "Confirm new password" }).fill("a new password");
  await section.getByRole("button", { name: "Change password" }).click();
  await expect(user.page.getByText("Password changed.")).toBeVisible();

  // This session carries on (the change issued new tokens).
  await user.page.reload();
  await expect(user.page.getByRole("heading", { level: 1, name: "Account" })).toBeVisible();

  await page.goto("/sign-in");
  await page.getByRole("textbox", { name: "Email" }).fill(user.email);
  await page.getByRole("textbox", { name: "Password" }).fill("a new password");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Campaigns" })).toBeVisible();
});

test("signs out everywhere, this device included", async ({ signUp, signIn }) => {
  const user = await signUp("Cai");
  const otherDevice = await signIn(user.email, password);
  await user.page.goto("/account");

  await user.page.getByRole("button", { name: "Sign out everywhere" }).click();
  await user.page.getByRole("dialog").getByRole("button", { name: "Sign out everywhere" }).click();

  await expect(user.page.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();
  await otherDevice.reload();
  await expect(otherDevice.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();
});
