import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";

const resetLink = "/reset-password?email=mel%40example.com&code=abc123";

describe("forgot password", () => {
  it("sends the request and shows the same confirmation whatever the answer", async () => {
    let sentEmail: unknown;
    server.use(
      http.post("*/api/auth/forgot-password", async ({ request }) => {
        sentEmail = ((await request.json()) as { email: string }).email;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await renderApp("/forgot-password", { session: "signed-out" });
    const user = userEvent.setup();

    await user.type(await screen.findByRole("textbox", { name: "Email" }), " mel@example.com ");
    await user.click(screen.getByRole("button", { name: "Send reset link" }));

    expect(await screen.findByText("Check your email")).toBeInTheDocument();
    expect(sentEmail).toBe("mel@example.com");
  });

  it("is linked from sign-in", async () => {
    await renderApp("/sign-in", { session: "signed-out" });

    await userEvent.click(await screen.findByRole("link", { name: "Forgot your password?" }));

    expect(
      await screen.findByRole("heading", { level: 1, name: "Reset your password" }),
    ).toBeInTheDocument();
  });

  it("has no detectable accessibility problems", async () => {
    const { container } = await renderApp("/forgot-password", { session: "signed-out" });
    await screen.findByRole("heading", { level: 1, name: "Reset your password" });

    await expectNoAxeViolations(container);
  });
});

describe("reset password", () => {
  async function choose(password: string, confirm = password) {
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText(/^New password/), password);
    await user.type(screen.getByLabelText(/^Confirm new password/), confirm);
    await user.click(screen.getByRole("button", { name: "Change password" }));
  }

  it("changes the password with the link's code, then goes to sign-in and says so", async () => {
    let body: unknown;
    server.use(
      http.post("*/api/auth/reset-password", async ({ request }) => {
        body = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await renderApp(resetLink, { session: "signed-out" });

    await choose("a brand new password");

    expect(await screen.findByText("Password changed")).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1, name: "Sign in" })).toBeInTheDocument();
    expect(body).toEqual({
      email: "mel@example.com",
      code: "abc123",
      newPassword: "a brand new password",
    });
  });

  it("checks the passwords match before sending anything", async () => {
    let requests = 0;
    server.use(
      http.post("*/api/auth/reset-password", () => {
        requests++;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await renderApp(resetLink, { session: "signed-out" });

    await choose("a brand new password", "a different password");

    expect(await screen.findByText("The passwords don't match.")).toBeInTheDocument();
    expect(requests).toBe(0);
  });

  it("offers a new link when this one has expired or was used", async () => {
    server.use(
      http.post("*/api/auth/reset-password", () =>
        HttpResponse.json(
          { status: 400, errors: { code: ["This reset link is invalid or has expired."] } },
          { status: 400, headers: { "Content-Type": "application/problem+json" } },
        ),
      ),
    );
    await renderApp(resetLink, { session: "signed-out" });

    await choose("a brand new password");

    expect(await screen.findByRole("alert")).toHaveTextContent("expired or was already used");
    expect(screen.getByRole("link", { name: "Ask for a new link" })).toBeInTheDocument();
  });

  it("says when the link is missing its email or code", async () => {
    await renderApp("/reset-password?email=mel%40example.com", { session: "signed-out" });

    expect(await screen.findByText("This link is incomplete")).toBeInTheDocument();
  });

  it("has no detectable accessibility problems", async () => {
    const { container } = await renderApp(resetLink, { session: "signed-out" });
    await screen.findByRole("heading", { level: 1, name: "Choose a new password" });

    await expectNoAxeViolations(container);
  });
});
