import { createFileRoute } from "@tanstack/react-router";
import { z } from "zod";
import { ConfirmEmailPage } from "@/features/auth/ConfirmEmailPage";

// The link from a welcome, or a changed address's email: /confirm-email?user=...&code=...
export const Route = createFileRoute("/_public/confirm-email")({
  validateSearch: z.object({
    user: z.string().optional().catch(undefined),
    code: z.string().optional().catch(undefined),
  }),
  component: function ConfirmEmailRoute() {
    const { user, code } = Route.useSearch();
    return <ConfirmEmailPage user={user} code={code} />;
  },
});
