import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type {
  AdminCampaignSummary,
  CampaignResponse,
  UserDetails,
  UserSummary,
} from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testAdmin, testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";

const summaries: AdminCampaignSummary[] = [
  {
    id: campaignId,
    name: "The Peninsular War",
    umpireName: null,
    playerCount: 2,
    createdAt: "2026-09-01T12:00:00Z",
  },
  {
    id: "0192f5c1-0000-7000-8000-00000000c002",
    name: "Waterloo",
    umpireName: "Mel Green",
    playerCount: 0,
    createdAt: "2026-09-02T12:00:00Z",
  },
];

function serveList() {
  const requests: URLSearchParams[] = [];
  server.use(
    http.get("*/api/admin/campaigns", ({ request }) => {
      const params = new URL(request.url).searchParams;
      requests.push(params);
      const items =
        params.get("withoutUmpire") === "true"
          ? summaries.filter((c) => c.umpireName === null)
          : summaries;
      return HttpResponse.json({ items, page: 1, pageSize: 25, totalCount: items.length });
    }),
  );
  return requests;
}

const campaign = (overrides: Partial<CampaignResponse> = {}): CampaignResponse => ({
  id: campaignId,
  name: "The Peninsular War",
  description: null,
  umpire: null,
  myRole: null,
  playerCount: 2,
  createdAt: "2026-09-01T12:00:00Z",
  updatedAt: "2026-09-01T12:00:00Z",
  ...overrides,
});

describe("admin campaigns", () => {
  it("lists every campaign, and flags those without an Umpire", async () => {
    serveList();
    await renderApp("/admin/campaigns", { user: testAdmin });

    const row = (await screen.findByRole("link", { name: "The Peninsular War" })).closest("tr");
    if (!row) throw new Error("No row");
    expect(within(row).getByText("None")).toBeInTheDocument();
    expect(screen.getByText("2 campaigns")).toBeInTheDocument();
  });

  it("filters to campaigns without an Umpire, in the URL", async () => {
    const requests = serveList();
    const { router } = await renderApp("/admin/campaigns", { user: testAdmin });
    await screen.findByRole("link", { name: "Waterloo" });

    await userEvent.click(screen.getByRole("checkbox", { name: "Only those without an Umpire" }));

    await waitFor(() => {
      expect(screen.queryByRole("link", { name: "Waterloo" })).not.toBeInTheDocument();
    });
    expect(router.state.location.search).toEqual({ withoutUmpire: true });
    expect(requests.at(-1)?.get("withoutUmpire")).toBe("true");
  });

  it("isn't there for non-admins", async () => {
    await renderApp("/admin/campaigns");

    expect(
      await screen.findByRole("heading", { level: 1, name: /not found/i }),
    ).toBeInTheDocument();
  });

  it("has no detectable accessibility problems", async () => {
    serveList();
    const { container } = await renderApp("/admin/campaigns", { user: testAdmin });
    await screen.findByRole("link", { name: "Waterloo" });

    await expectNoAxeViolations(container);
  });
});

describe("set Umpire", () => {
  const arthur: UserSummary = {
    id: "0192f5c1-0000-7000-8000-0000000000a2",
    email: "arthur@example.com",
    firstName: "Arthur",
    lastName: "Wellesley",
    isAdmin: false,
    isManager: false,
    createdAt: "2026-09-01T12:00:00Z",
  };

  it("lets an Admin choose any user as the Umpire", async () => {
    const bodies: unknown[] = [];
    // Like the API: once set, the campaign comes back with its new Umpire.
    let current = campaign();
    server.use(
      http.get(`*/api/campaigns/${campaignId}`, () => HttpResponse.json(current)),
      http.get("*/api/admin/users", () =>
        HttpResponse.json({ items: [arthur], page: 1, pageSize: 20, totalCount: 1 }),
      ),
      http.put(`*/api/admin/campaigns/${campaignId}/umpire`, async ({ request }) => {
        bodies.push(await request.json());
        current = campaign({
          umpire: {
            memberId: "0192f5c1-0000-7000-8000-00000000d009",
            userId: arthur.id,
            firstName: "Arthur",
            lastName: "Wellesley",
          },
        });
        return HttpResponse.json(current);
      }),
    );
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}`, { user: testAdmin });

    await user.click(await screen.findByRole("button", { name: "Set Umpire" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByRole("combobox", { name: "New Umpire" }), "Wel");
    await user.click(
      await within(dialog).findByRole("option", {
        name: "Arthur Wellesley (arthur@example.com)",
        // Mantine's dropdown transition leaves it display: none in jsdom.
        hidden: true,
      }),
    );
    await user.click(within(dialog).getByRole("button", { name: "Set Umpire" }));

    expect(await screen.findByText("Arthur Wellesley is now the Umpire.")).toBeInTheDocument();
    expect(bodies).toEqual([{ userId: arthur.id }]);
    expect(await screen.findByText(/Umpire: Arthur Wellesley/)).toBeInTheDocument();
  });

  it("isn't offered to the campaign's own Umpire", async () => {
    server.use(
      http.get(`*/api/campaigns/${campaignId}`, () =>
        HttpResponse.json(campaign({ myRole: "Umpire" })),
      ),
    );
    await renderApp(`/campaigns/${campaignId}`, { user: testUser });
    await screen.findByRole("link", { name: "Edit" });

    expect(screen.queryByRole("button", { name: /Umpire$/ })).not.toBeInTheDocument();
  });
});

describe("admin user details", () => {
  it("lists the user's campaigns and roles", async () => {
    const details: UserDetails = {
      ...testUser,
      createdAt: "2026-09-01T12:00:00Z",
      lockedOutUntil: null,
      campaigns: [{ id: campaignId, name: "The Peninsular War", role: "Umpire" }],
    };
    server.use(http.get("*/api/admin/users/:id", () => HttpResponse.json(details)));
    await renderApp(`/admin/users/${testUser.id}`, { user: testAdmin });

    const section = await screen.findByRole("region", { name: "Campaigns" });
    expect(within(section).getByRole("link", { name: "The Peninsular War" })).toHaveAttribute(
      "href",
      `/campaigns/${campaignId}`,
    );
    expect(within(section).getByText("Umpire")).toBeInTheDocument();
  });
});
