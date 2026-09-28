import { expect, type Page } from "@playwright/test";

/** Creates a campaign through the New campaign page; the page ends on its details. */
export async function createCampaign(page: Page, name: string): Promise<void> {
  await page.goto("/campaigns");
  await page.getByRole("link", { name: "New campaign" }).click();
  await page.getByRole("textbox", { name: "Name" }).fill(name);
  await page.getByRole("button", { name: "Create campaign" }).click();
  await expect(page.getByRole("heading", { level: 1, name })).toBeVisible();
}

/** The join link, as the Umpire sees it on the campaign's page (which `page` must be on). */
export async function joinLink(page: Page): Promise<string> {
  const box = page.getByRole("textbox", { name: "Send this link to your Players" });
  await expect(box).toHaveValue(/\/join\//);
  return box.inputValue();
}

/** A signed-in user joins with the link; the page ends on the campaign. */
export async function join(page: Page, link: string, campaignName: string): Promise<void> {
  await page.goto(link);
  await page.getByRole("button", { name: "Join as a Player" }).click();
  await expect(page.getByRole("heading", { level: 1, name: campaignName })).toBeVisible();
}
