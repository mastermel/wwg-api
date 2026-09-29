import type { Query, QueryClient } from "@tanstack/react-query";

/**
 * Cache updates after a change inside a campaign. The API spreads a campaign over several queries
 * (details, members, armies, each army's details, the campaign lists), and a change to one often
 * changes others: removing a Player unassigns their army, and a new Umpire changes the members
 * and armies. So every change refreshes the whole campaign rather than guessing which queries it
 * touched. Query keys are the request paths (Orval), which is what these match on.
 */

/** Refetches everything about the campaign, and the campaign lists (and admin views). */
export function refreshCampaign(queryClient: QueryClient, campaignId: string) {
  return queryClient.invalidateQueries({
    predicate: (query) => belongsTo(query, campaignId) || isList(query),
  });
}

/**
 * After the campaign is deleted, or the user leaves it: drops everything saved about it (so it
 * isn't shown offline either), and refetches the campaign lists. Call it after navigating away,
 * or the open page refetches and shows "not found".
 */
export function forgetCampaign(queryClient: QueryClient, campaignId: string) {
  queryClient.removeQueries({ predicate: (query) => belongsTo(query, campaignId) });
  return queryClient.invalidateQueries({ predicate: isList });
}

function belongsTo(query: Query, campaignId: string) {
  const [path] = query.queryKey;
  if (typeof path !== "string") return false;
  if (path.startsWith(`/api/campaigns/${campaignId}`)) return true;
  // An army's details are keyed by the army, but say which campaign they're in.
  return path.startsWith("/api/armies/") && armyCampaignId(query.state.data) === campaignId;
}

/** My campaigns, and the admin views (every campaign, a user's campaigns and roles). */
function isList(query: Query) {
  const [path] = query.queryKey;
  return typeof path === "string" && (path === "/api/campaigns" || path.startsWith("/api/admin/"));
}

function armyCampaignId(data: unknown) {
  return typeof data === "object" && data !== null && "campaignId" in data
    ? data.campaignId
    : undefined;
}
