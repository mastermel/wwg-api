import { createRootRoute } from "@tanstack/react-router";
import { AppLayout } from "@/components/AppLayout";
import { ErrorPage } from "@/components/ErrorPage";
import { NotFoundPage } from "@/components/NotFoundPage";

export const Route = createRootRoute({
  component: AppLayout,
  notFoundComponent: NotFoundPage,
  errorComponent: ErrorPage,
});
