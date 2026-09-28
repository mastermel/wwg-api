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

/**
 * Dark mode's greys, tinted toward the navy (Mantine's are neutral). Shade 8 is the page canvas and
 * 7 the panels on it; 6 the inputs.
 */
const dark: MantineColorsTuple = [
  "#c9ccd4",
  "#adb2bd",
  "#8b919d",
  "#6b717d",
  "#474c57",
  "#3a3f49",
  "#2d313a",
  "#23272f",
  "#1b1e25",
  "#13151a",
];

export const theme = createTheme({
  primaryColor: "navy",
  // Filled navy with white text: 8.3:1 (light, shade 8) and 5.5:1 (dark, shade 6).
  primaryShade: { light: 8, dark: 6 },
  colors: { navy, silver, gray: silver, dark },
  // System fonts only: no download, works offline, simpler CSP.
  fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
  headings: {
    fontWeight: "650",
    sizes: {
      // A page title that heads the page without shouting; sections are h2 at h4 size.
      h1: { fontSize: "1.875rem", lineHeight: "1.25" },
    },
  },
  defaultRadius: "md",
  components: {
    Paper: { defaultProps: { shadow: "xs" } },
    Card: { defaultProps: { shadow: "xs" } },
    Modal: {
      defaultProps: {
        radius: "md",
        overlayProps: { backgroundOpacity: 0.45, blur: 2 },
        // Mantine's close (×) button has no accessible name of its own.
        closeButtonProps: { "aria-label": "Close" },
      },
    },
    // Nor does the notifications' close button.
    Notification: { defaultProps: { closeButtonProps: { "aria-label": "Dismiss" } } },
    // Column headings are styled in app.css: as inline styles here they'd reach footers too.
    Table: { defaultProps: { verticalSpacing: "sm" } },
  },
});

/**
 * The app's surfaces, and fixes where Mantine's defaults fall short of AA.
 *
 * Surfaces: `--app-canvas` is the page behind everything; panels, the sidebar and the tab bar
 * sit on it in `--mantine-color-body` (which Mantine's Paper, Card and Modal use), and the header
 * is navy (`--app-header`, text `--app-header-text`).
 *
 * Contrast (checked with the WCAG formula): dimmed text is 5.3:1 on the canvas and 5.9:1 on
 * panels in light mode (Mantine's is 4.2:1 on the canvas), 7.0:1 and more in dark; dark mode's
 * links and light-variant text 5.6:1; yellow and orange light-variant text in light mode (the
 * offline banner, the admin list's "None" badge) 4.6:1 and 6.3:1; header text 10:1 and more. Input borders are 3.8:1 (light) and 4.1:1 (dark) against
 * their background, for WCAG 1.4.11 (Mantine's are about 2:1).
 */
export const cssVariablesResolver: CSSVariablesResolver = (t) => ({
  variables: {},
  light: {
    "--app-canvas": "#f1f3f7",
    "--app-header": t.colors.navy[9],
    "--app-header-text": t.white,
    "--app-header-dimmed": "#c7d3ea",
    "--app-input-border": t.colors.silver[7],
    "--mantine-color-dimmed": t.colors.silver[9],
    "--mantine-color-yellow-light-color": "#7a5200",
    "--mantine-color-orange-light-color": "#9a3c00",
  },
  dark: {
    "--app-canvas": t.colors.dark[8],
    "--app-header": "#172c52",
    "--app-header-text": t.white,
    "--app-header-dimmed": t.colors.navy[2],
    "--app-input-border": t.colors.dark[2],
    "--mantine-color-dimmed": t.colors.dark[1],
    "--mantine-color-anchor": t.colors.navy[3],
    "--mantine-primary-color-light-color": t.colors.navy[3],
    "--mantine-color-navy-light-color": t.colors.navy[3],
  },
});
