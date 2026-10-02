import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { BoatSettingsResponse, CampaignResponse } from "@/api/generated/model";
import { renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";

function serve(settings: BoatSettingsResponse = { capacity: 14 }) {
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
    http.get(`*/api/campaigns/${campaignId}/boat-settings`, () => HttpResponse.json(settings)),
    http.put(`*/api/campaigns/${campaignId}/boat-settings`, async ({ request }) => {
      const body = (await request.json()) as BoatSettingsResponse;
      saved.push(body);
      return HttpResponse.json(body);
    }),
  );
  return saved;
}

const section = async () => within(await screen.findByRole("region", { name: "Boats" }));

describe("the campaign's boats", () => {
  it("lets the Umpire change what a boat carries", async () => {
    const saved = serve();
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/edit`);
    const found = await section();

    const capacity = await found.findByRole("textbox", { name: /Points a boat carries/ });
    expect(capacity).toHaveValue("14");
    await user.clear(capacity);
    await user.type(capacity, "20");
    await user.click(found.getByRole("button", { name: "Save boats" }));

    expect(await screen.findByText("Saved the boat settings.")).toBeInTheDocument();
    expect(saved).toEqual([{ capacity: 20 }]);
  });

  it("puts the rules' boat back", async () => {
    const saved = serve({ capacity: 30 });
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/edit`);
    const found = await section();

    await user.click(await found.findByRole("button", { name: "Use the rules' boat" }));
    await user.click(found.getByRole("button", { name: "Save boats" }));

    expect(await screen.findByText("Saved the boat settings.")).toBeInTheDocument();
    expect(saved).toEqual([{ capacity: 14 }]);
  });
});
