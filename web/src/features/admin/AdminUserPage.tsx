import {
  Alert,
  Anchor,
  Badge,
  Button,
  Group,
  List,
  SimpleGrid,
  Stack,
  Switch,
  Text,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconMasksTheater, IconSwords, IconTrash } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import type { ReactNode } from "react";
import {
  getGetUserQueryKey,
  getListUsersQueryKey,
  useDeleteUser,
  useGetUser,
  useSetManager,
} from "@/api/generated/endpoints/admin/admin";
import type { UserDetails } from "@/api/generated/model";
import { BackLink } from "@/components/BackLink";
import { ConfirmModal } from "@/components/ConfirmModal";
import { EmptyState } from "@/components/EmptyState";
import { Page } from "@/components/Page";
import { Section } from "@/components/Section";
import { QueryState } from "@/components/QueryState";
import { useMasqueradeSession } from "@/features/admin/use-masquerade-session";
import { useSession } from "@/features/auth/session-context";
import { formatDate, formatDateTime } from "@/lib/format";
import { useOnline } from "@/lib/use-online";
import { errorMessage } from "@/lib/errors";

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

export function AdminUserPage({ id }: { id: string }) {
  const user = useGetUser(id, { query: { meta: { persist: false } } });
  const title = user.data ? `${user.data.firstName} ${user.data.lastName}` : "User";

  return (
    <Page
      title={title}
      back={
        <BackLink renderLink={(props) => <Link to="/admin/users" {...props} />}>All users</BackLink>
      }
      summary={
        user.data && (
          <Group gap="xs">
            <Text span inherit>
              {user.data.email}
            </Text>
            {user.data.isAdmin && (
              <Badge size="sm" variant="light">
                Admin
              </Badge>
            )}
            {user.data.isManager && (
              <Badge size="sm" variant="light" color="gray">
                Manager
              </Badge>
            )}
          </Group>
        )
      }
    >
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
  const [masquerading, masqueradeModal] = useDisclosure(false);
  const masquerade = useMasqueradeSession();
  const isMe = me?.id === user.id;

  const confirmDelete = async () => {
    try {
      await remove.mutateAsync({ id: user.id });
      // Their memberships, Umpire roles and commands are gone: nearly anything cached can be out
      // of date, so refetch it all (only what's on screen is fetched now).
      await queryClient.invalidateQueries();
      notifications.show({
        color: "green",
        message: `Deleted ${user.firstName} ${user.lastName}.`,
      });
      await navigate({ to: "/admin/users" });
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "That user couldn't be deleted. Try again."),
      });
      close();
    }
  };

  return (
    <Stack gap="xl" maw={720}>
      <Section title="Details">
        <Stack gap="md">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <Field label="Email">{user.email}</Field>
            <Field label="Registered">{formatDate(user.createdAt)}</Field>
            <Field label="Role">
              {user.isAdmin ? <Badge variant="light">Admin</Badge> : "Member"}
            </Field>
            {user.lockedOutUntil && (
              <Field label="Sign-in locked until">{formatDateTime(user.lockedOutUntil)}</Field>
            )}
          </SimpleGrid>
          {user.isAdmin && (
            <Alert role="status" color="gray">
              Admins are set by the server&apos;s Admin:Emails setting, not here.
            </Alert>
          )}
        </Stack>
      </Section>
      <ManagerSection user={user} />
      <UserCampaigns user={user} />
      <Section
        title="Masquerade"
        description="Use the app as this person, with exactly their permissions, to see what they see."
      >
        <Group>
          <Button
            variant="light"
            leftSection={<IconMasksTheater size={16} aria-hidden />}
            onClick={masqueradeModal.open}
            disabled={isMe || !online}
          >
            Masquerade as {user.firstName}
          </Button>
          {isMe && (
            <Text size="sm" c="dimmed">
              You can&apos;t masquerade as yourself.
            </Text>
          )}
        </Group>
      </Section>
      <Section
        title="Danger zone"
        tone="danger"
        description="Deletes the account and signs them out everywhere. Their campaigns stay."
      >
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
      </Section>
      <ConfirmModal
        opened={masquerading}
        onClose={masqueradeModal.close}
        title={`Masquerade as ${user.firstName}?`}
        confirmLabel="Masquerade"
        color="navy"
        onConfirm={() => void masquerade.start(user).then(masqueradeModal.close)}
        loading={masquerade.busy}
      >
        You&apos;ll use the app as {user.firstName} {user.lastName}, able to do what they can and no
        more, until you end the masquerade from your account menu (or after 8 hours). What&apos;s
        saved on this device is cleared, now and when it ends.
      </ConfirmModal>
      <ConfirmModal
        opened={confirming}
        onClose={close}
        title="Delete this user?"
        confirmLabel="Delete user"
        onConfirm={() => void confirmDelete()}
        loading={remove.isPending}
      >
        {user.firstName} {user.lastName} ({user.email}) will be deleted and signed out everywhere.
        This can&apos;t be undone.
      </ConfirmModal>
    </Stack>
  );
}

/** Managers edit the library of factions and units (decision 0015); Admins make them. */
function ManagerSection({ user }: { user: UserDetails }) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const setManager = useSetManager();

  const change = async (manager: boolean) => {
    try {
      await setManager.mutateAsync({ id: user.id, data: { manager } });
      notifications.show({
        color: "green",
        message: manager
          ? `${user.firstName} is now a Manager.`
          : `${user.firstName} is no longer a Manager.`,
      });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getGetUserQueryKey(user.id) }),
        queryClient.invalidateQueries({ queryKey: getListUsersQueryKey() }),
      ]);
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "That couldn't be changed. Try again."),
      });
    }
  };

  return (
    <Section
      title="Library"
      description="Managers add and edit the library's factions and units, as Admins can."
    >
      <Switch
        label="Manager"
        checked={user.isManager}
        disabled={!online || setManager.isPending}
        onChange={(event) => void change(event.currentTarget.checked)}
      />
    </Section>
  );
}

function UserCampaigns({ user }: { user: UserDetails }) {
  return (
    <Section title="Campaigns" description="The campaigns they're in, and their role in each.">
      {user.campaigns.length === 0 ? (
        <EmptyState icon={IconSwords} title="Not in any campaigns." />
      ) : (
        <List listStyleType="none" spacing={4} p={0}>
          {user.campaigns.map((campaign) => (
            <List.Item key={campaign.id}>
              <Group gap="xs">
                <Anchor
                  renderRoot={(props) => (
                    <Link to="/campaigns/$id" params={{ id: campaign.id }} {...props} />
                  )}
                >
                  {campaign.name}
                </Anchor>
                <Badge size="sm" variant={campaign.role === "Umpire" ? "filled" : "light"}>
                  {campaign.role}
                </Badge>
              </Group>
            </List.Item>
          ))}
        </List>
      )}
    </Section>
  );
}
