import { expect, type Browser, type Locator, type Page } from "@playwright/test";
import { admin } from "./accounts.ts";
import { apiIn } from "./api.ts";

export interface LibraryUnit {
  name: string;
  type: string;
  fightingFactor?: number;
  points?: number;
}

let counter = 0;

/** A name no other test (or run, or browser project) uses: every run shares the library. */
export function uniqueName(name: string): string {
  counter += 1;
  return `${name} ${String(Date.now())}-${String(process.pid)}-${String(counter)}`;
}

/**
 * A library faction of the test's own, with these units (FF 6 and 30 points unless given), made
 * through the API by the shared Admin: only Managers and Admins fill the library (decision 0015).
 * Its name is `name` with a stamp (`uniqueName`).
 */
export async function libraryFaction(
  browser: Browser,
  name: string,
  nation: string,
  units: LibraryUnit[],
) {
  const context = await browser.newContext();
  try {
    const signedIn = await context.request.post("/api/auth/login", { data: admin });
    if (!signedIn.ok()) throw new Error(`The Admin's sign-in failed: ${String(signedIn.status())}`);
    const api = await apiIn(context.request);
    const faction = await api.post<{ id: string; name: string }>("/api/factions", {
      name: uniqueName(name),
      nation,
    });
    const made: { id: string; name: string }[] = [];
    for (const unit of units) {
      made.push(
        await api.post<{ id: string; name: string }>(`/api/factions/${faction.id}/units`, {
          fightingFactor: 6,
          points: 30,
          ...unit,
        }),
      );
    }
    return { ...faction, units: made };
  } finally {
    await context.close();
  }
}

/** The browser `page` runs in, for `libraryFaction`. */
export function browserOf(page: { context: () => { browser: () => Browser | null } }): Browser {
  const browser = page.context().browser();
  if (!browser) throw new Error("The page has no browser.");
  return browser;
}

/**
 * Picks `name` in a searchable list (a Mantine MultiSelect in a dialog), as a person would. The
 * field is brought to the middle of the screen first: Mantine hides the list of a field that's
 * out of sight (below a phone's fold). The option is then chosen with the keyboard: clicking it
 * makes Playwright scroll it into view, which moves the field, which flips the list above or
 * below it, so the option never holds still long enough to click.
 */
export async function chooseFromList(field: Locator, name: string) {
  await field.evaluate((element) => {
    element.scrollIntoView({ block: "center" });
  });
  await field.fill(name);
  await expect(field.page().getByRole("option", { name })).toBeVisible();
  await field.press("ArrowDown");
  await field.press("Enter");
}

/** The Umpire, on an army's page, has it take units from this library faction (Edit army). */
export async function chooseFaction(page: Page, factionName: string) {
  await page.getByRole("button", { name: "Edit army" }).click();
  const dialog = page.getByRole("dialog");
  await chooseFromList(dialog.getByRole("combobox", { name: "Factions" }), factionName);
  await closeFactionList(page);
  await dialog.getByRole("button", { name: "Save" }).click();
  await expect(page.getByText(`Units from ${factionName}`)).toBeVisible();
  await expect(page.getByRole("dialog")).toHaveCount(0);
}

/** The Umpire, on an army's page, ticks these library units in Add units and adds them. */
export async function addFromLibrary(page: Page, unitNames: string[]) {
  await page
    .getByRole("region", { name: "Units" })
    .getByRole("button", { name: "Add units" })
    .click();
  const dialog = page.getByRole("dialog", { name: "Add units" });
  for (const name of unitNames) await dialog.getByRole("checkbox", { name }).check();
  await dialog.getByRole("button", { name: /^Add \d+ units?$/ }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
}

/**
 * Closes the army form's Factions list, which stays open after a pick, as a person moves on:
 * with every run's factions in it, it's a long list (and scans flag its scrolling while open).
 */
export async function closeFactionList(page: Page) {
  await page.getByRole("dialog").getByRole("textbox", { name: "Name" }).click();
  await expect(page.getByRole("listbox")).toBeHidden();
}
