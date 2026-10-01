import { expect, test } from "./support/fixtures.ts";

test.use({ colorScheme: "light" });

test("a user chooses dark, which is kept, and can go back to the system's", async ({ signUp }) => {
  const { page } = await signUp("Ada");
  const html = page.locator("html");
  await expect(html).toHaveAttribute("data-mantine-color-scheme", "light");

  const menu = page.getByRole("button", { name: /Ada/ });
  await menu.click();
  await page.getByRole("menuitem", { name: "Dark" }).click();
  await expect(html).toHaveAttribute("data-mantine-color-scheme", "dark");

  // Kept: dark from the first paint after a reload, though the system is light.
  await page.reload();
  await expect(html).toHaveAttribute("data-mantine-color-scheme", "dark");

  await menu.click();
  await expect(page.getByRole("menuitem", { name: "Dark" })).toHaveAttribute(
    "aria-current",
    "true",
  );
  await page.getByRole("menuitem", { name: "Match the system" }).click();
  await expect(html).toHaveAttribute("data-mantine-color-scheme", "light");
});
