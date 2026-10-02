import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const userId = "0192f5c1-0000-7000-8000-000000000001";
const link = `/confirm-email?user=${userId}&code=abc123`;

describe("confirming an email", () => {
  it("confirms the address as the link opens, and offers to sign in", async () => {
    const sent: unknown[] = [];
    server.use(
      http.post("*/api/auth/confirm-email", async ({ request }) => {
        sent.push(await request.json());
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const { container } = await renderApp(link, { session: "signed-out" });

    expect(await screen.findByText("Your email is confirmed")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Sign in" })).toBeInTheDocument();
    expect(sent).toEqual([{ userId, code: "abc123" }]);
    await expectNoAxeViolations(container);
  });

  it("says when the link didn't work", async () => {
    server.use(
      http.post("*/api/auth/confirm-email", () =>
        HttpResponse.json(
          { status: 400, errors: { code: ["This link has expired."] } },
          { status: 400, headers: { "Content-Type": "application/problem+json" } },
        ),
      ),
    );

    await renderApp(link, { session: "signed-out" });

    expect(await screen.findByText("This link didn't work")).toBeInTheDocument();
  });
});

describe("the reminder to confirm", () => {
  const unconfirmed = { ...testUser, emailConfirmed: false };

  it("asks an unconfirmed account to confirm, and sends a new link", async () => {
    sessionStorage.clear();
    let sends = 0;
    server.use(
      http.post("*/api/me/confirmation-email", () => {
        sends++;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    await renderApp("/campaigns", { user: unconfirmed });

    expect(await screen.findByText("Confirm your email")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Send a new link" }));

    expect(await screen.findByText(`Sent a new link to ${testUser.email}.`)).toBeInTheDocument();
    expect(sends).toBe(1);
  });

  it("goes for the session on Not now, and never shows to a confirmed account", async () => {
    sessionStorage.clear();
    const user = userEvent.setup();
    await renderApp("/campaigns", { user: unconfirmed });

    await user.click(await screen.findByRole("button", { name: "Not now" }));
    expect(screen.queryByText("Confirm your email")).not.toBeInTheDocument();
    expect(sessionStorage.getItem("wwg:confirm-email-reminder-dismissed")).toBe("true");
  });

  it("isn't shown once confirmed", async () => {
    sessionStorage.clear();
    await renderApp("/campaigns");

    expect(await screen.findByRole("heading", { level: 1, name: "Campaigns" })).toBeInTheDocument();
    expect(screen.queryByText("Confirm your email")).not.toBeInTheDocument();
  });
});
