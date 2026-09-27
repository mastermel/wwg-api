import { RouterProvider } from "@tanstack/react-router";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import { createAppRouter } from "@/app/router";

const root = document.getElementById("root");
if (!root) {
  throw new Error("Missing #root element in index.html");
}

const queryClient = createQueryClient();
const router = createAppRouter();

createRoot(root).render(
  <StrictMode>
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>
  </StrictMode>,
);
