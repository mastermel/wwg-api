import type { ArmyColor } from "@/api/generated/model";

/**
 * The 8 army colours, from the Okabe–Ito palette (chosen to stay distinct with colour-blindness),
 * darkened for light mode and lightened for dark until each is at least 3.2:1 against the
 * panels and the canvas (WCAG 1.4.11, for a colour that identifies something): light against
 * #ffffff and #f1f3f7; dark against dark[7], dark[8] and dark[6]. Checked with the WCAG formula.
 *
 * Colour is never the only signal: the army's name (and flag) is always beside it.
 */
export const armyColors: Record<ArmyColor, { label: string; light: string; dark: string }> = {
  Red: { label: "Red", light: "#d55e00", dark: "#d55e00" }, // 3.5:1 / 3.4:1
  Blue: { label: "Blue", light: "#0072b2", dark: "#0084ce" }, // 4.7:1 / 3.2:1
  Green: { label: "Green", light: "#00996f", dark: "#009e73" }, // 3.3:1 / 3.8:1
  Orange: { label: "Orange", light: "#b37c00", dark: "#e69f00" }, // 3.3:1 / 5.8:1
  Purple: { label: "Purple", light: "#7b4fa8", dark: "#956fbb" }, // 5.4:1 / 3.3:1
  Sky: { label: "Sky blue", light: "#1b8fd0", dark: "#56b4e9" }, // 3.2:1 / 5.6:1
  Gold: { label: "Gold", light: "#92890c", dark: "#f0e442" }, // 3.3:1 / 9.9:1
  Magenta: { label: "Magenta", light: "#c6689c", dark: "#cc79a7" }, // 3.2:1 / 4.3:1
};

/** The CSS variable holding an army colour for the current colour scheme (see theme.ts). */
export const armyColorVar = (color: ArmyColor) => `var(--army-${color.toLowerCase()})`;

/** The theme's CSS variables for the palette, per colour scheme. */
export function armyColorVariables(scheme: "light" | "dark"): Record<string, string> {
  return Object.fromEntries(
    Object.entries(armyColors).map(([name, value]) => [
      `--army-${name.toLowerCase()}`,
      value[scheme],
    ]),
  );
}

/** The colour a new army gets, as the API picks it: the first no army has, else the least used. */
export function freeColor(taken: readonly ArmyColor[]): ArmyColor {
  const palette = Object.keys(armyColors) as ArmyColor[];
  const uses = (color: ArmyColor) => taken.filter((t) => t === color).length;
  return palette.reduce((best, color) => (uses(color) < uses(best) ? color : best));
}
