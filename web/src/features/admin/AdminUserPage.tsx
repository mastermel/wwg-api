import { Alert, Anchor, Badge, Button, Group, Modal, SimpleGrid, Stack, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconArrowLeft, IconTrash } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import type { ReactNode } from "react";
import {
  getListUsersQueryKey,
  useDeleteUser,
  useGetUser,
} from "@/api/generated/endpoints/admin/admin";
import type { UserDetails } from "@/api/generated/model";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { useSession } from "@/features/auth/session-context";
import { formatDate, formatDateTime } from "@/lib/format";
import { useOnline } from "@/lib/use-online";

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <Text size="sm" c="dimmed">
        {label}
      </Text>
      <Text component="div">{children}</Text>
    </div>
  );
}

function BackToUsers() {
  return (
    <Anchor size="sm" renderRoot={(props) => <Link to="/admin/users" {...props} />}>
      <Group gap={4}>
        <IconArrowLeft size={16} aria-hidden /> All users
      </Group>
    </Anchor>
  );
}

export function AdminUserPage({ id }: { id: string }) {
  const user = useGetUser(id, { query: { meta: { persist: false } } });
  const title = user.data ? `${user.data.firstName} ${user.data.lastName}` : "User";

  return (
    <Page title={title}>
      <BackToUsers />
      <QueryState query={user}>{(details) => <UserDetailsView user={details} />}</QueryState>
    </Page>
  );
}

function UserDetailsView({ user }: { user: UserDetails }) {
  const { user: me } = useSession();
  const online = useOnline();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const remove = useDeleteUser();
  const [confirming, { open, close }] = useDisclosure(false);
  const isMe = me?.id === user.id;

  const confirmDelete = async () => {
    try {
      await remove.mutateAsync({ id: user.id });
      // Every page of the list (any search) is now out of date.
      await queryClient.invalidateQueries({ queryKey: getListUsersQueryKey() });
      notifications.show({
        color: "green",
        message: `Deleted ${user.firstName} ${user.lastName}.`,
      });
      await navigate({ to: "/admin/users" });
    } catch {
      notifications.show({ color: "red", message: "That user couldn't be deleted. Try again." });
      close();
    }
  };

  return (
    <Stack gap="lg" maw={640}>
      <SimpleGrid cols={{ base: 1, sm: 2 }}>
        <Field label="Email">{user.email}</Field>
        <Field label="Registered">{formatDate(user.createdAt)}</Field>
        <Field label="Role">{user.isAdmin ? <Badge variant="light">Admin</Badge> : "Member"}</Field>
        {user.lockedOutUntil && (
          <Field label="Sign-in locked until">{formatDateTime(user.lockedOutUntil)}</Field>
        )}
      </SimpleGrid>
      {user.isAdmin && (
        <Alert color="gray">
          Admins are set by the server&apos;s Admin:Emails setting, not here.
        </Alert>
      )}
      <Group>
        <Button
          color="red"
          variant="light"
          leftSection={<IconTrash size={16} aria-hidden />}
          onClick={open}
          disabled={isMe || !online}
        >
          Delete user
        </Button>
        {isMe && (
          <Text size="sm" c="dimmed">
            You can&apos;t delete your own account.
          </Text>
        )}
      </Group>
      <Modal opened={confirming} onClose={close} title="Delete this user?" centered>
        <Text size="sm">
          {user.firstName} {user.lastName} ({user.email}) will be deleted and signed out everywhere.
          This can&apos;t be undone.
        </Text>
        <Group justify="flex-end" mt="lg">
          <Button variant="default" onClick={close}>
            Cancel
          </Button>
          <Button color="red" loading={remove.isPending} onClick={() => void confirmDelete()}>
            Delete user
          </Button>
        </Group>
      </Modal>
    </Stack>
  );
}
