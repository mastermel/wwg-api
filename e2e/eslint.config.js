import js from "@eslint/js";
import prettier from "eslint-config-prettier";
import playwright from "eslint-plugin-playwright";
import { defineConfig, globalIgnores } from "eslint/config";
import tseslint from "typescript-eslint";

export default defineConfig([
  globalIgnores(["playwright-report", "test-results"]),
  {
    files: ["**/*.ts"],
    extends: [
      js.configs.recommended,
      tseslint.configs.strictTypeChecked,
      tseslint.configs.stylisticTypeChecked,
    ],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
  },
  {
    files: ["tests/**/*.ts"],
    extends: [playwright.configs["flat/recommended"]],
    rules: {
      // A test that can't run in one browser skips there, saying why; never unconditionally.
      "playwright/no-skipped-test": ["error", { allowConditional: true }],
    },
  },
  {
    // This config file itself: plain JS, no type information.
    files: ["**/*.js"],
    extends: [js.configs.recommended, tseslint.configs.disableTypeChecked],
  },
  prettier,
]);
