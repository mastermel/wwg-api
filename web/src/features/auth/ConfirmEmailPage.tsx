import { Alert, Anchor, Button, Stack, Text } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { Link } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";
import { getMe, useSendConfirmationEmail } from "@/api/generated/endpoints/account/account";
import { useConfirmEmail } from "@/api/generated/endpoints/auth/auth";
import { Page } from "@/components/Page";
import { useSession, useSessionStore } from "@/features/auth/session-context";
import { errorMessage } from "@/lib/errors";

type Outcome = "confirming" | "confirmed" | "failed";

/**
 * The link from a welcome or a changed address's email (decision 0023): confirms the address as
 * it opens, signed in or not, and says how it went.
 */
export function ConfirmEmailPage({ user, code }: { user?: string; code?: string }) {
  const confirm = useConfirmEmail();
  const session = useSessionStore();
  const { status } = useSession();
  const [outcome, setOutcome] = useState<Outcome>(user && code ? "confirming" : "failed");
  // Once, even where React runs effects twice: a second try would fail on the spent link.
  const tried = useRef(false);

  useEffect(() => {
    if (!user || !code || tried.current) return;
    tried.current = true;
    confirm
      .mutateAsync({ data: { userId: user, code } })
      .then(async () => {
        setOutcome("confirmed");
        // Signed in: the reminder goes.
        const me = await getMe().catch(() => null);
        if (me) session.setUser(me);
      })
      .catch(() => {
        setOutcome("failed");
      });
  }, [user, code, confirm, session]);

  const signedIn = status === "signed-in";
  return (
    <Page title="Confirm your email">
      {outcome === "confirming" && <Text>Confirming your email…</Text>}
      {outcome === "confirmed" && (
        <Stack>
          <Alert role="status" color="green" title="Your email is confirmed">
            Thank you: the club's emails will reach you.
          </Alert>
          <Anchor
            renderRoot={(props) =>
              signedIn ? <Link to="/campaigns" {...props} /> : <Link to="/sign-in" {...props} />
            }
          >
            {signedIn ? "Go to your campaigns" : "Sign in"}
          </Anchor>
        </Stack>
      )}
      {outcome === "failed" && (
        <Stack>
          <Alert role="status" color="yellow" title="This link didn't work">
            It may have expired, or be for an older address.{" "}
            {signedIn
              ? "Send yourself a new one."
              : "Sign in, and send yourself a new one from the reminder."}
          </Alert>
          {signedIn ? <SendAgain /> : null}
        </Stack>
      )}
    </Page>
  );
}

/** Sends a new confirmation link to the signed-in user. */
export function SendAgain({ compact = false }: { compact?: boolean }) {
  const { user } = useSession();
  const send = useSendConfirmationEmail();
  return (
    <Button
      variant={compact ? "light" : "filled"}
      size={compact ? "compact-sm" : "sm"}
      loading={send.isPending}
      onClick={() => {
        send
          .mutateAsync()
          .then(() => {
            notifications.show({
              color: "green",
              message: `Sent a new link to ${user?.email ?? "your email"}.`,
            });
          })
          .catch((error: unknown) => {
            notifications.show({
              color: "red",
              message: errorMessage(error, "The link wasn't sent. Try again."),
            });
          });
      }}
    >
      Send a new link
    </Button>
  );
}
