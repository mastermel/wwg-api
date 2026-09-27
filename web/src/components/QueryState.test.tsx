import { onlineManager, useQuery } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import { OfflineBanner } from "@/components/OfflineBanner";
import { QueryState } from "@/components/QueryState";

function Thing({ load }: { load: () => Promise<string> }) {
  const query = useQuery({ queryKey: ["thing"], queryFn: load, retry: false });
  return <QueryState query={query}>{(name) => <p>Thing: {name}</p>}</QueryState>;
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

  it("shows the error when loading fails", async () => {
    renderWith(<Thing load={() => Promise.reject(new Error("The server said no."))} />);

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
