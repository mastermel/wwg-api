import { dehydrate, QueryClient } from "@tanstack/react-query";
import { expect, it } from "vitest";
import { persistOptions } from "@/app/query-persistence";

it("never saves queries marked persist: false (admin data, live status)", async () => {
  const client = new QueryClient();
  await client.query({ queryKey: ["/api/campaigns"], queryFn: () => ({ items: [] }) });
  await client.query({
    queryKey: ["/api/admin/users"],
    queryFn: () => ({ items: [{ email: "mel@example.com" }] }),
    meta: { persist: false },
  });

  const saved = dehydrate(client, persistOptions.dehydrateOptions);

  expect(saved.queries.map((query) => query.queryKey)).toEqual([["/api/campaigns"]]);
});
