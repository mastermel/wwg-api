import type { MeResponse } from "@/api/generated/model";

/** Managers and Admins edit the library (decision 0015); everyone signed in can view it. */
export const canEditLibrary = (user: MeResponse | null | undefined) =>
  Boolean(user && (user.isAdmin || user.isManager));

export const unitCount = (count: number) => (count === 1 ? "1 unit" : `${String(count)} units`);
