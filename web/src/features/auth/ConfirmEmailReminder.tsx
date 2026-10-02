import { Alert, Button, Group, Text } from "@mantine/core";
import { IconMailCheck } from "@tabler/icons-react";
import { useState } from "react";
import { SendAgain } from "@/features/auth/ConfirmEmailPage";
import { useSession } from "@/features/auth/session-context";

const dismissedKey = "wwg:confirm-email-reminder-dismissed";

function wasDismissed() {
  try {
    return sessionStorage.getItem(dismissedKey) === "true";
  } catch {
    return false;
  }
}

/**
 * A reminder to confirm the account's email (decision 0023), until it's confirmed: the welcome's
 * link, or a new one sent again. "Not now" hides it until the browser closes.
 */
export function ConfirmEmailReminder() {
  const { status, user } = useSession();
  const [dismissed, setDismissed] = useState(wasDismissed);
  if (status !== "signed-in" || !user || user.emailConfirmed || dismissed) return null;

  return (
    <Alert
      role="status"
      color="navy"
      icon={<IconMailCheck aria-hidden />}
      title="Confirm your email"
      mb="lg"
    >
      <Text size="sm" mb="xs">
        We sent a link to {user.email}. Confirm it, so the club&apos;s emails reach you.
      </Text>
      <Group gap="xs">
        <SendAgain compact />
        <Button
          size="compact-sm"
          variant="subtle"
          onClick={() => {
            setDismissed(true);
            try {
              sessionStorage.setItem(dismissedKey, "true");
            } catch {
              // Not remembered: it's back on the next page load.
            }
          }}
        >
          Not now
        </Button>
      </Group>
    </Alert>
  );
}
