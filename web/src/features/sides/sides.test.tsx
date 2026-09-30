import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { CampaignResponse, SideResponse } from "@/api/generated/model";
import { renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";
const coalition: SideResponse = {
  id: "0192f5c1-0000-7000-8000-00000000f001",
  name: "Coalition",
  armyCount: 2,
};

/** A campaign with the given sides, which change as the test adds and deletes them. */
function serveCampaign(myRole: CampaignResponse["myRole"], initial: SideResponse[]) {
  let sides = initial;
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
    http.get(`*/api/campaigns/${campaignId}/sides`, () => HttpResponse.json(sides)),
    http.post(`*/api/campaigns/${campaignId}/sides`, async ({ request }) => {
      const { name } = (await request.json()) as { name: string };
      const added = { id: "0192f5c1-0000-7000-8000-00000000f002", name, armyCount: 0 };
      sides = [...sides, added];
      return HttpResponse.json(added, { status: 201 });
    }),
    http.delete("*/api/sides/:id", ({ params }) => {
      deleted.push(String(params.id));
      sides = sides.filter((f) => f.id !== params.id);
      return new HttpResponse(null, { status: 204 });
    }),
  );
  return deleted;
}

const sidesSection = async () => within(await screen.findByRole("region", { name: "Sides" }));

describe("sides", () => {
  it("lists the sides and their armies for everyone, without the Umpire's buttons", async () => {
    serveCampaign("Player", [coalition]);
    await renderApp(`/campaigns/${campaignId}`);
    const section = await sidesSection();

    expect(await section.findByText("Coalition")).toBeInTheDocument();
    expect(section.getByText("2 armies")).toBeInTheDocument();
    expect(section.queryByRole("button")).not.toBeInTheDocument();
  });

  it("says what to do when there are none", async () => {
    serveCampaign("Umpire", []);
    await renderApp(`/campaigns/${campaignId}`);

    expect(await (await sidesSection()).findByText("No sides yet")).toBeInTheDocument();
  });

  it("lets the Umpire add a side", async () => {
    serveCampaign("Umpire", [coalition]);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}`);
    const section = await sidesSection();

    await user.click(section.getByRole("button", { name: "New side" }));
    const dialog = within(await screen.findByRole("dialog"));
    await user.type(dialog.getByRole("textbox", { name: "Name" }), " French Empire ");
    await user.click(dialog.getByRole("button", { name: "Add side" }));

    expect(await screen.findByText("Added French Empire.")).toBeInTheDocument();
    expect(await section.findByText("French Empire")).toBeInTheDocument();
  });

  it("warns that deleting a side leaves its armies unassigned", async () => {
    const deleted = serveCampaign("Umpire", [coalition]);
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}`);
    const section = await sidesSection();

    await user.click(await section.findByRole("button", { name: "Delete Coalition" }));
    const dialog = within(await screen.findByRole("dialog"));
    expect(dialog.getByText(/its 2 armies left Unassigned/)).toBeInTheDocument();
    await user.click(dialog.getByRole("button", { name: "Delete side" }));

    expect(await screen.findByText("Deleted Coalition.")).toBeInTheDocument();
    expect(deleted).toEqual([coalition.id]);
  });
});
