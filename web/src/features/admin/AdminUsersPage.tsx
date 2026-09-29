import {
  Anchor,
  Badge,
  Box,
  Divider,
  Group,
  Pagination,
  Table,
  Text,
  TextInput,
} from "@mantine/core";
import { useDebouncedCallback } from "@mantine/hooks";
import { IconSearch, IconUsers } from "@tabler/icons-react";
import { keepPreviousData } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useListUsers } from "@/api/generated/endpoints/admin/admin";
import { EmptyState } from "@/components/EmptyState";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { formatDate } from "@/lib/format";

const pageSize = 25;

export function AdminUsersPage({ search, page }: { search: string; page: number }) {
  const navigate = useNavigate();
  const users = useListUsers(
    { search: search || undefined, page, pageSize },
    // Admin data is never saved for offline use (it includes everyone's email).
    { query: { meta: { persist: false }, placeholderData: keepPreviousData } },
  );

  const setSearch = useDebouncedCallback((value: string) => {
    void navigate({
      to: "/admin/users",
      search: { search: value.trim() || undefined, page: undefined },
      replace: true,
    });
  }, 300);

  return (
    <Page title="Users" summary="Everyone with an account.">
      <Section title="Accounts" flush>
        <Box px="lg" py="md">
          <TextInput
            label="Search"
            description="By first name, last name or email."
            leftSection={<IconSearch size={16} aria-hidden />}
            defaultValue={search}
            onChange={(event) => {
              setSearch(event.currentTarget.value);
            }}
            maw={420}
            type="search"
          />
        </Box>
        <Divider />
        <QueryState query={users}>
          {(result) =>
            result.items.length === 0 ? (
              <EmptyState
                icon={IconUsers}
                title={search ? `No users match "${search}".` : "No users yet."}
              />
            ) : (
              <>
                <Table highlightOnHover horizontalSpacing="lg">
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Name</Table.Th>
                      {/* On phones the email sits under the name, rather than scroll sideways. */}
                      <Table.Th visibleFrom="sm">Email</Table.Th>
                      <Table.Th visibleFrom="sm">Registered</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {result.items.map((user) => (
                      <Table.Tr key={user.id}>
                        <Table.Td>
                          <Group gap="xs" wrap="nowrap">
                            <Anchor
                              fw={500}
                              renderRoot={(props) => (
                                <Link to="/admin/users/$id" params={{ id: user.id }} {...props} />
                              )}
                            >
                              {user.lastName}, {user.firstName}
                            </Anchor>
                            {user.isAdmin && (
                              <Badge size="sm" variant="light">
                                Admin
                              </Badge>
                            )}
                          </Group>
                          <Text size="xs" c="dimmed" hiddenFrom="sm">
                            {user.email}
                          </Text>
                        </Table.Td>
                        <Table.Td visibleFrom="sm">{user.email}</Table.Td>
                        <Table.Td visibleFrom="sm">{formatDate(user.createdAt)}</Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
                <Divider />
                <Group justify="space-between" px="lg" py="sm">
                  <Text size="sm" c="dimmed">
                    {result.totalCount} {result.totalCount === 1 ? "user" : "users"}
                  </Text>
                  {result.totalCount > pageSize && (
                    <Pagination
                      total={Math.ceil(result.totalCount / pageSize)}
                      value={page}
                      onChange={(next) =>
                        void navigate({
                          to: "/admin/users",
                          search: { search: search || undefined, page: next },
                        })
                      }
                      getControlProps={(control) => ({
                        "aria-label": control === "previous" ? "Previous page" : "Next page",
                      })}
                    />
                  )}
                </Group>
              </>
            )
          }
        </QueryState>
      </Section>
    </Page>
  );
}
