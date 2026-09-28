import type {
  ArmyCommander,
  CampaignMemberResponse,
  CampaignResponse,
  MeResponse,
} from "@/api/generated/model";
import { canManage } from "@/features/campaigns/campaign-access";

/**
 * Whether the user can open an army's page: its commander, the Umpire or an Admin. Other Players
 * see the army in the list only. The API enforces the same rule.
 */
export const canViewArmy = (
  campaign: CampaignResponse,
  commander: ArmyCommander | null,
  user: MeResponse | null,
) => canManage(campaign, user) || (user !== null && commander?.userId === user.id);

/** Players who could command an army: those without one, plus the army's current commander. */
export const commanderOptions = (members: CampaignMemberResponse[], armyId?: string) =>
  members
    .filter((m) => m.role === "Player" && (m.army === null || m.army.id === armyId))
    .map((m) => ({ value: m.id, label: `${m.firstName} ${m.lastName}` }));
