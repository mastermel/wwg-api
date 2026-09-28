import { expect, type Page } from "@playwright/test";

/**
 * Waits until the service worker is active, so a reload with no network still loads the app. (On
 * a first visit it doesn't control the page yet; it serves the next load.)
 */
export async function waitForServiceWorker(page: Page): Promise<void> {
  // Polled, not navigator.serviceWorker.ready: in WebKit that doesn't resolve for a page the
  // worker doesn't control yet.
  const state = () =>
    page.evaluate(async () => (await navigator.serviceWorker.getRegistration())?.active?.state);
  await expect.poll(state, { message: "the service worker is active" }).toBe("activated");
}

/**
 * Waits until the saved copy of the API cache (IndexedDB, idb-keyval's default store) has the
 * response for `apiPath`, and it includes `text`: so it's there to show offline. The app saves at
 * most once a second, so a page load right after new data can still lose it.
 */
export async function waitUntilSaved(page: Page, apiPath: string, text: string): Promise<void> {
  const saved = () =>
    page.evaluate(
      ([path, expected]) =>
        new Promise<boolean>((resolve) => {
          const open = indexedDB.open("keyval-store");
          open.onerror = () => {
            resolve(false);
          };
          open.onsuccess = () => {
            const read = open.result
              .transaction("keyval", "readonly")
              .objectStore("keyval")
              .get("wwg:query-cache");
            read.onerror = () => {
              resolve(false);
            };
            read.onsuccess = () => {
              if (typeof read.result !== "string") {
                resolve(false);
                return;
              }
              const saved = JSON.parse(read.result) as {
                clientState: { queries: { queryKey: unknown[]; state: { data: unknown } }[] };
              };
              resolve(
                saved.clientState.queries.some(
                  (query) =>
                    query.queryKey[0] === path &&
                    JSON.stringify(query.state.data).includes(expected),
                ),
              );
            };
          };
        }),
      [apiPath, text] as const,
    );
  // expect.poll, not waitForFunction: that doesn't wait on a predicate that returns a promise.
  await expect.poll(saved, { message: `${apiPath} saved for offline use` }).toBe(true);
}
