import { createFileRoute } from "@tanstack/react-router";
import { z } from "zod";
import { ResetPasswordPage } from "@/features/auth/ResetPasswordPage";

// The link from the reset email: /reset-password?email=...&code=...
export const Route = createFileRoute("/_public/reset-password")({
  validateSearch: z.object({
    email: z.string().optional().catch(undefined),
    code: z.string().optional().catch(undefined),
  }),
  component: function ResetPasswordRoute() {
    const { email, code } = Route.useSearch();
    return <ResetPasswordPage email={email} code={code} />;
  },
});
