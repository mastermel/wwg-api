import { createFileRoute } from "@tanstack/react-router";
import { JoinPage } from "@/features/join/JoinPage";

// Signed in or not: the page offers joining, or sign-in and register (which come back here).
export const Route = createFileRoute("/_public/join/$code")({
  beforeLoad: async ({ context }) => {
    await context.session.ready;
  },
  component: function JoinRoute() {
    return <JoinPage code={Route.useParams().code} />;
  },
});
