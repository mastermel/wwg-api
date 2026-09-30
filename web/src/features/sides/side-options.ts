import type { SideResponse } from "@/api/generated/model";

/** The campaign's sides, as options for choosing one. */
export const sideOptions = (sides: readonly SideResponse[] | undefined) =>
  (sides ?? []).map((side) => ({ value: side.id, label: side.name }));
