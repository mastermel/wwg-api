import { describe, expect, it } from "vitest";
import type { HexSettlement } from "@/api/generated/model";
import { rulesValue, settlementValue } from "@/features/maps/victory";
import { noSettlement } from "@/features/maps/terrain";

const place = (changes: Partial<HexSettlement>): HexSettlement => ({ ...noSettlement, ...changes });

describe("rulesValue", () => {
  it("is the highest that applies, and a capital's more, as the API's", () => {
    expect(
      [
        place({}),
        place({ size: "Town" }),
        place({ size: "City" }),
        place({ size: "Town", walled: true }),
        place({ fortress: true }),
        place({ size: "City", walled: true, fortress: true }),
        place({ size: "City", capital: "Capital" }),
        place({ size: "Town", capital: "Minor" }),
      ].map(rulesValue),
    ).toEqual([0, 10, 25, 35, 50, 50, 50, 20]);
  });
});

describe("settlementValue", () => {
  it("is the Umpire's value where they set one, 0 included", () => {
    expect(settlementValue(place({ size: "Town", victoryPoints: 40 }))).toBe(40);
    expect(settlementValue(place({ size: "City", victoryPoints: 0 }))).toBe(0);
    expect(settlementValue(place({ size: "City", victoryPoints: null }))).toBe(25);
  });

  it("is nothing without the Umpire's value, where only those they give points count", () => {
    expect(settlementValue(place({ size: "City", victoryPoints: null }), "Chosen")).toBe(0);
    expect(settlementValue(place({ size: "Town", victoryPoints: 30 }), "Chosen")).toBe(30);
  });
});
