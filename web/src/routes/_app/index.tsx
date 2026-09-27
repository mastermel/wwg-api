import { createFileRoute, redirect } from "@tanstack/react-router";

// The start page: signed-out users are sent to sign-in by the _app layout first.
export const Route = createFileRoute("/_app/")({
  beforeLoad: () => {
    // TanStack Router's redirects are thrown by design.
    // eslint-disable-next-line @typescript-eslint/only-throw-error
    throw redirect({ to: "/campaigns" });
  },
});
