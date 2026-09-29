import type { CampaignMemberResponse } from "@/api/generated/model";

/** Players who could command an army: those without one, plus the army's current commander. */
export const commanderOptions = (members: CampaignMemberResponse[], armyId?: string) =>
  members
    .filter((m) => m.role === "Player" && (m.army === null || m.army.id === armyId))
    .map((m) => ({ value: m.id, label: `${m.firstName} ${m.lastName}` }));
