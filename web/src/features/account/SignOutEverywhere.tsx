import { Button, Group } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { useSignOutEverywhere } from "@/api/generated/endpoints/account/account";
import { ConfirmModal } from "@/components/ConfirmModal";
import { Section } from "@/components/Section";
import { useSessionStore } from "@/features/auth/session-context";
import { useOnline } from "@/lib/use-online";
import { errorMessage } from "@/lib/errors";

export function SignOutEverywhere() {
  const session = useSessionStore();
  const online = useOnline();
  const signOutEverywhere = useSignOutEverywhere();
  const [confirming, { open, close }] = useDisclosure(false);

  const confirm = async () => {
    try {
      await signOutEverywhere.mutateAsync();
      await session.signOut();
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "That didn't work. Try again."),
      });
      close();
    }
  };

  return (
    <Section
      title="Sign out everywhere"
      tone="danger"
      description="Signs out every device and browser, including this one. Use it if you've lost a device or think someone else is signed in."
    >
      <Group>
        <Button color="red" variant="light" onClick={open} disabled={!online}>
          Sign out everywhere
        </Button>
      </Group>
      <ConfirmModal
        opened={confirming}
        onClose={close}
        title="Sign out everywhere?"
        confirmLabel="Sign out everywhere"
        onConfirm={() => void confirm()}
        loading={signOutEverywhere.isPending}
      >
        You&apos;ll need to sign in again on every device, including this one.
      </ConfirmModal>
    </Section>
  );
}
