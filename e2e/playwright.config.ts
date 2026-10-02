import { defineConfig, devices } from "@playwright/test";

const ci = Boolean(process.env.CI);

/** Runs against the stack from ./stack.sh up: the production image behind a TLS proxy. */
export default defineConfig({
  testDir: "./tests",
  fullyParallel: true,
  forbidOnly: ci,
  retries: ci ? 1 : 0,
  reporter: ci
    ? [["github"], ["html", { open: "never" }]]
    : [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: "https://localhost:8443",
    // The proxy's certificate comes from its own local authority, which the browsers don't know.
    ignoreHTTPSErrors: true,
    // A click, fill or page load that hasn't happened in 10s won't: fail then, not at the test's
    // own timeout (30s, or 90s with test.slow()). Waiting on what's shown is expect's 5s.
    actionTimeout: 10_000,
    navigationTimeout: 10_000,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  // The supported browsers (DESIGN.md §3.12): Chrome on the desktop, and Safari on an iPhone.
  projects: [
    {
      name: "chromium",
      use: {
        ...devices["Desktop Chrome"],
        // ignoreHTTPSErrors isn't enough for the service worker: Chromium won't register one
        // from a certificate it doesn't trust without this.
        launchOptions: { args: ["--ignore-certificate-errors"] },
      },
    },
    { name: "iphone", use: { ...devices["iPhone 15"] } },
  ],
});
