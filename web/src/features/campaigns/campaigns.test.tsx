import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { CampaignResponse, CampaignSummary } from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testAdmin, testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";

const details = (overrides: Partial<CampaignResponse> = {}): CampaignResponse => ({
  id: campaignId,
  name: "The Peninsular War",
  description: "Wellington in Spain.",
  umpire: {
    memberId: "0192f5c1-0000-7000-8000-00000000d001",
    userId: testUser.id,
    firstName: "Mel",
    lastName: "Green",
  },
  myRole: "Umpire",
  playerCount: 2,
  createdAt: "2026-09-01T12:00:00Z",
  updatedAt: "2026-09-01T12:00:00Z",
  ...overrides,
});

function serveCampaign(campaign: CampaignResponse) {
  server.use(http.get(`*/api/campaigns/${campaignId}`, () => HttpResponse.json(campaign)));
}

describe("campaigns", () => {
  it("lists my campaigns with my role", async () => {
    const summary: CampaignSummary = {
      id: campaignId,
      name: "The Peninsular War",
      myRole: "Player",
      umpireName: "Ada Admin",
      playerCount: 3,
    };
    server.use(
      http.get("*/api/campaigns", () =>
        HttpResponse.json({ items: [summary], page: 1, pageSize: 25, totalCount: 1 }),
      ),
    );
    await renderApp("/campaigns");

    const card = (await screen.findByRole("link", { name: "The Peninsular War" })).closest(
      "article",
    );
    if (!card) throw new Error("No campaign card");
    expect(within(card).getByText("Player")).toBeInTheDocument();
    expect(within(card).getByText("Umpire: Ada Admin")).toBeInTheDocument();
    expect(within(card).getByText("3 players")).toBeInTheDocument();
  });

  it("explains what to do when there are none", async () => {
    await renderApp("/campaigns");

    expect(await screen.findByText("You're not in any campaigns yet.")).toBeInTheDocument();
  });

  it("creates a campaign and opens it", async () => {
    let sent: unknown;
    server.use(
      http.post("*/api/campaigns", async ({ request }) => {
        sent = await request.json();
        return HttpResponse.json(details(), { status: 201 });
      }),
    );
    serveCampaign(details());
    await renderApp("/campaigns");
    const user = userEvent.setup();

    await user.click(await screen.findByRole("link", { name: "New campaign" }));
    await user.type(await screen.findByRole("textbox", { name: "Name" }), " The Peninsular War ");
    await user.type(screen.getByRole("textbox", { name: "Description" }), "Wellington in Spain.");
    await user.click(screen.getByRole("button", { name: "Create campaign" }));

    expect(
      await screen.findByRole("heading", { level: 1, name: "The Peninsular War" }),
    ).toBeInTheDocument();
    expect(sent).toEqual({ name: "The Peninsular War", description: "Wellington in Spain." });
  });

  it.each([
    ["the Umpire", details(), testUser, true],
    ["a Player", details({ myRole: "Player" }), testUser, false],
    ["an Admin who isn't a member", details({ myRole: null }), testAdmin, true],
  ])("offers Edit and Delete to %s: %s", async (_who, campaign, user, offered) => {
    serveCampaign(campaign);
    await renderApp(`/campaigns/${campaignId}`, { user });

    await screen.findByRole("heading", { level: 1, name: "The Peninsular War" });
    expect(screen.queryByRole("link", { name: "Edit" }) !== null).toBe(offered);
    expect(screen.queryByRole("button", { name: "Delete campaign" }) !== null).toBe(offered);
  });

  it("edits the campaign and shows the change", async () => {
    let current = details();
    server.use(
      http.get(`*/api/campaigns/${campaignId}`, () => HttpResponse.json(current)),
      http.put(`*/api/campaigns/${campaignId}`, async ({ request }) => {
        current = { ...current, ...((await request.json()) as object) };
        return HttpResponse.json(current);
      }),
    );
    await renderApp(`/campaigns/${campaignId}/edit`);
    const user = userEvent.setup();
    const name = await screen.findByRole("textbox", { name: "Name" });

    expect(name).toHaveValue("The Peninsular War");
    await user.clear(name);
    await user.type(name, "The Hundred Days");
    await user.click(screen.getByRole("button", { name: "Save changes" }));

    expect(
      await screen.findByRole("heading", { level: 1, name: "The Hundred Days" }),
    ).toBeInTheDocument();
  });

  it("deletes the campaign after confirming", async () => {
    let deleted = false;
    serveCampaign(details());
    server.use(
      http.delete(`*/api/campaigns/${campaignId}`, () => {
        deleted = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await renderApp(`/campaigns/${campaignId}`);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Delete campaign" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Delete campaign" }),
    );

    expect(await screen.findByRole("heading", { level: 1, name: "Campaigns" })).toBeInTheDocument();
    expect(deleted).toBe(true);
  });

  it("has no detectable accessibility problems", async () => {
    serveCampaign(details());
    const { container } = await renderApp(`/campaigns/${campaignId}`);
    await screen.findByRole("heading", { level: 1, name: "The Peninsular War" });

    await expectNoAxeViolations(container);
  });
});
