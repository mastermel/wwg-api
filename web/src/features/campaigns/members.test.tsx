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
  army: null,
};

const player: CampaignMemberResponse = {
  id: playerMemberId,
  userId: "0192f5c1-0000-7000-8000-0000000000u2",
  firstName: "Arthur",
  lastName: "Wellesley",
  role: "Player",
  joinedAt: "2026-09-02T12:00:00Z",
  army: null,
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
    // Cells: name (with the role under it, for phones), role, army.
    expect(
      rows.map((row) =>
        within(row)
          .getAllByRole("cell")
          .map((cell) => cell.textContent),
      ),
    ).toEqual([
      ["Ada AdminUmpire", "Umpire", "–"],
      ["Mel Green (you)Player", "Player", "None"],
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

  it("refetches the armies after removing a Player (their army is now unassigned)", async () => {
    serveCampaign("Umpire", [me("Umpire"), player]);
    let armyRequests = 0;
    server.use(
      http.get(`*/api/campaigns/${campaignId}/armies`, () => {
        armyRequests++;
        return HttpResponse.json([]);
      }),
    );
    await openCampaign();
    const table = await membersTable();
    await waitFor(() => {
      expect(armyRequests).toBe(1);
    });

    await userEvent.click(within(table).getByRole("button", { name: "Remove Arthur Wellesley" }));
    await userEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Remove" }),
    );

    await waitFor(() => {
      expect(armyRequests).toBe(2);
    });
  });

  it("shows why leaving failed", async () => {
    serveCampaign("Player", [umpire, me("Player")]);
    server.use(
      http.delete(`*/api/campaigns/${campaignId}/members/me`, () =>
        HttpResponse.json(
          { status: 409, title: "Conflict", detail: "Hand over the army first." },
          { status: 409, headers: { "Content-Type": "application/problem+json" } },
        ),
      ),
    );
    await openCampaign();

    await userEvent.click(screen.getByRole("button", { name: "Leave campaign" }));
    await userEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Leave campaign" }),
    );

    expect(await screen.findByText("Hand over the army first.")).toBeInTheDocument();
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
