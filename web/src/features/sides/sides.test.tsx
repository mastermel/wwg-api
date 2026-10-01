import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { CampaignResponse, SideResponse } from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";
const coalition: SideResponse = {
  id: "0192f5c1-0000-7000-8000-00000000f001",
  name: "Coalition",
  armyCount: 2,
};
const empire: SideResponse = {
  id: "0192f5c1-0000-7000-8000-00000000f002",
  name: "French Empire",
  armyCount: 1,
};

/** A campaign with its two sides, renamed as the test renames them; records the renames. */
function serveCampaign(myRole: CampaignResponse["myRole"]) {
  let sides = [coalition, empire];
  const renamed: unknown[] = [];
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
    http.put("*/api/sides/:id", async ({ params, request }) => {
      const { name } = (await request.json()) as { name: string };
      renamed.push({ id: params.id, name });
      sides = sides.map((s) => (s.id === params.id ? { ...s, name } : s));
      return HttpResponse.json(sides.find((s) => s.id === params.id));
    }),
  );
  return renamed;
}

const sidesSection = async () => within(await screen.findByRole("region", { name: "Sides" }));

describe("sides", () => {
  it("lists the two sides and their armies for everyone, without the Umpire's buttons", async () => {
    serveCampaign("Player");
    await renderApp(`/campaigns/${campaignId}`);
    const section = await sidesSection();

    expect(await section.findByText("Coalition")).toBeInTheDocument();
    expect(section.getByText("French Empire")).toBeInTheDocument();
    expect(section.getByText("2 armies")).toBeInTheDocument();
    expect(section.getByText("1 army")).toBeInTheDocument();
    expect(section.queryByRole("button")).not.toBeInTheDocument();
  });

  it("lets the Umpire rename a side, and nothing else", async () => {
    const renamed = serveCampaign("Umpire");
    const user = userEvent.setup();
    const { container } = await renderApp(`/campaigns/${campaignId}`);
    const section = await sidesSection();

    expect(section.queryByRole("button", { name: /New side|Delete/ })).not.toBeInTheDocument();
    await expectNoAxeViolations(container);
    await user.click(await section.findByRole("button", { name: "Rename Coalition" }));
    const dialog = within(await screen.findByRole("dialog", { name: "Rename side" }));
    await user.clear(dialog.getByRole("textbox", { name: "Name" }));
    await user.type(dialog.getByRole("textbox", { name: "Name" }), "Sixth Coalition ");
    await user.click(dialog.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Renamed to Sixth Coalition.")).toBeInTheDocument();
    expect(renamed).toEqual([{ id: coalition.id, name: "Sixth Coalition" }]);
    expect(await section.findByText("Sixth Coalition")).toBeInTheDocument();
  });
});
