import { createTheme, type CSSVariablesResolver, type MantineColorsTuple } from "@mantine/core";

/** Brand primary. Shades chosen so text and filled components meet WCAG AA in both schemes. */
const navy: MantineColorsTuple = [
  "#edf1f8",
  "#d8e0ee",
  "#b1c1dd",
  "#879fcb",
  "#6483bd",
  "#4e71b4",
  "#4268b1",
  "#33579b",
  "#2a4d8b",
  "#1c3f7a",
];

/** Brand neutral: a cool, slightly blue grey. Also replaces Mantine's gray. */
const silver: MantineColorsTuple = [
  "#f5f6f8",
  "#e9ebef",
  "#d2d6dd",
  "#b9bfc9",
  "#a3abb7",
  "#959eac",
  "#8d97a6",
  "#7a8392",
  "#6c7584",
  "#5b6576",
];

export const theme = createTheme({
  primaryColor: "navy",
  // Filled navy with white text: 8.3:1 (light, shade 8) and 5.5:1 (dark, shade 6).
  primaryShade: { light: 8, dark: 6 },
  colors: { navy, silver, gray: silver },
  // System fonts only: no download, works offline, simpler CSP.
  fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
  headings: { fontWeight: "650" },
  defaultRadius: "md",
});

/**
 * Mantine's defaults fall short of AA in a few places: dimmed text; in dark mode, links and
 * light-variant text (4.0–4.1:1 on the dark background); and in light mode, yellow light-variant
 * text such as the offline banner's title and icon (2.7:1). These raise them to 4.6:1 or more.
 */
export const cssVariablesResolver: CSSVariablesResolver = (t) => ({
  variables: {},
  light: {
    "--mantine-color-dimmed": t.colors.silver[8],
    "--mantine-color-yellow-light-color": "#7a5200",
  },
  dark: {
    "--mantine-color-dimmed": t.colors.dark[1],
    "--mantine-color-anchor": t.colors.navy[3],
    "--mantine-primary-color-light-color": t.colors.navy[3],
    "--mantine-color-navy-light-color": t.colors.navy[3],
  },
});
