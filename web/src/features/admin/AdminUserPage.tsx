import { Alert, Anchor, Badge, Button, Group, List, SimpleGrid, Stack, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconTrash } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { useDeleteUser, useGetUser } from "@/api/generated/endpoints/admin/admin";
import type { UserDetails } from "@/api/generated/model";
import { BackLink } from "@/components/BackLink";
import { ConfirmModal } from "@/components/ConfirmModal";
import { Page } from "@/components/Page";
import { Section } from "@/components/Section";
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
    } catch {
      notifications.show({ color: "red", message: "That user couldn't be deleted. Try again." });
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
            <Alert color="gray">
              Admins are set by the server&apos;s Admin:Emails setting, not here.
            </Alert>
          )}
        </Stack>
      </Section>
      <UserCampaigns user={user} />
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

function UserCampaigns({ user }: { user: UserDetails }) {
  return (
    <Section title="Campaigns" description="The campaigns they're in, and their role in each.">
      {user.campaigns.length === 0 ? (
        <Text c="dimmed">Not in any campaigns.</Text>
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
