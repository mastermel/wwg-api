import { createFileRoute, redirect } from "@tanstack/react-router";

// The start page. Signed-out users will go to sign-in instead (step 10).
export const Route = createFileRoute("/")({
  beforeLoad: () => {
    // TanStack Router's redirects are thrown by design.
    // eslint-disable-next-line @typescript-eslint/only-throw-error
    throw redirect({ to: "/campaigns" });
  },
});
