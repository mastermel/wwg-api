import { onlineManager, useQuery } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import { OfflineBanner } from "@/components/OfflineBanner";
import { QueryState } from "@/components/QueryState";
import { ApiError } from "@/lib/api-fetch";

function Thing({ load }: { load: () => Promise<string> }) {
  const query = useQuery({ queryKey: ["thing"], queryFn: load, retry: false });
  return <QueryState query={query}>{(name) => <p>Thing: {name}</p>}</QueryState>;
}

function OtherThing({ load }: { load: () => Promise<string> }) {
  const query = useQuery({ queryKey: ["other thing"], queryFn: load, retry: false });
  return <QueryState query={query}>{(name) => <p>Other thing: {name}</p>}</QueryState>;
}

function renderWith(ui: React.ReactNode, queryClient = createQueryClient()) {
  render(<AppProviders queryClient={queryClient}>{ui}</AppProviders>);
  return queryClient;
}

describe("QueryState", () => {
  it("shows the data once it has loaded", async () => {
    renderWith(<Thing load={() => Promise.resolve("Army")} />);

    expect(await screen.findByText("Thing: Army")).toBeInTheDocument();
  });

  it("says the data isn't available offline when nothing was saved", async () => {
    onlineManager.setOnline(false);

    renderWith(<Thing load={() => Promise.resolve("Army")} />);

    expect(await screen.findByText("Not available offline")).toBeInTheDocument();
  });

  it("shows saved data offline", async () => {
    onlineManager.setOnline(false);
    const queryClient = createQueryClient();
    queryClient.setQueryData(["thing"], "Saved army");

    renderWith(<Thing load={() => Promise.resolve("Army")} />, queryClient);

    expect(await screen.findByText("Thing: Saved army")).toBeInTheDocument();
  });

  it("keeps showing saved data when a refetch fails", async () => {
    const queryClient = createQueryClient();
    queryClient.setQueryData(["thing"], "Saved army", { updatedAt: 0 });

    renderWith(<Thing load={() => Promise.reject(new Error("Server down."))} />, queryClient);

    expect(await screen.findByText("Thing: Saved army")).toBeInTheDocument();
    expect(screen.queryByText("Server down.")).not.toBeInTheDocument();
  });

  it("says not found when a refetch does, even with saved data", async () => {
    const queryClient = createQueryClient();
    queryClient.setQueryData(["thing"], "Saved army", { updatedAt: 0 });

    renderWith(
      <Thing load={() => Promise.reject(new ApiError(404, undefined, undefined))} />,
      queryClient,
    );

    expect(await screen.findByText("Not found")).toBeInTheDocument();
    expect(screen.queryByText("Thing: Saved army")).not.toBeInTheDocument();
  });

  it("tells the user once about a server error, however many queries it breaks", async () => {
    const serverError = () => Promise.reject(new ApiError(503, undefined, undefined));
    renderWith(
      <>
        <Thing load={serverError} />
        <OtherThing load={serverError} />
      </>,
    );

    // One inline message per query, and a single notification.
    await waitFor(() => {
      expect(screen.getAllByText(/went wrong on the server/)).toHaveLength(3);
    });
  });

  it("shows information as a status, not an alert", async () => {
    onlineManager.setOnline(false);

    renderWith(<Thing load={() => Promise.resolve("Army")} />);

    expect(await screen.findByRole("status", { name: "Not available offline" })).toBeVisible();
  });

  it("shows the error when loading fails", async () => {
    renderWith(
      <Thing
        load={() => Promise.reject(new ApiError(409, undefined, { detail: "The server said no." }))}
      />,
    );

    expect(await screen.findByText("The server said no.")).toBeInTheDocument();
  });
});

describe("OfflineBanner", () => {
  it("shows nothing while online", () => {
    renderWith(<OfflineBanner />);

    expect(screen.queryByText("You're offline")).not.toBeInTheDocument();
  });

  it("says when the saved data is from while offline", () => {
    onlineManager.setOnline(false);
    const queryClient = createQueryClient();
    const savedAt = new Date(2026, 8, 27, 14, 30).getTime();
    queryClient.setQueryData(["thing"], "Army", { updatedAt: savedAt });

    renderWith(<OfflineBanner />, queryClient);

    expect(screen.getByText("You're offline")).toBeInTheDocument();
    expect(screen.getByText(/Showing saved data from/)).toHaveTextContent(
      new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(
        savedAt,
      ),
    );
  });
});
