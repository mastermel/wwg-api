import { describe, expect, it } from "vitest";
import type { ArmySummary, SightingResponse } from "@/api/generated/model";
import {
  describeSighting,
  describeTypes,
  sightingsFor,
  suggestedSize,
} from "@/features/maps/sightings";

const prussians: ArmySummary = {
  id: "p",
  name: "Prussian I Corps",
  commander: null,
  side: { id: "s2", name: "Coalition" },
  color: "Red",
  nation: "Prussia",
};

const sighting = (changes: Partial<SightingResponse>): SightingResponse => ({
  id: "s",
  observingArmyId: "a",
  turn: 3,
  q: 1,
  r: -2,
  latitude: 50.7,
  longitude: 4.4,
  whereabouts: "2 hexes north of Imperial Guard",
  armyIds: [prussians.id],
  unitTypes: ["LineInfantry", "LineInfantry", "LightCavalry"],
  strength: "Rough",
  afloat: null,
  size: "Medium",
  points: null,
  byUmpire: false,
  sharedByArmyId: null,
  ...changes,
});

describe("describeSighting", () => {
  it("says where, whose, what and how strong", () => {
    expect(describeSighting(sighting({}), [prussians])).toBe(
      "Hex (1, −2): Prussian I Corps: 2 line infantry and 1 light cavalry, a medium force.",
    );
  });

  it("gives only roughly where, and nothing more, when that's all that was seen", () => {
    expect(
      describeSighting(
        sighting({
          q: null,
          r: null,
          armyIds: null,
          unitTypes: null,
          strength: "Hidden",
          size: null,
        }),
        [],
      ),
    ).toBe("2 hexes north of Imperial Guard: Enemy troops.");
  });

  it("says where a sighting came from: an ally's report, or the Umpire", () => {
    expect(
      describeSighting(sighting({ strength: "Exact", points: 60, sharedByArmyId: "p" }), [
        prussians,
      ]),
    ).toMatch(/, 60 points \(from Prussian I Corps's report\)\.$/);
    expect(describeSighting(sighting({ byUmpire: true }), [prussians])).toMatch(/\(reported\)\.$/);
  });
});

describe("sightingsFor", () => {
  it("draws a turn's own in full, and the 3 turns' before faded", () => {
    const all = [1, 2, 3, 4, 5].map((turn) => sighting({ id: String(turn), turn }));

    expect(sightingsFor(all, 5).map((s) => [s.sighting.turn, s.faded])).toEqual([
      [2, true],
      [3, true],
      [4, true],
      [5, false],
    ]);
    expect(sightingsFor(all, 1).map((s) => s.sighting.turn)).toEqual([1]);
  });
});

describe("suggestedSize and describeTypes", () => {
  it("offer a size by the points, and count the types", () => {
    expect([20, 100, 300].map(suggestedSize)).toEqual(["Small", "Medium", "Large"]);
    expect(describeTypes(["Scouts"])).toBe("1 scouts");
  });
});
