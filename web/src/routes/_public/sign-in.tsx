import { createFileRoute, redirect } from "@tanstack/react-router";
import { z } from "zod";
import { SignInPage } from "@/features/auth/SignInPage";
import { safeRedirect } from "@/lib/safe-redirect";

export const Route = createFileRoute("/_public/sign-in")({
  validateSearch: z.object({ redirect: z.string().optional().catch(undefined) }),
  beforeLoad: async ({ context, search }) => {
    await context.session.ready;
    if (context.session.getState().status !== "signed-out") {
      // eslint-disable-next-line @typescript-eslint/only-throw-error
      throw redirect({ href: safeRedirect(search.redirect) });
    }
  },
  component: function SignInRoute() {
    return <SignInPage redirect={Route.useSearch().redirect} />;
  },
});
