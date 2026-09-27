import { defineConfig, minimal2023Preset } from "@vite-pwa/assets-generator/config";

// Generates the PWA icons, favicon and Apple touch icon from public/app-icon.svg at build time
// (vite-plugin-pwa's pwaAssets option), so no PNGs are committed.
const navy = "#1c3f7a";

export default defineConfig({
  headLinkOptions: { preset: "2023" },
  preset: {
    ...minimal2023Preset,
    // Padding around the artwork is filled with the icon's own navy, not white.
    maskable: { ...minimal2023Preset.maskable, resizeOptions: { background: navy } },
    apple: { ...minimal2023Preset.apple, resizeOptions: { background: navy } },
  },
  images: ["public/app-icon.svg"],
});
