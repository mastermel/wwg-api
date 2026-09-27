import { onlineManager } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import { persistOptions } from "@/app/query-persistence";
import { createAppRouter } from "@/app/router";

const root = document.getElementById("root");
if (!root) {
  throw new Error("Missing #root element in index.html");
}

// TanStack Query assumes it starts online and only listens for changes, so an installed app
// opened offline would try (and fail) to fetch instead of pausing and showing saved data.
onlineManager.setOnline(navigator.onLine);

const queryClient = createQueryClient();
const router = createAppRouter();

createRoot(root).render(
  <StrictMode>
    <AppProviders queryClient={queryClient} persistOptions={persistOptions}>
      <RouterProvider router={router} />
    </AppProviders>
  </StrictMode>,
);
