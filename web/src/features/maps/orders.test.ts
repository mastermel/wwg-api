import { describe, expect, it } from "vitest";
import type { UnitPosition } from "@/api/generated/model";
import { describeOrder, formatDistance } from "@/features/maps/orders";

const from = { latitude: 50.7, longitude: 4.4 };
const order = (kind: UnitPosition["kind"], latitude = 50.7): UnitPosition => ({
  unitId: "u",
  armyId: "a",
  turn: 1,
  status: "Draft",
  kind,
  latitude,
  longitude: 4.4,
});

describe("describeOrder", () => {
  it("says how far a Move goes, in the campaign's unit", () => {
    expect(describeOrder(order("Move", 50.73), from, "Kilometres")).toBe("Moves 3.3 km");
    expect(describeOrder(order("Move", 50.73), from, "Miles")).toBe("Moves 2.1 mi");
  });

  it("says a Hold holds, and when there's no order", () => {
    expect(describeOrder(order("Hold"), from, "Kilometres")).toBe("Holds");
    expect(describeOrder(undefined, from, "Kilometres")).toBe("No order yet");
  });

  it("says a unit that isn't placed isn't on the map", () => {
    expect(describeOrder(undefined, undefined, "Kilometres")).toBe("Not on the map yet");
  });
});

describe("formatDistance", () => {
  it("rounds to a tenth", () => {
    expect(formatDistance(12_345, "Kilometres")).toBe("12.3 km");
  });
});
