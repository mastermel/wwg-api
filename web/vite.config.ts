/// <reference types="vitest/config" />
import { fileURLToPath } from "node:url";
import { tanstackRouter } from "@tanstack/router-plugin/vite";
import react from "@vitejs/plugin-react";
import { defineConfig, type ProxyOptions } from "vite";
import { VitePWA } from "vite-plugin-pwa";

// In development the browser only talks to Vite (:5173); these paths go to the API (:5102), so
// it's one origin, like production where the API serves the built app.
const apiProxy: Record<string, ProxyOptions> = Object.fromEntries(
  ["/api", "/openapi", "/swagger", "/health"].map((path) => [
    path,
    { target: "http://localhost:5102", changeOrigin: false },
  ]),
);

export default defineConfig({
  define: {
    // The image tag (e.g. v20260927.143005), passed in by the Docker build; "dev" otherwise.
    __APP_VERSION__: JSON.stringify(process.env.APP_VERSION ?? "dev"),
  },
  plugins: [
    // Generates src/routeTree.gen.ts from src/routes/, and splits each route into its own chunk.
    tanstackRouter({ target: "react", autoCodeSplitting: true }),
    react(),
    VitePWA({
      // Ask before activating a new version (UpdatePrompt), rather than swapping it in silently.
      registerType: "prompt",
      injectRegister: false,
      pwaAssets: { config: true },
      manifest: {
        name: "WWG Campaigner",
        short_name: "WWG",
        description: "The Wasatch Wargamers campaign app.",
        theme_color: "#1c3f7a",
        background_color: "#1c3f7a",
        display: "standalone",
        start_url: "/",
        scope: "/",
      },
      workbox: {
        // The app shell only. API responses are never cached by the service worker; offline
        // data comes from the persisted query cache.
        // The plugin adds the manifest itself; globbing it too lists it twice,
        // and Workbox then refuses to install ("conflicting entries").
        globPatterns: ["**/*.{js,css,html,svg,png,ico}"],
        navigateFallback: "/index.html",
        navigateFallbackDenylist: [/^\/api\//, /^\/openapi/, /^\/swagger/, /^\/health/],
        cleanupOutdatedCaches: true,
      },
    }),
  ],
  resolve: {
    // @/ is src/ (also set in tsconfig.app.json).
    alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) },
  },
  server: { proxy: apiProxy },
  preview: { proxy: apiProxy },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
  },
});
