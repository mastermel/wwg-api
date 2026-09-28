import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { UserSummary } from "@/api/generated/model";
import { expectNoAxeViolations, renderApp } from "@/test/render";
import { server } from "@/test/server";
import { testAdmin } from "@/test/session";

const summary = (i: number): UserSummary => ({
  id: `0192f5c1-0000-7000-8000-${String(i).padStart(12, "0")}`,
  email: `user${String(i)}@example.com`,
  firstName: `First${String(i)}`,
  lastName: "Zed",
  isAdmin: false,
  createdAt: "2026-09-01T12:00:00Z",
});

/** Serves /api/admin/users from `all`, filtering and paging like the API; records requests. */
function mockUserList(all: UserSummary[]) {
  const requests: URLSearchParams[] = [];
  server.use(
    http.get("*/api/admin/users", ({ request }) => {
      const params = new URL(request.url).searchParams;
      requests.push(params);
      const search = params.get("search")?.toLowerCase() ?? "";
      const page = Number(params.get("page") ?? "1");
      const pageSize = Number(params.get("pageSize") ?? "25");
      const matches = all.filter((u) =>
        `${u.firstName} ${u.lastName} ${u.email}`.toLowerCase().includes(search),
      );
      return HttpResponse.json({
        items: matches.slice((page - 1) * pageSize, page * pageSize),
        page,
        pageSize,
        totalCount: matches.length,
      });
    }),
  );
  return requests;
}

describe("admin users", () => {
  it("shows Users in the navigation only for admins", async () => {
    await renderApp("/campaigns");
    await screen.findByRole("heading", { level: 1, name: "Campaigns" });
    expect(screen.queryByRole("link", { name: "Users" })).not.toBeInTheDocument();
  });

  it("gives non-admins the not-found page", async () => {
    await renderApp("/admin/users");

    expect(
      await screen.findByRole("heading", { level: 1, name: "Page not found" }),
    ).toBeInTheDocument();
  });

  it("lists users and searches them", async () => {
    const requests = mockUserList([
      summary(1),
      summary(2),
      { ...summary(3), firstName: "Melanie" },
    ]);
    await renderApp("/admin/users", { user: testAdmin });

    const sidebar = await screen.findByRole("navigation", { name: "Main" });
    expect(within(sidebar).getByRole("link", { name: "Users" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(await screen.findByRole("link", { name: "Zed, First1" })).toBeInTheDocument();
    expect(screen.getByText("3 users")).toBeInTheDocument();

    await userEvent.type(screen.getByRole("searchbox", { name: "Search" }), "mel");

    expect(await screen.findByText("1 user")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Zed, Melanie" })).toBeInTheDocument();
    expect(requests.at(-1)?.get("search")).toBe("mel");
  });

  it("pages through a long list", async () => {
    mockUserList(Array.from({ length: 30 }, (_, i) => summary(i + 1)));
    await renderApp("/admin/users", { user: testAdmin });
    await screen.findByRole("link", { name: "Zed, First1" });

    await userEvent.click(screen.getByRole("button", { name: "2" }));

    expect(await screen.findByRole("link", { name: "Zed, First26" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Zed, First1" })).not.toBeInTheDocument();
  });

  it("deletes a user after confirming, then returns to the list", async () => {
    const mel = { ...summary(7), firstName: "Mel", lastName: "Green", email: "mel@example.com" };
    mockUserList([mel]);
    let deleted: string | undefined;
    server.use(
      http.get("*/api/admin/users/:id", () => HttpResponse.json({ ...mel, lockedOutUntil: null })),
      http.delete("*/api/admin/users/:id", ({ params }) => {
        deleted = params.id as string;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await renderApp(`/admin/users/${mel.id}`, { user: testAdmin });
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Delete user" }));
    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Delete user" }),
    );

    expect(await screen.findByRole("heading", { level: 1, name: "Users" })).toBeInTheDocument();
    expect(deleted).toBe(mel.id);
  });

  it("doesn't let admins delete themselves", async () => {
    server.use(
      http.get("*/api/admin/users/:id", () =>
        HttpResponse.json({
          ...testAdmin,
          createdAt: "2026-09-01T12:00:00Z",
          lockedOutUntil: null,
        }),
      ),
    );
    await renderApp(`/admin/users/${testAdmin.id}`, { user: testAdmin });

    expect(await screen.findByRole("button", { name: "Delete user" })).toBeDisabled();
    expect(screen.getByText("You can't delete your own account.")).toBeInTheDocument();
  });

  it("says when the user doesn't exist", async () => {
    server.use(
      http.get("*/api/admin/users/:id", () =>
        HttpResponse.json(
          { status: 404, title: "Not Found" },
          { status: 404, headers: { "Content-Type": "application/problem+json" } },
        ),
      ),
    );
    await renderApp(`/admin/users/${summary(9).id}`, { user: testAdmin });

    expect(await screen.findByText("Not found")).toBeInTheDocument();
  });

  it("has no detectable accessibility problems", async () => {
    mockUserList([summary(1), { ...summary(2), isAdmin: true }]);
    const { container } = await renderApp("/admin/users", { user: testAdmin });
    await screen.findByRole("link", { name: "Zed, First1" });

    await expectNoAxeViolations(container);
  });
});
