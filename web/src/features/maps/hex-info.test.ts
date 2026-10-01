import { describe, expect, it } from "vitest";
import type {
  ArmySummary,
  DepotResponse,
  HexEdgeResponse,
  HexDetailResponse,
} from "@/api/generated/model";
import { describeHex } from "@/features/maps/hex-info";
import { indexTerrain, noSettlement } from "@/features/maps/terrain";

const edge = (
  q: number,
  r: number,
  side: HexEdgeResponse["side"],
  changes: Partial<HexEdgeResponse>,
): HexEdgeResponse => ({
  q,
  r,
  side,
  road: "None",
  river: false,
  bridge: false,
  waterway: "None",
  setByUmpire: true,
  ...changes,
});

const nord: ArmySummary = {
  id: "a",
  name: "Armée du Nord",
  commander: null,
  side: { id: "s", name: "French Empire" },
  color: "Blue",
  nation: "France",
};

const centre = { q: 0, r: 0 };

describe("describeHex", () => {
  it("is flat ground where nothing's been set", () => {
    expect(describeHex(centre, indexTerrain(undefined), [], [], [])).toEqual({
      title: "Hex (0, 0)",
      summary: "Flat",
      lines: ["Flat."],
    });
  });

  it("gives its ground, forest, settlement and what's on each side", () => {
    const terrain = indexTerrain({
      cells: [
        {
          q: 0,
          r: 0,
          terrain: "LowHill",
          forest: true,
          settlement: { ...noSettlement, size: "Town", walled: true, name: "Wavre" },
          setByUmpire: true,
        },
      ],
      edges: [
        edge(0, 0, "N", { road: "Good" }),
        // Its S side is the N of the hex below.
        edge(0, 1, "N", { road: "Good" }),
        edge(0, 0, "NE", { river: true, bridge: true, road: "Poor" }),
        edge(0, 0, "SE", { river: true }),
        // Its SW side, stored on that neighbour, flowing out of it: into this hex.
        edge(-1, 1, "NE", { waterway: "Out" }),
      ],
    });

    const info = describeHex(centre, terrain, [], [], []);

    expect(info.title).toBe("Hex (0, 0), Wavre");
    expect(info.summary).toBe("Low hills, forest");
    expect(info.lines).toEqual([
      "Low hills, forest.",
      "Walled town: Wavre.",
      "Worth 35 points.",
      "Good road to the north and south.",
      "Poor road to the north-east.",
      "River along the north-east and south-east sides, bridged to the north-east.",
      "Waterway to the south-west, upstream.",
    ]);
  });

  it("says what a settlement is worth, and who holds it where the viewer may know", () => {
    const terrain = indexTerrain({
      cells: [
        {
          q: 0,
          r: 0,
          terrain: "Flat",
          forest: false,
          settlement: { ...noSettlement, size: "City", name: "Namur" },
          setByUmpire: true,
        },
      ],
      edges: [],
    });
    const holding = {
      q: 0,
      r: 0,
      latitude: 0,
      longitude: 0,
      name: "Namur",
      value: 25,
      armyId: nord.id,
    };

    expect(describeHex(centre, terrain, [], [], [nord]).lines).toContain("Worth 25 points.");
    expect(describeHex(centre, terrain, [], [], [nord], [holding]).lines).toContain(
      "Worth 25 points; held by Armée du Nord.",
    );
    expect(describeHex(centre, terrain, [], [], [nord], [], "Chosen").lines).not.toContainEqual(
      expect.stringMatching(/^Worth/),
    );
  });

  it("adds the actual terrain the viewer was shown, and the depots they may see", () => {
    const detail: HexDetailResponse = {
      q: 0,
      r: 0,
      relief: "Rolling",
      features: {
        scrub: false,
        village: true,
        woods: false,
        forest: false,
        farms: false,
        fields: false,
        streams: false,
      },
      dominant: "SmallCastle",
      favorability: "NotRolled",
      dice: null,
      forArmyId: null,
      shownToArmyIds: [],
      shownToAll: true,
    };
    const depot: DepotResponse = {
      id: "d",
      armyId: nord.id,
      kind: "Main",
      name: "Fleurus",
      q: 0,
      r: 0,
      latitude: 0,
      longitude: 0,
      cutOffTurns: 0,
    };

    const info = describeHex(centre, indexTerrain(undefined), [detail], [depot], [nord]);

    expect(info.lines.slice(1)).toEqual([
      "Actual terrain: Rolling: a small village. A small castle.",
      "Fleurus: Armée du Nord's main depot.",
    ]);
  });
});
