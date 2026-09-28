import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { JoinCampaignResponse, JoinPreviewResponse } from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const code = "AbCdEfGhIjKlMnOpQrStUv";
const campaignId = "0192f5c1-0000-7000-8000-00000000c001";

function servePreview(preview: JoinPreviewResponse | null) {
  server.use(
    http.get(`*/api/join/${code}`, () =>
      preview
        ? HttpResponse.json(preview)
        : HttpResponse.json(
            { status: 404, title: "Not Found" },
            { status: 404, headers: { "Content-Type": "application/problem+json" } },
          ),
    ),
  );
}

function serveJoin(response: JoinCampaignResponse) {
  const joins: string[] = [];
  server.use(
    http.post(`*/api/join/${code}`, ({ request }) => {
      joins.push(request.headers.get("Authorization") ?? "");
      return HttpResponse.json(response, { status: response.myRole === "Player" ? 201 : 200 });
    }),
    http.get(`*/api/campaigns/${campaignId}`, () =>
      HttpResponse.json({
        id: campaignId,
        name: "The Peninsular War",
        description: null,
        umpire: null,
        myRole: response.myRole,
        playerCount: 1,
        createdAt: "2026-09-01T12:00:00Z",
        updatedAt: "2026-09-01T12:00:00Z",
      }),
    ),
  );
  return joins;
}

const preview: JoinPreviewResponse = {
  campaignName: "The Peninsular War",
  umpireName: "Ada Admin",
};

describe("join links", () => {
  it("shows a signed-out visitor the campaign, and sign-in that comes back", async () => {
    servePreview(preview);
    await renderApp(`/join/${code}`, { session: "signed-out" });

    expect(await screen.findByText(/, run by Ada Admin/)).toHaveTextContent(
      "You've been invited to join The Peninsular War, run by Ada Admin.",
    );
    expect(screen.getByRole("link", { name: "Sign in to join" })).toHaveAttribute(
      "href",
      `/sign-in?redirect=%2Fjoin%2F${code}`,
    );
    expect(screen.getByRole("link", { name: "Create an account" })).toHaveAttribute(
      "href",
      `/register?redirect=%2Fjoin%2F${code}`,
    );
  });

  it("returns to the join page after signing in, then joins", async () => {
    servePreview(preview);
    const joins = serveJoin({ campaignId, myRole: "Player" });
    server.use(
      http.post("*/api/auth/login", () =>
        HttpResponse.json({ accessToken: `token-for-${testUser.id}`, expiresIn: 1800 }),
      ),
    );
    const user = userEvent.setup();
    await renderApp(`/join/${code}`, { session: "signed-out" });

    await user.click(await screen.findByRole("link", { name: "Sign in to join" }));
    await user.type(await screen.findByRole("textbox", { name: "Email" }), "mel@example.com");
    await user.type(screen.getByLabelText(/^Password/), "correct horse battery");
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    await user.click(await screen.findByRole("button", { name: "Join as a Player" }));

    expect(
      await screen.findByRole("heading", { level: 1, name: "The Peninsular War" }),
    ).toBeInTheDocument();
    expect(await screen.findByText("You're in The Peninsular War.")).toBeInTheDocument();
    expect(joins).toEqual([`Bearer token-for-${testUser.id}`]);
  });

  it("opens the campaign for someone already in it", async () => {
    servePreview(preview);
    serveJoin({ campaignId, myRole: "Umpire" });
    await renderApp(`/join/${code}`);

    await userEvent.click(await screen.findByRole("button", { name: "Join as a Player" }));

    expect(
      await screen.findByText("You're already the Umpire of The Peninsular War."),
    ).toBeInTheDocument();
    expect(
      await screen.findByRole("heading", { level: 1, name: "The Peninsular War" }),
    ).toBeInTheDocument();
  });

  it("says when a link doesn't work", async () => {
    servePreview(null);
    await renderApp(`/join/${code}`);

    expect(await screen.findByText("This join link doesn't work")).toBeInTheDocument();
  });

  it("has no detectable accessibility problems", async () => {
    servePreview(preview);
    const { container } = await renderApp(`/join/${code}`, { session: "signed-out" });
    await screen.findByRole("link", { name: "Sign in to join" });

    await expectNoAxeViolations(container);
  });
});
