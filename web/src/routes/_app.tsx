import { createFileRoute, redirect } from "@tanstack/react-router";
import { AppLayout } from "@/components/AppLayout";

// Every page inside the app frame needs a session (or the saved one, offline).
export const Route = createFileRoute("/_app")({
  beforeLoad: async ({ context, location }) => {
    await context.session.ready;
    if (context.session.getState().status === "signed-out") {
      // TanStack Router's redirects are thrown by design.
      // eslint-disable-next-line @typescript-eslint/only-throw-error
      throw redirect({ to: "/sign-in", search: { redirect: location.href } });
    }
  },
  component: AppLayout,
});
