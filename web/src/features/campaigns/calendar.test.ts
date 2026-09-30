import { describe, expect, it } from "vitest";
import { formatDay, turnWhen } from "@/features/campaigns/calendar";

describe("turnWhen", () => {
  it("says a turn's day and time of day, as the rule book writes them", () => {
    expect(turnWhen({ part: "Afternoon", date: "1815-06-17" })).toBe("17 June 1815, Afternoon");
    expect(turnWhen({ part: "Night", date: null })).toBe("Night");
    expect(turnWhen({ part: null, date: null })).toBeNull();
  });

  it("keeps the day it's given, whatever the time zone", () => {
    expect(formatDay("1815-01-01")).toBe("1 January 1815");
  });
});
