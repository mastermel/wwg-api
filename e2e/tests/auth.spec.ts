import { password, uniqueEmail } from "./support/accounts.ts";
import { expect, test } from "./support/fixtures.ts";
import { latestEmailText, linkIn } from "./support/mailpit.ts";

test("registers, stays signed in across a reload, and signs out", async ({ page }) => {
  await page.goto("/register");
  await page.getByRole("textbox", { name: "First name" }).fill("Ada");
  await page.getByRole("textbox", { name: "Last name" }).fill("Tester");
  await page.getByRole("textbox", { name: "Email" }).fill(uniqueEmail("ada"));
  await page.getByRole("textbox", { name: "Password" }).fill(password);
  await page.getByRole("button", { name: "Create account" }).click();

  await expect(page.getByText("You're not in any campaigns yet.")).toBeVisible();

  // The access token is only in memory: staying signed in is the refresh cookie at work.
  await page.reload();
  await expect(page.getByRole("heading", { level: 1, name: "Campaigns" })).toBeVisible();

  await page.getByRole("button", { name: "Ada" }).click();
  await page.getByRole("menuitem", { name: "Sign out" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();
  await page.reload();
  await expect(page.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();
});

test("sends a signed-out visitor to sign in, then back where they were going", async ({
  page,
  signUp,
}) => {
  const user = await signUp("Ada");
  await page.goto("/about");
  await expect(page.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();

  await page.getByRole("textbox", { name: "Email" }).fill(user.email);
  await page.getByRole("textbox", { name: "Password" }).fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page.getByRole("heading", { level: 1, name: "About" })).toBeVisible();
});

// Its own user: failed sign-ins lock an account out, which would break every test sharing it.
test("says so when the password is wrong", async ({ page, signUp }) => {
  const user = await signUp("Ada");
  await page.goto("/sign-in");
  await page.getByRole("textbox", { name: "Email" }).fill(user.email);
  await page.getByRole("textbox", { name: "Password" }).fill("not the password");
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page.getByRole("alert")).toContainText("incorrect");
});

test("says so when signing up with an email that's taken", async ({ page, signUp }) => {
  const user = await signUp("Dee");

  await page.goto("/register");
  await page.getByRole("textbox", { name: "First name" }).fill("Dee");
  await page.getByRole("textbox", { name: "Last name" }).fill("Again");
  await page.getByRole("textbox", { name: "Email" }).fill(user.email);
  await page.getByRole("textbox", { name: "Password" }).fill(password);
  await page.getByRole("button", { name: "Create account" }).click();

  await expect(page.getByText("An account with this email already exists.")).toBeVisible();
  await expect(page.getByRole("heading", { level: 1, name: "Create an account" })).toBeVisible();
});

test("resets a forgotten password with the emailed link", async ({ page, signUp }) => {
  const user = await signUp("Bea");
  await user.page.context().close();

  await page.goto("/sign-in");
  await page.getByRole("link", { name: "Forgot your password?" }).click();
  await page.getByRole("textbox", { name: "Email" }).fill(user.email);
  await page.getByRole("button", { name: "Send reset link" }).click();
  await expect(page.getByText("Check your email")).toBeVisible();

  const link = linkIn(await latestEmailText(user.email), "https://localhost:8443/reset-password");
  await page.goto(link);
  await page
    .getByRole("textbox", { name: "New password", exact: true })
    .fill("a brand new password");
  await page.getByRole("textbox", { name: "Confirm new password" }).fill("a brand new password");
  await page.getByRole("button", { name: "Change password" }).click();

  await expect(page.getByText("Password changed")).toBeVisible();
  await page.getByRole("textbox", { name: "Email" }).fill(user.email);
  await page.getByRole("textbox", { name: "Password" }).fill("a brand new password");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Campaigns" })).toBeVisible();
});

test("a new account confirms its email with the welcome's link", async ({ signUp }) => {
  const user = await signUp("Cleo");
  const page = user.page;
  await page.goto("/campaigns");
  await expect(page.getByText("Confirm your email")).toBeVisible();

  const link = linkIn(
    await latestEmailText(user.email, { welcome: true }),
    "https://localhost:8443/confirm-email",
  );
  await page.goto(link);

  await expect(page.getByText("Your email is confirmed")).toBeVisible();
  await page.getByRole("link", { name: "Go to your campaigns" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Campaigns" })).toBeVisible();
  await expect(page.getByText("Confirm your email")).toHaveCount(0);
});
