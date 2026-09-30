import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { CampaignCalendarResponse, CampaignResponse } from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const campaignId = "0192f5c1-0000-7000-8000-00000000c001";

const rules: CampaignCalendarResponse = {
  startDate: null,
  firstTurnPart: "Morning",
  morningNations: ["France", "Bavaria", "Wurttemberg", "Baden", "Warsaw", "Italy", "Holland"],
  afternoonNations: ["Russia", "Austria"],
  rules: true,
};

/** The Umpire's campaign with this calendar; records what's saved. */
function serve(calendar: CampaignCalendarResponse = rules) {
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
    http.get(`*/api/campaigns/${campaignId}/calendar`, () => HttpResponse.json(calendar)),
    http.put(`*/api/campaigns/${campaignId}/calendar`, async ({ request }) => {
      const body = (await request.json()) as CampaignCalendarResponse;
      saved.push(body);
      return HttpResponse.json({ ...body, rules: false });
    }),
  );
  return saved;
}

const calendarSection = async () => within(await screen.findByRole("region", { name: "Calendar" }));

describe("the campaign's calendar", () => {
  it("lets the Umpire set the first turn's day and time of day", async () => {
    const saved = serve();
    const user = userEvent.setup();
    const { container } = await renderApp(`/campaigns/${campaignId}/edit`);
    const section = await calendarSection();

    await user.type(await section.findByLabelText(/The first turn's day/), "1815-06-15");
    await user.click(section.getByText("Afternoon (14:00–22:00)"));
    await expectNoAxeViolations(container);
    await user.click(section.getByRole("button", { name: "Save calendar" }));

    expect(await screen.findByText("Saved the calendar.")).toBeInTheDocument();
    expect(saved).toEqual([
      {
        startDate: "1815-06-15",
        firstTurnPart: "Afternoon",
        morningNations: rules.morningNations,
        afternoonNations: rules.afternoonNations,
      },
    ]);
  });

  it("puts the rule book's nations back", async () => {
    const saved = serve({ ...rules, morningNations: [], afternoonNations: [], rules: false });
    const user = userEvent.setup();
    await renderApp(`/campaigns/${campaignId}/edit`);
    const section = await calendarSection();

    await user.click(await section.findByRole("button", { name: "Use the rule book's nations" }));
    await user.click(section.getByRole("button", { name: "Save calendar" }));

    expect(await screen.findByText("Saved the calendar.")).toBeInTheDocument();
    expect(saved).toEqual([
      expect.objectContaining({
        startDate: null,
        morningNations: rules.morningNations,
        afternoonNations: rules.afternoonNations,
      }),
    ]);
  });
});
