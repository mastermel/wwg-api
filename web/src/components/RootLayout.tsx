import { Outlet, useRouter } from "@tanstack/react-router";
import { useEffect } from "react";
import { UpdatePrompt } from "@/components/UpdatePrompt";
import { useSessionStore } from "@/features/auth/session-context";

/**
 * Around every page. When the session changes (sign-in, sign-out here or in another tab, or a
 * session ending), the routes' guards run again, so signed-out users land on sign-in.
 */
export function RootLayout() {
  const router = useRouter();
  const session = useSessionStore();

  useEffect(() => session.subscribe(() => void router.invalidate()), [router, session]);

  return (
    <>
      <Outlet />
      <UpdatePrompt />
    </>
  );
}
