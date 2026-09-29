import { defineConfig } from "orval";

// Generates the API client from the committed contract (../api/openapi.json) into
// src/api/generated/ (git-ignored). Run by `npm run generate`, which runs before dev, build,
// lint, typecheck and test.
export default defineConfig({
  api: {
    input: { target: "../api/openapi.json" },
    output: {
      mode: "tags-split",
      target: "src/api/generated/endpoints",
      schemas: "src/api/generated/model",
      client: "react-query",
      httpClient: "fetch",
      clean: true,
      override: {
        mutator: { path: "src/lib/api-fetch.ts", name: "apiFetch" },
        fetch: { includeHttpResponseReturnType: false },
      },
    },
  },
  zod: {
    input: { target: "../api/openapi.json" },
    output: {
      mode: "tags-split",
      target: "src/api/generated/zod",
      client: "zod",
      fileExtension: ".zod.ts",
      clean: true,
    },
  },
});
