import { dehydrate, hydrate, QueryClient } from "@tanstack/react-query";
import { afterEach, expect, it, vi } from "vitest";
import { createQueryClient } from "@/app/query-client";

afterEach(() => {
  vi.useRealTimers();
});

it("keeps restored data nothing is using yet, so it's there offline", () => {
  vi.useFakeTimers();
  const saved = new QueryClient();
  saved.setQueryData(["/api/campaigns"], { items: [] });
  const queryClient = createQueryClient();

  hydrate(queryClient, dehydrate(saved));
  vi.advanceTimersByTime(60_000);

  expect(queryClient.getQueryData(["/api/campaigns"])).toEqual({ items: [] });
});
