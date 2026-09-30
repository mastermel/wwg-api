import { notifications } from "@mantine/notifications";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { masquerade } from "@/api/generated/endpoints/admin/admin";
import { endMasquerade } from "@/api/generated/endpoints/auth/auth";
import { useSessionStore } from "@/features/auth/session-context";
import { errorMessage } from "@/lib/errors";

/**
 * Starting and ending an Admin's masquerade as another user (decision 0012). Either way the
 * session switches user, clearing everything saved on the device, and the app starts afresh on
 * a page that person can see.
 */
export function useMasqueradeSession() {
  const session = useSessionStore();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);

  const run = async (
    change: () => Promise<Parameters<typeof session.switchUser>[0]>,
    done: string,
    failed: string,
    to: "/campaigns" | "/admin/users",
  ) => {
    setBusy(true);
    try {
      await session.switchUser(await change());
      notifications.show({ color: "green", message: done });
      await navigate({ to });
    } catch (error) {
      notifications.show({ color: "red", message: errorMessage(error, failed) });
    } finally {
      setBusy(false);
    }
  };

  return {
    busy,
    start: (user: { id: string; firstName: string; lastName: string }) =>
      run(
        () => masquerade(user.id),
        `You're masquerading as ${user.firstName} ${user.lastName}.`,
        "The masquerade couldn't start. Try again.",
        "/campaigns",
      ),
    end: () =>
      run(
        () => endMasquerade(),
        "Masquerade ended: you're yourself again.",
        "The masquerade couldn't end. Try again.",
        "/admin/users",
      ),
  };
}
