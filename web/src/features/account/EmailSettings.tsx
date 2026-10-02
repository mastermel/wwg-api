import { Stack, Switch, Text } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import {
  getGetEmailSettingsQueryKey,
  useGetEmailSettings,
  useUpdateEmailSettings,
} from "@/api/generated/endpoints/account/account";
import type { EmailKind } from "@/api/generated/model";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { errorMessage } from "@/lib/errors";
import { useOnline } from "@/lib/use-online";

/** The campaign emails, by who gets them, in words (decision 0023). */
const groups: {
  title: string;
  kinds: { kind: EmailKind; label: string; description: string }[];
}[] = [
  {
    title: "Commanding an army",
    kinds: [
      {
        kind: "TurnStarted",
        label: "A new turn",
        description: "When you can give orders again, with what's new for your army.",
      },
      {
        kind: "TurnReviewed",
        label: "The Umpire's word on your turn",
        description: "Approved, sent back, reopened, or submitted for you.",
      },
      {
        kind: "ArmyGiven",
        label: "Given an army",
        description: "When you're made an army's commander.",
      },
    ],
  },
  {
    title: "Umpiring a campaign",
    kinds: [
      {
        kind: "ArmySubmitted",
        label: "An army submits its turn",
        description: "One for each army, as it submits.",
      },
      {
        kind: "AllSubmitted",
        label: "Every army has submitted",
        description: "With what's waiting for you as you start the next turn.",
      },
      {
        kind: "PlayerJoined",
        label: "A player joins",
        description: "When someone joins one of your campaigns.",
      },
    ],
  },
];

/**
 * The campaign emails the user gets (step 52a, decision 0023), each one theirs to turn off. The
 * account's own (password resets, a changed address) always go.
 */
export function EmailSettings() {
  const settings = useGetEmailSettings();
  return (
    <Section
      title="Email notifications"
      description="Campaign emails you get. Emails about your account always come."
    >
      <QueryState query={settings}>{(loaded) => <EmailSwitches muted={loaded.muted} />}</QueryState>
    </Section>
  );
}

function EmailSwitches({ muted }: { muted: readonly EmailKind[] }) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const update = useUpdateEmailSettings();

  const toggle = async (kind: EmailKind, on: boolean) => {
    const next = on ? muted.filter((k) => k !== kind) : [...muted, kind];
    try {
      const saved = await update.mutateAsync({ data: { muted: next } });
      // At once, so a quick next switch builds on this one, not the list before it.
      queryClient.setQueryData(getGetEmailSettingsQueryKey(), saved);
      notifications.show({ color: "green", message: "Saved your email settings." });
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "Your email settings weren't saved. Try again."),
      });
    }
  };

  return (
    <Stack gap="lg">
      {groups.map((group) => (
        <Stack key={group.title} gap="sm" role="group" aria-label={group.title}>
          <Text fw={600} size="sm">
            {group.title}
          </Text>
          {group.kinds.map(({ kind, label, description }) => (
            <Switch
              key={kind}
              label={label}
              description={description}
              checked={!muted.includes(kind)}
              disabled={!online || update.isPending}
              onChange={(event) => void toggle(kind, event.currentTarget.checked)}
            />
          ))}
        </Stack>
      ))}
    </Stack>
  );
}
