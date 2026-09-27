import { createAsyncStoragePersister } from "@tanstack/query-async-storage-persister";
import { defaultShouldDehydrateQuery } from "@tanstack/react-query";
import type { PersistQueryClientProviderProps } from "@tanstack/react-query-persist-client";
import { del, get, set } from "idb-keyval";
import { cacheMaxAge } from "@/app/query-client";
import { appVersion } from "@/lib/app-version";

/**
 * Saves the API cache to IndexedDB so the app can show the last-known data offline (read-only).
 * The buster is the app version: a new deploy discards the saved copy, so saved data never has
 * an older shape than the code reading it. (Tying it to the signed-in user comes with sign-in.)
 */
export const persistOptions: PersistQueryClientProviderProps["persistOptions"] = {
  persister: createAsyncStoragePersister({
    storage: { getItem: get, setItem: set, removeItem: del },
    key: "wwg:query-cache",
  }),
  maxAge: cacheMaxAge,
  buster: appVersion,
  dehydrateOptions: {
    shouldDehydrateQuery: (query) =>
      defaultShouldDehydrateQuery(query) && query.meta?.persist !== false,
  },
};
