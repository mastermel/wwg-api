import { defineConfig, minimal2023Preset } from "@vite-pwa/assets-generator/config";

// Generates the PWA icons, favicon and Apple touch icon from public/app-icon.svg at build time
// (vite-plugin-pwa's pwaAssets option), so no PNGs are committed.
const navy = "#1c3f7a";

export default defineConfig({
  headLinkOptions: { preset: "2023" },
  preset: {
    ...minimal2023Preset,
    // Full colour: the default (quality 60) reduces the icons to a palette, which roughens the
    // figure's edges.
    png: { compressionLevel: 9, palette: false },
    // The rounded square fills these: Android and iOS cut their own shape from it (the figure
    // keeps to Android's safe middle circle). The corners it leaves are the icon's navy, not white.
    maskable: {
      ...minimal2023Preset.maskable,
      padding: 0,
      resizeOptions: { background: navy },
    },
    apple: { ...minimal2023Preset.apple, padding: 0, resizeOptions: { background: navy } },
  },
  images: ["public/app-icon.svg"],
});
