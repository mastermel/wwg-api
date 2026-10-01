import { describe, expect, it } from "vitest";
import type {
  ArmySummary,
  ArmyUnitResponse,
  CampaignConcentrationResponse,
  HexSettlement,
  UnitType,
} from "@/api/generated/model";
import { usualCavalryTypes, usualInfantryTypes } from "@/features/campaigns/concentration";
import {
  afterOrders,
  depotThreats,
  describeThreat,
  describeWarning,
  hexWarnings,
  warningPlace,
  type UnitInHex,
} from "@/features/maps/contact";
import { indexTerrain, noSettlement } from "@/features/maps/terrain";

const coalition = { id: "s1", name: "Coalition" };
const french = { id: "s2", name: "French Empire" };

const army = (id: string, side: typeof coalition): ArmySummary => ({
  id,
  name: id,
  commander: null,
  side,
  color: "Blue",
  nation: "None",
});
const wellington = army("Anglo-Allied", coalition);
const blucher = army("Prussians", coalition);
const napoleon = army("Armée du Nord", french);

const usual: CampaignConcentrationResponse = {
  infantryTypes: [...usualInfantryTypes],
  cavalryTypes: [...usualCavalryTypes],
  infantryLimit: 200,
  cavalryLimit: 160,
};

let next = 0;
const unit = (
  of: ArmySummary,
  points: number,
  type: UnitType = "LineInfantry",
  hex = { q: 0, r: 0 },
): UnitInHex => {
  next += 1;
  return {
    unit: {
      id: `u${String(next)}`,
      armyId: of.id,
      unitId: "l",
      factionId: "f",
      nation: "None",
      name: `Unit ${String(next)}`,
      type,
      fightingFactor: 5,
      points,
    } satisfies ArmyUnitResponse,
    army: of,
    hex,
  };
};

const open = indexTerrain(undefined);
const settled = (settlement: Partial<HexSettlement>) =>
  indexTerrain({
    cells: [
      {
        q: 0,
        r: 0,
        terrain: "Flat",
        forest: false,
        settlement: { ...noSettlement, ...settlement },
        setByUmpire: true,
      },
    ],
    edges: [],
  });

describe("hexWarnings", () => {
  it("warns of no hex within the limits and held by one side", () => {
    expect(hexWarnings([unit(wellington, 100), unit(blucher, 100)], usual, open)).toEqual([]);
  });

  it("finds contact where both sides are in a hex", () => {
    const [warning] = hexWarnings([unit(wellington, 20), unit(napoleon, 20)], usual, open);

    expect(warning).toMatchObject({ hex: { q: 0, r: 0 }, contact: true });
    expect(describeWarning(warning)).toEqual(["Contact: Coalition and French Empire."]);
  });

  it("counts a side's armies together, over the infantry limit", () => {
    const [warning] = hexWarnings(
      [unit(wellington, 100), unit(blucher, 60), unit(blucher, 50, "FootArtillery")],
      usual,
      open,
    );

    expect(warning).toMatchObject({ contact: false, infantryLimit: 200 });
    expect(warning.sides[0]).toMatchObject({ infantryLimit: 200, offTheLand: false });
    expect(describeWarning(warning)).toEqual(["Coalition has 210 points of infantry, over 200."]);
  });

  it("judges infantry and cavalry each on its own, and leaves free types out", () => {
    const units = [
      unit(napoleon, 150),
      unit(napoleon, 100, "HeavyCavalry"),
      unit(napoleon, 70, "HorseArtillery"),
      unit(napoleon, 100, "SupplyTrain"),
    ];

    expect(describeWarning(hexWarnings(units, usual, open)[0])).toEqual([
      "French Empire has 170 points of cavalry, over 160.",
    ]);
  });

  it("goes by the campaign's own types and limits", () => {
    const own = {
      ...usual,
      infantryTypes: [...usual.infantryTypes, "SupplyTrain" as const],
      infantryLimit: 100,
    };

    expect(
      hexWarnings([unit(napoleon, 60), unit(napoleon, 50, "SupplyTrain")], own, open),
    ).toHaveLength(1);
  });

  it("doubles the limits in a City or a fortress, not in a walled town", () => {
    const crowded = [unit(napoleon, 300)];

    expect(hexWarnings(crowded, usual, settled({ size: "City" }))).toEqual([]);
    expect(hexWarnings(crowded, usual, settled({ fortress: true }))).toEqual([]);
    expect(hexWarnings(crowded, usual, settled({ size: "Town", walled: true }))).toHaveLength(1);
    expect(
      describeWarning(hexWarnings([unit(napoleon, 401)], usual, settled({ size: "City" }))[0]),
    ).toEqual(["French Empire has 401 points of infantry, over 400 (doubled here)."]);
  });

  it("halves a side's limits in a hex where any of its units lives off the land", () => {
    const living = { ...unit(napoleon, 60, "HeavyCavalry"), livesOffTheLand: true };
    const [warning] = hexWarnings([unit(napoleon, 110), living], usual, open);

    expect(describeWarning(warning)).toEqual([
      "French Empire has 110 points of infantry, over 100 (halved: living off the land).",
    ]);
    // The other side in the hex keeps its full limits.
    expect(hexWarnings([unit(wellington, 150)], usual, open)).toEqual([]);
  });

  it("carries a unit's living off the land with its order", () => {
    const moving = unit(napoleon, 150);
    const [warning] = hexWarnings(
      afterOrders([moving], [{ unitId: moving.unit.id, q: 0, r: 0, livesOffTheLand: true }]),
      usual,
      open,
    );

    expect(warning.sides[0]?.offTheLand).toBe(true);
  });

  it("lists the hexes north to south, then west to east", () => {
    const at = (q: number, r: number) => [
      unit(wellington, 1, "LineInfantry", { q, r }),
      unit(napoleon, 1, "LineInfantry", { q, r }),
    ];

    const warnings = hexWarnings([...at(1, 0), ...at(0, -1), ...at(-1, 0)], usual, open);

    expect(warnings.map((w) => w.hex)).toEqual([
      { q: 0, r: -1 },
      { q: -1, r: 0 },
      { q: 1, r: 0 },
    ]);
  });
});

describe("afterOrders", () => {
  it("puts each unit with an order where it's going, and leaves the rest", () => {
    const moving = unit(napoleon, 20);
    const still = unit(wellington, 20, "LineInfantry", { q: 2, r: 0 });

    const after = afterOrders(
      [moving, still],
      [{ unitId: moving.unit.id, q: 2, r: 0, livesOffTheLand: false }],
    );

    expect(hexWarnings(after, usual, open)).toMatchObject([{ hex: { q: 2, r: 0 }, contact: true }]);
  });
});

describe("warningPlace", () => {
  it("names the hex, and its settlement if it has a name", () => {
    const [warning] = hexWarnings([unit(napoleon, 401)], usual, open);

    expect(warningPlace(warning, open)).toBe("Hex (0, 0)");
    expect(warningPlace(warning, settled({ size: "Town", name: "Ligny" }))).toBe(
      "Hex (0, 0), Ligny",
    );
  });
});

describe("depotThreats", () => {
  const depot = {
    id: "d",
    armyId: napoleon.id,
    kind: "Main" as const,
    name: "Charleroi",
    q: 0,
    r: 0,
    latitude: 0,
    longitude: 0,
    cutOffTurns: 0,
  };

  it("finds a depot with the other side's units in its hex, not its own side's", () => {
    const threats = depotThreats(
      [unit(napoleon, 50), unit(wellington, 20), unit(blucher, 10, "LineInfantry", { q: 1, r: 0 })],
      [depot],
      [napoleon, wellington, blucher],
    );

    expect(threats.map(describeThreat)).toEqual([
      "Charleroi (Armée du Nord's main depot): 20 points of the other side are in its hex.",
    ]);
    expect(depotThreats([unit(napoleon, 50)], [depot], [napoleon])).toEqual([]);
  });
});
