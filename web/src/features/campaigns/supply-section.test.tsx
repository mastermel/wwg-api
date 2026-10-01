import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { CampaignResponse, CampaignSupplySettingsResponse } from "@/api/generated/model";
import { usualExemptTypes, usualOffTheLandNations } from "@/features/campaigns/supply";
import { renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";

const usual: CampaignSupplySettingsResponse = {
  reach: 1,
  exemptTypes: [...usualExemptTypes],
  offTheLandNations: [...usualOffTheLandNations],
};

function serve(settings: CampaignSupplySettingsResponse = usual) {
  const saved: unknown[] = [];
  server.use(
    http.get(`*/api/campaigns/${campaignId}`, () =>
      HttpResponse.json({
        id: campaignId,
        name: "The Hundred Days",
        description: null,
        umpire: {
          memberId: "0192f5c1-0000-7000-8000-00000000d001",
          userId: testUser.id,
          firstName: "Mel",
          lastName: "Green",
        },
        myRole: "Umpire",
        playerCount: 0,
        createdAt: "2026-09-01T12:00:00Z",
        updatedAt: "2026-09-01T12:00:00Z",
      } satisfies CampaignResponse),
    ),
    http.get(`*/api/campaigns/${campaignId}/supply-settings`, () => HttpResponse.json(settings)),
    http.put(`*/api/campaigns/${campaignId}/supply-settings`, async ({ request }) => {
      const body = (await request.json()) as CampaignSupplySettingsResponse;
      saved.push(body);
      return HttpResponse.json(body);
    }),
  );
  return saved;
}

const section = async () => within(await screen.findByRole("region", { name: "Supply" }));

describe("the campaign's supply settings", () => {
  it("lets the Umpire change the reach", async () => {
    const saved = serve();
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/edit`);
    const found = await section();

    const reach = await found.findByRole("textbox", { name: /Supply reach/ });
    expect(reach).toHaveValue("1");
    await user.clear(reach);
    await user.type(reach, "2");
    await user.click(found.getByRole("button", { name: "Save supply" }));

    expect(await screen.findByText("Saved the supply settings.")).toBeInTheDocument();
    expect(saved).toEqual([{ ...usual, reach: 2 }]);
  });

  it("puts the usual settings back", async () => {
    const saved = serve({ reach: 3, exemptTypes: [], offTheLandNations: [] });
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/edit`);
    const found = await section();

    await user.click(await found.findByRole("button", { name: "Use the usual settings" }));
    await user.click(found.getByRole("button", { name: "Save supply" }));

    expect(await screen.findByText("Saved the supply settings.")).toBeInTheDocument();
    expect(saved).toEqual([usual]);
  });
});
