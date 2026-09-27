/// <reference types="vitest/config" />
import { fileURLToPath } from "node:url";
import { tanstackRouter } from "@tanstack/router-plugin/vite";
import react from "@vitejs/plugin-react";
import { defineConfig, type ProxyOptions } from "vite";

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
