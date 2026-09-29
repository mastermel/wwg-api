import { describe, expect, it } from "vitest";
import { ApiError } from "@/lib/api-fetch";
import { errorMessage } from "@/lib/errors";

const fallback = "The army couldn't be deleted. Try again.";

describe("errorMessage", () => {
  it("uses the API's detail", () => {
    const error = new ApiError(409, undefined, { detail: "Mel already commands First Corps." });

    expect(errorMessage(error, fallback)).toBe("Mel already commands First Corps.");
  });

  it("falls back when the API gives no detail", () => {
    expect(errorMessage(new ApiError(404, undefined, { title: "Not Found" }), fallback)).toBe(
      fallback,
    );
  });

  it("says to wait on a rate limit", () => {
    expect(errorMessage(new ApiError(429, undefined, undefined), fallback)).toMatch(
      /Wait a minute/,
    );
  });

  it("blames the server for a 5xx, whatever its detail", () => {
    const error = new ApiError(500, undefined, { detail: "NullReferenceException at …" });

    expect(errorMessage(error, fallback)).toMatch(/went wrong on the server/);
  });

  it("says the server couldn't be reached when the request never got there", () => {
    expect(errorMessage(new TypeError("Failed to fetch"), fallback)).toMatch(/Couldn't reach/);
  });

  it("explains a 401 that a session refresh couldn't fix", () => {
    expect(errorMessage(new ApiError(401, undefined, undefined), fallback)).toMatch(
      /session couldn't be checked/,
    );
  });
});
