import { describe, expect, it, vi } from "vitest";
import { ApiError } from "@/lib/api-fetch";
import { applyServerErrors } from "@/lib/form-errors";

const fields = ["name", "description"] as const;

const validation = (errors: Record<string, string[]>, detail?: string) =>
  new ApiError(400, undefined, { status: 400, title: "Validation failed", detail, errors });

describe("applyServerErrors", () => {
  it("puts each validation error on its field, with nothing left for the form", () => {
    const setError = vi.fn();

    const message = applyServerErrors(
      validation({ name: ["Too long.", "Not unique."] }),
      setError,
      fields,
    );

    expect(setError).toHaveBeenCalledWith("name", { message: "Too long. Not unique." });
    expect(message).toBeNull();
  });

  it("returns a message for the form when an error is on a field the form doesn't have", () => {
    const setError = vi.fn();

    const message = applyServerErrors(
      validation({ name: ["Too long."], campaignId: ["Gone."] }, "The campaign is gone."),
      setError,
      fields,
    );

    expect(setError).toHaveBeenCalledOnce();
    expect(message).toBe("The campaign is gone.");
  });

  it("returns the API's detail for an error that isn't about fields", () => {
    const error = new ApiError(409, undefined, {
      title: "Conflict",
      detail: "Mel already commands First Corps.",
    });

    expect(applyServerErrors(error, vi.fn(), fields)).toBe("Mel already commands First Corps.");
  });

  it("says the server couldn't be reached when the request never got there", () => {
    expect(applyServerErrors(new TypeError("Failed to fetch"), vi.fn(), fields)).toMatch(
      /Couldn't reach the server/,
    );
  });
});
