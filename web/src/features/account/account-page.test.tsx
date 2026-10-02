import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testUser } from "@/test/session";

const problem = (status: number, body: object) =>
  HttpResponse.json(
    { status, ...body },
    { status, headers: { "Content-Type": "application/problem+json" } },
  );

const section = (name: string) => screen.getByRole("region", { name });

describe("account page", () => {
  it("is linked from the user menu", async () => {
    await renderApp("/campaigns");
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Mel" }));
    await user.click(await screen.findByRole("menuitem", { name: "Account" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Account" })).toBeInTheDocument();
  });

  it("saves the name, and the header shows it", async () => {
    server.use(
      http.put("*/api/me", async ({ request }) =>
        HttpResponse.json({ ...testUser, ...((await request.json()) as object) }),
      ),
    );
    await renderApp("/account");
    const user = userEvent.setup();
    const name = await screen.findByRole("textbox", { name: "First name" });

    await user.clear(name);
    await user.type(name, " Melanie ");
    await user.click(screen.getByRole("button", { name: "Save name" }));

    expect(await screen.findByRole("button", { name: "Melanie" })).toBeInTheDocument();
  });

  it("changes the email and keeps the session with the new tokens", async () => {
    server.use(
      http.put("*/api/me/email", () =>
        HttpResponse.json({ accessToken: `token-for-${testUser.id}`, expiresIn: 1800 }),
      ),
    );
    await renderApp("/account");
    const user = userEvent.setup();
    const email = within(await screen.findByRole("region", { name: "Email" }));

    await user.type(email.getByRole("textbox", { name: "New email" }), "melanie@example.com");
    await user.type(email.getByLabelText(/^Current password/), "correct horse battery");
    await user.click(email.getByRole("button", { name: "Change email" }));

    expect(await screen.findByText(/sign in with melanie@example.com/)).toBeInTheDocument();
  });

  it("shows on the field when the new email is taken", async () => {
    server.use(http.put("*/api/me/email", () => problem(409, { title: "Email already in use" })));
    await renderApp("/account");
    const user = userEvent.setup();
    const email = within(await screen.findByRole("region", { name: "Email" }));

    await user.type(email.getByRole("textbox", { name: "New email" }), "taken@example.com");
    await user.type(email.getByLabelText(/^Current password/), "correct horse battery");
    await user.click(email.getByRole("button", { name: "Change email" }));

    expect(
      await screen.findByText("An account with this email already exists."),
    ).toBeInTheDocument();
  });

  it("checks the new passwords match, and shows a wrong current password on its field", async () => {
    let requests = 0;
    server.use(
      http.put("*/api/me/password", () => {
        requests++;
        return problem(400, {
          errors: { currentPassword: ["The current password is incorrect."] },
        });
      }),
    );
    await renderApp("/account");
    const user = userEvent.setup();
    const password = within(await screen.findByRole("region", { name: "Password" }));

    await user.type(password.getByLabelText(/^Current password/), "wrong password");
    await user.type(password.getByLabelText(/^New password/), "a brand new password");
    await user.type(password.getByLabelText(/^Confirm new password/), "something else");
    await user.click(password.getByRole("button", { name: "Change password" }));
    expect(await screen.findByText("The passwords don't match.")).toBeInTheDocument();
    expect(requests).toBe(0);

    await user.clear(password.getByLabelText(/^Confirm new password/));
    await user.type(password.getByLabelText(/^Confirm new password/), "a brand new password");
    await user.click(password.getByRole("button", { name: "Change password" }));

    expect(await screen.findByText("The current password is incorrect.")).toBeInTheDocument();
  });

  it("signs out everywhere after confirming, and lands on sign-in", async () => {
    let called = false;
    server.use(
      http.post("*/api/me/sign-out-everywhere", () => {
        called = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await renderApp("/account");
    const user = userEvent.setup();

    await user.click(
      within(await screen.findByRole("region", { name: "Sign out everywhere" })).getByRole(
        "button",
        { name: "Sign out everywhere" },
      ),
    );
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", {
        name: "Sign out everywhere",
      }),
    );

    expect(await screen.findByRole("heading", { level: 1, name: "Sign in" })).toBeInTheDocument();
    expect(called).toBe(true);
  });

  it("turns a campaign email off", async () => {
    const saved: unknown[] = [];
    server.use(
      http.get("*/api/me/email-settings", () => HttpResponse.json({ muted: ["PlayerJoined"] })),
      http.put("*/api/me/email-settings", async ({ request }) => {
        const body = await request.json();
        saved.push(body);
        return HttpResponse.json(body);
      }),
    );
    await renderApp("/account");
    const user = userEvent.setup();
    const emails = within(await screen.findByRole("region", { name: "Email notifications" }));

    expect(await emails.findByRole("switch", { name: /A player joins/ })).not.toBeChecked();
    await user.click(emails.getByRole("switch", { name: /An army submits its turn/ }));

    expect(await screen.findByText("Saved your email settings.")).toBeInTheDocument();
    expect(saved).toEqual([{ muted: ["PlayerJoined", "ArmySubmitted"] }]);
  });

  it("builds each switch's change on the last, however quick", async () => {
    const saved: unknown[] = [];
    server.use(
      http.get("*/api/me/email-settings", () => HttpResponse.json({ muted: [] })),
      http.put("*/api/me/email-settings", async ({ request }) => {
        const body = await request.json();
        saved.push(body);
        return HttpResponse.json(body);
      }),
    );
    await renderApp("/account");
    const user = userEvent.setup();
    const emails = within(await screen.findByRole("region", { name: "Email notifications" }));

    await user.click(await emails.findByRole("switch", { name: /A new turn/ }));
    await waitFor(() => {
      expect(emails.getByRole("switch", { name: /A player joins/ })).toBeEnabled();
    });
    await user.click(emails.getByRole("switch", { name: /A player joins/ }));

    await waitFor(() => {
      expect(saved).toEqual([
        { muted: ["TurnStarted"] },
        { muted: ["TurnStarted", "PlayerJoined"] },
      ]);
    });
  });

  it("has no detectable accessibility problems", async () => {
    const { container } = await renderApp("/account");
    await screen.findByRole("heading", { level: 1, name: "Account" });
    expect(section("Your name")).toBeInTheDocument();

    await expectNoAxeViolations(container);
  });
});
