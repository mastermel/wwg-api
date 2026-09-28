import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { CampaignMemberResponse, CampaignResponse } from "@/api/generated/model";
import { renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";
const playerMemberId = "0192f5c1-0000-7000-8000-00000000d002";

const umpire: CampaignMemberResponse = {
  id: "0192f5c1-0000-7000-8000-00000000d001",
  userId: "0192f5c1-0000-7000-8000-0000000000u1",
  firstName: "Ada",
  lastName: "Admin",
  role: "Umpire",
  joinedAt: "2026-09-01T12:00:00Z",
};

const player: CampaignMemberResponse = {
  id: playerMemberId,
  userId: "0192f5c1-0000-7000-8000-0000000000u2",
  firstName: "Arthur",
  lastName: "Wellesley",
  role: "Player",
  joinedAt: "2026-09-02T12:00:00Z",
};

const me = (role: CampaignMemberResponse["role"]): CampaignMemberResponse => ({
  ...(role === "Umpire" ? umpire : player),
  userId: testUser.id,
  firstName: testUser.firstName,
  lastName: testUser.lastName,
});

function serveCampaign(myRole: CampaignResponse["myRole"], members: CampaignMemberResponse[]) {
  const deleted: string[] = [];
  server.use(
    http.get(`*/api/campaigns/${campaignId}`, () =>
      HttpResponse.json({
        id: campaignId,
        name: "The Peninsular War",
        description: null,
        umpire: null,
        myRole,
        playerCount: members.filter((m) => m.role === "Player").length,
        createdAt: "2026-09-01T12:00:00Z",
        updatedAt: "2026-09-01T12:00:00Z",
      } satisfies CampaignResponse),
    ),
    http.get(`*/api/campaigns/${campaignId}/members`, () => HttpResponse.json(members)),
    http.delete(`*/api/campaigns/${campaignId}/members/:memberId`, ({ params }) => {
      deleted.push(String(params.memberId));
      return new HttpResponse(null, { status: 204 });
    }),
  );
  return deleted;
}

const openCampaign = async () => {
  const result = await renderApp(`/campaigns/${campaignId}`);
  await screen.findByRole("heading", { level: 1, name: "The Peninsular War" });
  return result;
};

const membersTable = async () =>
  within(await screen.findByRole("region", { name: "Members" })).findByRole("table");

describe("campaign members", () => {
  it("lists the members and marks me", async () => {
    serveCampaign("Player", [umpire, me("Player")]);
    await openCampaign();

    const rows = within(await membersTable())
      .getAllByRole("row")
      .slice(1);
    expect(rows.map((row) => row.textContent)).toEqual([
      "Ada AdminUmpire",
      "Mel Green (you)Player",
    ]);
  });

  it("lets the Umpire remove a Player, but not themselves", async () => {
    const deleted = serveCampaign("Umpire", [me("Umpire"), player]);
    await openCampaign();
    const table = await membersTable();

    expect(within(table).getAllByRole("button")).toHaveLength(1);
    await userEvent.click(within(table).getByRole("button", { name: "Remove Arthur Wellesley" }));
    await userEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Remove" }),
    );

    expect(await screen.findByText("Removed Arthur Wellesley.")).toBeInTheDocument();
    expect(deleted).toEqual([playerMemberId]);
  });

  it("doesn't offer Players removal or the join link", async () => {
    serveCampaign("Player", [umpire, me("Player")]);
    await openCampaign();

    expect(within(await membersTable()).queryByRole("button")).not.toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "Join link" })).not.toBeInTheDocument();
  });

  it("lets a Player leave, back to the campaign list", async () => {
    serveCampaign("Player", [umpire, me("Player")]);
    let left = false;
    server.use(
      http.delete(`*/api/campaigns/${campaignId}/members/me`, () => {
        left = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const { router } = await openCampaign();

    await userEvent.click(screen.getByRole("button", { name: "Leave campaign" }));
    await userEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Leave campaign" }),
    );

    expect(await screen.findByText("You left The Peninsular War.")).toBeInTheDocument();
    await waitFor(() => {
      expect(router.state.location.pathname).toBe("/campaigns");
    });
    expect(left).toBe(true);
  });

  it("doesn't offer the Umpire leaving", async () => {
    serveCampaign("Umpire", [me("Umpire")]);
    await openCampaign();

    expect(screen.queryByRole("button", { name: "Leave campaign" })).not.toBeInTheDocument();
  });
});

describe("join link", () => {
  it("shows the Umpire the link, and replaces it when asked", async () => {
    serveCampaign("Umpire", [me("Umpire")]);
    server.use(
      http.post(`*/api/campaigns/${campaignId}/join-code`, () =>
        HttpResponse.json({ joinCode: "a-new-code" }),
      ),
    );
    await openCampaign();
    const section = await screen.findByRole("region", { name: "Join link" });
    const link = await within(section).findByRole("textbox", {
      name: "Send this link to your Players",
    });

    expect(link).toHaveValue(`${window.location.origin}/join/test-join-code`);
    await userEvent.click(within(section).getByRole("button", { name: "Make a new link" }));
    await userEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Make a new link" }),
    );

    await waitFor(() => {
      expect(link).toHaveValue(`${window.location.origin}/join/a-new-code`);
    });
  });
});
