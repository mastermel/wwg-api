import { describe, expect, it } from "vitest";
import type { SightingDueResponse } from "@/api/generated/model";
import { initialEntries, toRequests } from "@/features/maps/sighting-entries";

const due = (afloat: boolean): SightingDueResponse => ({
  observingArmyId: "a",
  q: 1,
  r: 0,
  whereabouts: "1 hex east of the Guard",
  screened: false,
  units: [{ unitId: "u", armyId: "p", name: "Brigade", type: "LineInfantry", points: 30, afloat }],
});

describe("a sighting of a force on boats (step 51)", () => {
  it("shows the boats unless the Umpire leaves them out", () => {
    const entries = initialEntries([due(true)]);
    expect(entries.map((e) => e.showsAfloat)).toEqual([true]);
    expect(toRequests(entries).map((r) => r.showsAfloat)).toEqual([true]);
    expect(
      toRequests(entries.map((e) => ({ ...e, showsAfloat: false }))).map((r) => r.showsAfloat),
    ).toEqual([false]);
  });

  it("never says anything of boats for a force ashore", () => {
    const entries = initialEntries([due(false)]).map((e) => ({ ...e, showsAfloat: true }));
    expect(toRequests(entries).map((r) => r.showsAfloat)).toEqual([false]);
  });
});
