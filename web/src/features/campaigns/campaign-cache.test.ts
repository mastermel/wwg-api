import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import { forgetCampaign, refreshCampaign } from "@/features/campaigns/campaign-cache";

const campaign = "0192f5c1-0000-7000-8000-00000000c001";
const other = "0192f5c1-0000-7000-8000-00000000c002";

/** A cache holding one campaign's queries, another campaign's, and the lists. */
function seededClient() {
  const client = new QueryClient();
  const seed = (path: string, data: unknown = {}) => {
    client.setQueryData([path], data);
  };
  seed(`/api/campaigns/${campaign}`);
  seed(`/api/campaigns/${campaign}/members`, []);
  seed(`/api/campaigns/${campaign}/armies`, []);
  seed(`/api/campaigns/${campaign}/join-code`);
  seed("/api/armies/army-1", { campaignId: campaign });
  seed(`/api/campaigns/${other}`);
  seed("/api/armies/army-2", { campaignId: other });
  seed("/api/campaigns", { items: [] });
  seed("/api/admin/campaigns", { items: [] });
  seed("/api/about");
  return client;
}

const ours = [
  `/api/campaigns/${campaign}`,
  `/api/campaigns/${campaign}/members`,
  `/api/campaigns/${campaign}/armies`,
  `/api/campaigns/${campaign}/join-code`,
  "/api/armies/army-1",
];
const theirs = [`/api/campaigns/${other}`, "/api/armies/army-2", "/api/about"];
const lists = ["/api/campaigns", "/api/admin/campaigns"];

const isInvalidated = (client: QueryClient, path: string) =>
  client.getQueryState([path])?.isInvalidated;

describe("campaign cache", () => {
  it("refreshes everything about the campaign, and the lists, but not other campaigns", async () => {
    const client = seededClient();

    await refreshCampaign(client, campaign);

    for (const path of [...ours, ...lists]) expect(isInvalidated(client, path), path).toBe(true);
    for (const path of theirs) expect(isInvalidated(client, path), path).toBe(false);
  });

  it("forgets everything about the campaign, and refreshes the lists", async () => {
    const client = seededClient();

    await forgetCampaign(client, campaign);

    for (const path of ours) expect(client.getQueryData([path]), path).toBeUndefined();
    for (const path of theirs) expect(client.getQueryData([path]), path).toBeDefined();
    for (const path of lists) expect(isInvalidated(client, path), path).toBe(true);
  });
});
