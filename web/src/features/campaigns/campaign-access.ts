import type { CampaignResponse, MeResponse } from "@/api/generated/model";

/**
 * Whether the user can change the campaign (the Umpire, or an Admin). Only decides what the UI
 * offers; the API enforces the same rule.
 */
export const canManage = (campaign: CampaignResponse, user: MeResponse | null) =>
  campaign.myRole === "Umpire" || (user?.isAdmin ?? false);
