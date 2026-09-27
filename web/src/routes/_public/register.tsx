import { createFileRoute, redirect } from "@tanstack/react-router";
import { z } from "zod";
import { RegisterPage } from "@/features/auth/RegisterPage";
import { safeRedirect } from "@/lib/safe-redirect";

export const Route = createFileRoute("/_public/register")({
  validateSearch: z.object({ redirect: z.string().optional().catch(undefined) }),
  beforeLoad: async ({ context, search }) => {
    await context.session.ready;
    if (context.session.getState().status !== "signed-out") {
      // eslint-disable-next-line @typescript-eslint/only-throw-error
      throw redirect({ href: safeRedirect(search.redirect) });
    }
  },
  component: function RegisterRoute() {
    return <RegisterPage redirect={Route.useSearch().redirect} />;
  },
});
