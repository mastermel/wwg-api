import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { getGetHealthMockHandler } from "@/api/generated/endpoints/health/health.msw";
import { refreshAccessToken } from "@/lib/access-token";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { mockSession, testUser } from "@/test/session";

const problem = (status: number, body: object) =>
  HttpResponse.json(
    { status, ...body },
    { status, headers: { "Content-Type": "application/problem+json" } },
  );

function loginSucceeds() {
  server.use(
    http.post("*/api/auth/login", () =>
      HttpResponse.json({ accessToken: `token-for-${testUser.id}`, expiresIn: 1800 }),
    ),
  );
}

async function signIn(email = "mel@example.com", password = "correct horse battery") {
  const user = userEvent.setup();
  await user.type(screen.getByRole("textbox", { name: "Email" }), email);
  await user.type(screen.getByLabelText(/^Password/), password);
  await user.click(screen.getByRole("button", { name: "Sign in" }));
}

describe("sign-in", () => {
  it("sends signed-out visitors to sign-in, then back where they were going", async () => {
    server.use(getGetHealthMockHandler({ status: "Healthy" }));
    loginSucceeds();
    const { router } = await renderApp("/about", { session: "signed-out" });

    expect(await screen.findByRole("heading", { level: 1, name: "Sign in" })).toBeInTheDocument();
    expect(router.state.location.search).toEqual({ redirect: "/about" });

    await signIn();

    expect(await screen.findByRole("heading", { level: 1, name: "About" })).toBeInTheDocument();
  });

  it("shows the API's message for a wrong password", async () => {
    server.use(
      http.post("*/api/auth/login", () =>
        problem(401, { title: "Sign-in failed", detail: "The email or password is incorrect." }),
      ),
    );
    await renderApp("/sign-in", { session: "signed-out" });

    await signIn("mel@example.com", "wrong password");

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "The email or password is incorrect.",
    );
  });

  it("checks the fields before sending anything", async () => {
    let requests = 0;
    server.use(http.post("*/api/auth/login", () => (requests++, new HttpResponse(null))));
    await renderApp("/sign-in", { session: "signed-out" });

    await userEvent.click(await screen.findByRole("button", { name: "Sign in" }));

    expect(
      await screen.findAllByText(/./, { selector: ".mantine-InputWrapper-error" }),
    ).toHaveLength(2);
    expect(requests).toBe(0);
  });

  it("ignores a redirect to another site", async () => {
    loginSucceeds();
    await renderApp("/sign-in?redirect=%2F%2Fevil.example", { session: "signed-out" });

    await signIn();

    expect(await screen.findByRole("heading", { level: 1, name: "Campaigns" })).toBeInTheDocument();
  });

  it("skips sign-in when already signed in", async () => {
    await renderApp("/sign-in");

    expect(await screen.findByRole("heading", { level: 1, name: "Campaigns" })).toBeInTheDocument();
  });

  it("has no detectable accessibility problems", async () => {
    const { container } = await renderApp("/sign-in", { session: "signed-out" });
    await screen.findByRole("heading", { level: 1, name: "Sign in" });

    await expectNoAxeViolations(container);
  });
});

describe("signing out", () => {
  it("signs out from the user menu and returns to sign-in", async () => {
    const { calls } = await renderApp("/campaigns");
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Mel" }));
    await user.click(await screen.findByRole("menuitem", { name: "Sign out" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Sign in" })).toBeInTheDocument();
    expect(calls.logout).toBe(1);
  });

  it("says the session ended when it ends on its own", async () => {
    await renderApp("/campaigns");
    await screen.findByRole("heading", { level: 1, name: "Campaigns" });

    mockSession("signed-out");
    await refreshAccessToken();

    expect(await screen.findByText("You've been signed out")).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByRole("heading", { level: 1, name: "Sign in" })).toBeInTheDocument();
    });
  });
});
