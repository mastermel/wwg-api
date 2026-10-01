import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { CampaignResponse, VictorySettingsResponse } from "@/api/generated/model";
import { renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";

function serve(settings: VictorySettingsResponse = { mode: "Rules" }) {
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
    http.get(`*/api/campaigns/${campaignId}/victory-settings`, () => HttpResponse.json(settings)),
    http.put(`*/api/campaigns/${campaignId}/victory-settings`, async ({ request }) => {
      const body = (await request.json()) as VictorySettingsResponse;
      saved.push(body);
      return HttpResponse.json(body);
    }),
  );
  return saved;
}

const section = async () => within(await screen.findByRole("region", { name: "Victory points" }));

describe("the campaign's victory points settings", () => {
  it("counts every settlement by the rules, until the Umpire chooses only those they give points", async () => {
    const saved = serve();
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/edit`);
    const found = await section();

    expect(
      await found.findByRole("radio", { name: /Every settlement, by the rules/ }),
    ).toBeChecked();
    await user.click(found.getByRole("radio", { name: /Only those I give points/ }));
    await user.click(found.getByRole("button", { name: "Save victory points" }));

    expect(await screen.findByText("Saved the victory points settings.")).toBeInTheDocument();
    expect(saved).toEqual([{ mode: "Chosen" }]);
  });

  it("shows the choice saved", async () => {
    serve({ mode: "Chosen" });
    await renderApp(`/campaigns/${campaignId}/edit`);
    const found = await section();

    expect(await found.findByRole("radio", { name: /Only those I give points/ })).toBeChecked();
    expect(found.getByRole("button", { name: "Save victory points" })).toBeDisabled();
  });
});
