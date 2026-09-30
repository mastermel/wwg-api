import { describe, expect, it } from "vitest";
import { describeDetail, noFeatures } from "@/features/maps/hex-detail";

describe("describeDetail", () => {
  it("puts the lie of the land, what's there, the dominant feature and the ground in words", () => {
    expect(
      describeDetail({
        relief: "Rolling",
        features: { ...noFeatures, village: true, woods: true },
        dominant: "SmallCastle",
        favorability: "Favorable",
      }),
    ).toBe("Rolling: a small village and small woods. A small castle. Favourable ground.");
    expect(
      describeDetail({
        relief: "Hilly",
        features: { ...noFeatures, fields: true, farms: true, streams: true },
        dominant: "None",
        favorability: "NotRolled",
      }),
    ).toBe("Hilly: farms, fields and streams.");
    expect(
      describeDetail({
        relief: "Flat",
        features: noFeatures,
        dominant: "None",
        favorability: "Neutral",
      }),
    ).toBe("Flat. Neutral ground.");
  });
});
