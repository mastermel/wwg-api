import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { CampaignResponse, FactionResponse } from "@/api/generated/model";
import { renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";
const coalition: FactionResponse = {
  id: "0192f5c1-0000-7000-8000-00000000f001",
  name: "Coalition",
  armyCount: 2,
};

/** A campaign with the given factions, which change as the test adds and deletes them. */
function serveCampaign(myRole: CampaignResponse["myRole"], initial: FactionResponse[]) {
  let factions = initial;
  const deleted: string[] = [];
  server.use(
    http.get(`*/api/campaigns/${campaignId}`, () =>
      HttpResponse.json({
        id: campaignId,
        name: "The Peninsular War",
        description: null,
        umpire: {
          memberId: "0192f5c1-0000-7000-8000-00000000d001",
          userId: testUser.id,
          firstName: "Mel",
          lastName: "Green",
        },
        myRole,
        playerCount: 0,
        createdAt: "2026-09-01T12:00:00Z",
        updatedAt: "2026-09-01T12:00:00Z",
      } satisfies CampaignResponse),
    ),
    http.get(`*/api/campaigns/${campaignId}/factions`, () => HttpResponse.json(factions)),
    http.post(`*/api/campaigns/${campaignId}/factions`, async ({ request }) => {
      const { name } = (await request.json()) as { name: string };
      const added = { id: "0192f5c1-0000-7000-8000-00000000f002", name, armyCount: 0 };
      factions = [...factions, added];
      return HttpResponse.json(added, { status: 201 });
    }),
    http.delete("*/api/factions/:id", ({ params }) => {
      deleted.push(String(params.id));
      factions = factions.filter((f) => f.id !== params.id);
      return new HttpResponse(null, { status: 204 });
    }),
  );
  return deleted;
}

const factionsSection = async () => within(await screen.findByRole("region", { name: "Factions" }));

describe("factions", () => {
  it("lists the factions and their armies for everyone, without the Umpire's buttons", async () => {
    serveCampaign("Player", [coalition]);
    await renderApp(`/campaigns/${campaignId}`);
    const section = await factionsSection();

    expect(await section.findByText("Coalition")).toBeInTheDocument();
    expect(section.getByText("2 armies")).toBeInTheDocument();
    expect(section.queryByRole("button")).not.toBeInTheDocument();
  });

  it("says what to do when there are none", async () => {
    serveCampaign("Umpire", []);
    await renderApp(`/campaigns/${campaignId}`);

    expect(await (await factionsSection()).findByText("No factions yet")).toBeInTheDocument();
  });

  it("lets the Umpire add a faction", async () => {
    serveCampaign("Umpire", [coalition]);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}`);
    const section = await factionsSection();

    await user.click(section.getByRole("button", { name: "New faction" }));
    const dialog = within(await screen.findByRole("dialog"));
    await user.type(dialog.getByRole("textbox", { name: "Name" }), " French Empire ");
    await user.click(dialog.getByRole("button", { name: "Add faction" }));

    expect(await screen.findByText("Added French Empire.")).toBeInTheDocument();
    expect(await section.findByText("French Empire")).toBeInTheDocument();
  });

  it("warns that deleting a faction leaves its armies unassigned", async () => {
    const deleted = serveCampaign("Umpire", [coalition]);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}`);
    const section = await factionsSection();

    await user.click(await section.findByRole("button", { name: "Delete Coalition" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(dialog.getByText(/its 2 armies left Unassigned/)).toBeInTheDocument();
    await user.click(dialog.getByRole("button", { name: "Delete faction" }));

    expect(await screen.findByText("Deleted Coalition.")).toBeInTheDocument();
    expect(deleted).toEqual([coalition.id]);
  });
});
