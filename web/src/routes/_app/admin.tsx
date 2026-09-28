import { createFileRoute, notFound, Outlet } from "@tanstack/react-router";

// Admin screens: only for admins. Everyone else gets the not-found page (the API refuses them
// regardless).
export const Route = createFileRoute("/_app/admin")({
  beforeLoad: async ({ context }) => {
    await context.session.ready;
    if (!context.session.getState().user?.isAdmin) {
      // eslint-disable-next-line @typescript-eslint/only-throw-error
      throw notFound();
    }
  },
  component: Outlet,
});
