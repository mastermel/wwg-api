import {
  Anchor,
  Badge,
  Checkbox,
  Divider,
  Group,
  Pagination,
  Paper,
  Table,
  Text,
  TextInput,
} from "@mantine/core";
import { useDebouncedCallback } from "@mantine/hooks";
import { IconSearch } from "@tabler/icons-react";
import { keepPreviousData } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useListAllCampaigns } from "@/api/generated/endpoints/admin/admin";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { formatDate } from "@/lib/format";

const pageSize = 25;

interface AdminCampaignsPageProps {
  search: string;
  withoutUmpire: boolean;
  page: number;
}

/** Every campaign, for admins, including those left without an Umpire. */
export function AdminCampaignsPage({ search, withoutUmpire, page }: AdminCampaignsPageProps) {
  const navigate = useNavigate();
  const campaigns = useListAllCampaigns(
    { search: search || undefined, withoutUmpire: withoutUmpire || undefined, page, pageSize },
    // Admin data is never saved for offline use.
    { query: { meta: { persist: false }, placeholderData: keepPreviousData } },
  );

  const show = (changes: { search?: string; withoutUmpire?: boolean; page?: number }) =>
    navigate({
      to: "/admin/campaigns",
      search: {
        search: (changes.search ?? search) || undefined,
        withoutUmpire: (changes.withoutUmpire ?? withoutUmpire) || undefined,
        page: changes.page,
      },
      replace: changes.page === undefined,
    });

  const setSearch = useDebouncedCallback((value: string) => {
    void show({ search: value.trim() });
  }, 300);

  return (
    <Page title="All campaigns" summary="Every campaign, including those without an Umpire.">
      <Paper withBorder>
        <Group align="flex-end" px="lg" py="md">
          <TextInput
            label="Search"
            description="By campaign name."
            leftSection={<IconSearch size={16} aria-hidden />}
            defaultValue={search}
            onChange={(event) => {
              setSearch(event.currentTarget.value);
            }}
            w={{ base: "100%", xs: 320 }}
            type="search"
          />
          <Checkbox
            label="Only those without an Umpire"
            checked={withoutUmpire}
            onChange={(event) => void show({ withoutUmpire: event.currentTarget.checked })}
            mb={8}
          />
        </Group>
        <Divider />
        <QueryState query={campaigns}>
          {(result) =>
            result.items.length === 0 ? (
              <Text c="dimmed" px="lg" py="md">
                {search || withoutUmpire ? "No campaigns match." : "No campaigns yet."}
              </Text>
            ) : (
              <>
                <Table.ScrollContainer minWidth={360} type="native">
                  <Table highlightOnHover horizontalSpacing="lg">
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Name</Table.Th>
                        <Table.Th>Umpire</Table.Th>
                        <Table.Th>Players</Table.Th>
                        {/* Least needed; phones leave it out rather than scroll sideways. */}
                        <Table.Th visibleFrom="sm">Created</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {result.items.map((campaign) => (
                        <Table.Tr key={campaign.id}>
                          <Table.Td>
                            <Anchor
                              renderRoot={(props) => (
                                <Link to="/campaigns/$id" params={{ id: campaign.id }} {...props} />
                              )}
                            >
                              {campaign.name}
                            </Anchor>
                          </Table.Td>
                          <Table.Td>
                            {campaign.umpireName ?? (
                              <Badge color="orange" variant="light">
                                None
                              </Badge>
                            )}
                          </Table.Td>
                          <Table.Td>{campaign.playerCount}</Table.Td>
                          <Table.Td visibleFrom="sm">{formatDate(campaign.createdAt)}</Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Table.ScrollContainer>
                <Divider />
                <Group justify="space-between" px="lg" py="sm">
                  <Text size="sm" c="dimmed">
                    {result.totalCount} {result.totalCount === 1 ? "campaign" : "campaigns"}
                  </Text>
                  {result.totalCount > pageSize && (
                    <Pagination
                      total={Math.ceil(result.totalCount / pageSize)}
                      value={page}
                      onChange={(next) => void show({ page: next })}
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
      </Paper>
    </Page>
  );
}
