import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { ApiError, apiFetch } from "@/lib/api-fetch";
import { server } from "@/test/server";

describe("apiFetch", () => {
  it("returns the parsed JSON body of a successful response", async () => {
    server.use(http.get("*/api/thing", () => HttpResponse.json({ name: "Army" })));

    await expect(apiFetch("/api/thing", { method: "GET" })).resolves.toEqual({ name: "Army" });
  });

  it("returns undefined for an empty response", async () => {
    server.use(http.delete("*/api/thing", () => new HttpResponse(null, { status: 204 })));

    await expect(apiFetch("/api/thing", { method: "DELETE" })).resolves.toBeUndefined();
  });

  it("throws an ApiError with the Problem Details for an error response", async () => {
    server.use(
      http.post("*/api/thing", () =>
        HttpResponse.json(
          {
            title: "One or more validation errors occurred.",
            status: 400,
            errors: { name: ["Required."] },
          },
          { status: 400, headers: { "Content-Type": "application/problem+json" } },
        ),
      ),
    );

    const error = await apiFetch("/api/thing", { method: "POST" }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(400);
    expect(apiError.problem?.errors).toEqual({ name: ["Required."] });
    expect(apiError.message).toBe("One or more validation errors occurred.");
  });

  it("keeps a non-problem error body without Problem Details", async () => {
    server.use(
      http.get("*/health", () => HttpResponse.json({ status: "Unhealthy" }, { status: 503 })),
    );

    const error = (await apiFetch("/health", { method: "GET" }).catch(
      (e: unknown) => e,
    )) as ApiError;

    expect(error.status).toBe(503);
    expect(error.body).toEqual({ status: "Unhealthy" });
    expect(error.problem).toBeUndefined();
  });
});
