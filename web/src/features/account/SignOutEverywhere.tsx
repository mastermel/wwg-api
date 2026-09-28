import { Button, Group, Modal, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { useSignOutEverywhere } from "@/api/generated/endpoints/account/account";
import { AccountSection } from "@/features/account/AccountSection";
import { useSessionStore } from "@/features/auth/session-context";
import { useOnline } from "@/lib/use-online";

export function SignOutEverywhere() {
  const session = useSessionStore();
  const online = useOnline();
  const signOutEverywhere = useSignOutEverywhere();
  const [confirming, { open, close }] = useDisclosure(false);

  const confirm = async () => {
    try {
      await signOutEverywhere.mutateAsync();
      await session.signOut();
    } catch {
      notifications.show({ color: "red", message: "That didn't work. Try again." });
      close();
    }
  };

  return (
    <AccountSection
      title="Sign out everywhere"
      description="Signs out every device and browser, including this one. Use it if you've lost a device or think someone else is signed in."
    >
      <Group>
        <Button color="red" variant="light" onClick={open} disabled={!online}>
          Sign out everywhere
        </Button>
      </Group>
      <Modal opened={confirming} onClose={close} title="Sign out everywhere?" centered>
        <Text size="sm">
          You&apos;ll need to sign in again on every device, including this one.
        </Text>
        <Group justify="flex-end" mt="lg">
          <Button variant="default" onClick={close}>
            Cancel
          </Button>
          <Button color="red" loading={signOutEverywhere.isPending} onClick={() => void confirm()}>
            Sign out everywhere
          </Button>
        </Group>
      </Modal>
    </AccountSection>
  );
}
