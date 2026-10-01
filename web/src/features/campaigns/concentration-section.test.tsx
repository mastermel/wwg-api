import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { CampaignConcentrationResponse, CampaignResponse } from "@/api/generated/model";
import { usualCavalryTypes, usualInfantryTypes } from "@/features/campaigns/concentration";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";

const usual: CampaignConcentrationResponse = {
  infantryTypes: [...usualInfantryTypes],
  cavalryTypes: [...usualCavalryTypes],
  infantryLimit: 200,
  cavalryLimit: 160,
};

/** The Umpire's campaign with these settings; records what's saved. */
function serve(concentration: CampaignConcentrationResponse = usual) {
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
    http.get(`*/api/campaigns/${campaignId}/concentration`, () => HttpResponse.json(concentration)),
    http.put(`*/api/campaigns/${campaignId}/concentration`, async ({ request }) => {
      const body = (await request.json()) as CampaignConcentrationResponse;
      saved.push(body);
      return HttpResponse.json(body);
    }),
  );
  return saved;
}

const section = async () => within(await screen.findByRole("region", { name: "Concentration" }));

describe("the campaign's concentration settings", () => {
  it("shows the usual types, the free ones and the rules' limits", async () => {
    serve();
    const { container } = await renderApp(`/campaigns/${campaignId}/edit`);
    const found = await section();

    expect(await found.findByRole("textbox", { name: /Infantry limit/ })).toHaveValue("200");
    expect(found.getByRole("textbox", { name: /Cavalry limit/ })).toHaveValue("160");
    expect(
      found.getByText("Free (counted towards neither): Supply Train, Siege Artillery, Boat."),
    ).toBeInTheDocument();
    await expectNoAxeViolations(container);
  });

  it("lets the Umpire change a limit and free a type", async () => {
    const saved = serve();
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/edit`);
    const found = await section();

    const infantryLimit = await found.findByRole("textbox", { name: /Infantry limit/ });
    await user.clear(infantryLimit);
    await user.type(infantryLimit, "250");
    // Backspace takes off the last type chosen: horse artillery goes free.
    await user.click(found.getByRole("combobox", { name: "Counted as cavalry" }));
    await user.keyboard("{Backspace}");
    expect(found.getByText(/^Free .*Horse Artillery/)).toBeInTheDocument();
    await user.click(found.getByRole("button", { name: "Save concentration" }));

    expect(await screen.findByText("Saved the concentration settings.")).toBeInTheDocument();
    expect(saved).toEqual([
      {
        infantryTypes: usual.infantryTypes,
        cavalryTypes: usual.cavalryTypes.filter((type) => type !== "HorseArtillery"),
        infantryLimit: 250,
        cavalryLimit: 160,
      },
    ]);
  });

  it("says so when a limit is blank", async () => {
    const saved = serve();
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/edit`);
    const found = await section();

    await user.clear(await found.findByRole("textbox", { name: /Cavalry limit/ }));
    await user.click(found.getByRole("button", { name: "Save concentration" }));

    expect(await found.findByText("Enter points from 1 to 10000.")).toBeInTheDocument();
    expect(saved).toEqual([]);
  });

  it("puts the usual settings back", async () => {
    const saved = serve({
      infantryTypes: [],
      cavalryTypes: [],
      infantryLimit: 50,
      cavalryLimit: 40,
    });
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/edit`);
    const found = await section();

    await user.click(await found.findByRole("button", { name: "Use the usual settings" }));
    await user.click(found.getByRole("button", { name: "Save concentration" }));

    expect(await screen.findByText("Saved the concentration settings.")).toBeInTheDocument();
    expect(saved).toEqual([usual]);
  });
});
