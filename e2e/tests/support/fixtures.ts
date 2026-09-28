import { test as base, type Browser, type Page } from "@playwright/test";
import { password, uniqueEmail } from "./accounts.ts";

export interface User {
  page: Page;
  email: string;
  firstName: string;
  lastName: string;
  name: string;
}

interface Fixtures {
  /**
   * A new person, in a browser context of their own (the test's browser project's device),
   * registered through the API and signed in. Call it once per person a test needs.
   */
  signUp: (firstName: string) => Promise<User>;
  /** An existing account (e.g. the Admin), signed in through the sign-in page in its own context. */
  signIn: (email: string, password: string) => Promise<Page>;
}

async function signUpIn(browser: Browser, firstName: string): Promise<User> {
  // browser.newContext() applies the project's `use` options (device, baseURL, HTTPS errors).
  const context = await browser.newContext();
  const email = uniqueEmail(firstName);
  const lastName = "Tester";
  // The context's request client shares its cookies: the refresh cookie lands in the browser.
  const response = await context.request.post("/api/auth/register", {
    data: { email, password, firstName, lastName },
  });
  if (!response.ok()) {
    throw new Error(`Registering ${email} failed: ${String(response.status())}`);
  }
  const page = await context.newPage();
  await page.goto("/campaigns");
  await page.getByRole("heading", { level: 1, name: "Campaigns" }).waitFor();
  return { page, email, firstName, lastName, name: `${firstName} ${lastName}` };
}

async function signInIn(browser: Browser, email: string, password: string): Promise<Page> {
  const page = await (await browser.newContext()).newPage();
  await page.goto("/sign-in");
  await page.getByRole("textbox", { name: "Email" }).fill(email);
  await page.getByRole("textbox", { name: "Password" }).fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.getByRole("heading", { level: 1, name: "Campaigns" }).waitFor();
  return page;
}

export const test = base.extend<Fixtures>({
  signUp: async ({ browser }, use) => {
    const pages: Page[] = [];
    await use(async (firstName) => {
      const user = await signUpIn(browser, firstName);
      pages.push(user.page);
      return user;
    });
    await Promise.all(pages.map((page) => page.context().close()));
  },
  signIn: async ({ browser }, use) => {
    const pages: Page[] = [];
    await use(async (email, password) => {
      const page = await signInIn(browser, email, password);
      pages.push(page);
      return page;
    });
    await Promise.all(pages.map((page) => page.context().close()));
  },
});

export { expect } from "@playwright/test";
