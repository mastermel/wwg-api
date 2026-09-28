import { expect, test } from "@playwright/test";

// How the image serves the app (DESIGN.md §3.11). Not browser-specific: Chromium only.
test.describe("hosting", () => {
  test.skip(({ browserName }) => browserName !== "chromium", "the same for every browser");

  test("serves the app with its security headers", async ({ request }) => {
    const response = await request.get("/");

    expect(response.ok()).toBe(true);
    const headers = response.headers();
    expect(headers["content-security-policy"]).toContain("default-src 'self'");
    expect(headers["content-security-policy"]).toContain("frame-ancestors 'none'");
    expect(headers["x-content-type-options"]).toBe("nosniff");
    expect(headers["cache-control"]).toBe("no-cache");
  });

  test("serves the app for a deep link", async ({ page }) => {
    await page.goto("/campaigns/0192f5c1-0000-7000-8000-000000000000");

    await expect(page.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();
  });

  test("answers an unknown API path with a Problem Details 404, not the app", async ({
    request,
  }) => {
    const response = await request.get("/api/no-such-thing");

    expect(response.status()).toBe(404);
    expect(response.headers()["content-type"]).toContain("application/problem+json");
  });

  test("reports itself healthy", async ({ request }) => {
    const response = await request.get("/health");

    expect(await response.json()).toEqual({ status: "Healthy" });
  });
});
