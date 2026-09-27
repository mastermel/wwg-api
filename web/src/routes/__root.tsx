import { createRootRouteWithContext } from "@tanstack/react-router";
import type { RouterContext } from "@/app/router";
import { ErrorPage } from "@/components/ErrorPage";
import { NotFoundPage } from "@/components/NotFoundPage";
import { RootLayout } from "@/components/RootLayout";

export const Route = createRootRouteWithContext<RouterContext>()({
  component: RootLayout,
  notFoundComponent: NotFoundPage,
  errorComponent: ErrorPage,
});
