import { AxeBuilder } from "@axe-core/playwright";
import type { Page } from "@playwright/test";

// WCAG 2.1 A and AA, in a real browser: unlike jsdom, it has layout and computed colours, so axe
// checks colour contrast here (DESIGN.md §3.12).
const wcag = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];

/** axe's violations on the page, each labelled with `label`; empty if none. */
export async function scan(page: Page, label: string): Promise<string[]> {
  const results = await new AxeBuilder({ page }).withTags(wcag).analyze();
  return results.violations.map(
    (v) => `${label}: ${v.id} (${v.help}) at ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`,
  );
}
