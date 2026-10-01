import { describe, expect, it } from "vitest";
import type { UnitPosition } from "@/api/generated/model";
import { describeOrder, hexes } from "@/features/maps/orders";

const from = { latitude: 50.7, longitude: 4.4 };
const order = (kind: UnitPosition["kind"], path: UnitPosition["path"] = []): UnitPosition => ({
  unitId: "u",
  armyId: "a",
  turn: 1,
  status: "Draft",
  kind,
  q: path.at(-1)?.q ?? 0,
  r: path.at(-1)?.r ?? 0,
  latitude: 50.7,
  longitude: 4.4,
  path,
  byUmpire: false,
  progress: null,
  forceMarch: false,
});

describe("describeOrder", () => {
  it("says how many hexes a Move goes", () => {
    expect(describeOrder(order("Move", [{ q: 0, r: -1 }]), from)).toBe("Moves 1 hex");
    expect(
      describeOrder(
        order("Move", [
          { q: 0, r: -1 },
          { q: 0, r: -2 },
        ]),
        from,
      ),
    ).toBe("Moves 2 hexes");
  });

  it("says how far into a hex that takes more than a turn a Move gets", () => {
    const twoSteps = order("Move", [
      { q: 0, r: -1 },
      { q: 0, r: -2 },
    ]);
    expect(describeOrder({ ...twoSteps, progress: 0.25 }, from)).toBe(
      "Moves 1 hex, and a quarter of the way into the next",
    );
    expect(describeOrder({ ...order("Move", [{ q: 0, r: -1 }]), progress: 0.5 }, from)).toBe(
      "Goes half of the way into the next",
    );
  });

  it("says a move from before the grid just moves", () => {
    expect(describeOrder(order("Move"), from)).toBe("Moves");
  });

  it("says a Hold holds, and when there's no order", () => {
    expect(describeOrder(order("Hold"), from)).toBe("Holds");
    expect(describeOrder(undefined, from)).toBe("No order yet");
  });

  it("says a unit that isn't placed isn't on the map", () => {
    expect(describeOrder(undefined, undefined)).toBe("Not on the map yet");
  });
});

describe("hexes", () => {
  it("counts in hexes", () => {
    expect(hexes(1)).toBe("1 hex");
    expect(hexes(3)).toBe("3 hexes");
  });
});
