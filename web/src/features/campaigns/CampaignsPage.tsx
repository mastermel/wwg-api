import {
  Anchor,
  Badge,
  Button,
  Card,
  Group,
  Pagination,
  SimpleGrid,
  Stack,
  Text,
} from "@mantine/core";
import { IconPlus } from "@tabler/icons-react";
import { Link, useNavigate } from "@tanstack/react-router";
import { useListMyCampaigns } from "@/api/generated/endpoints/campaigns/campaigns";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { useOnline } from "@/lib/use-online";

const pageSize = 25;

export function CampaignsPage({ page = 1 }: { page?: number }) {
  const navigate = useNavigate();
  const online = useOnline();
  const campaigns = useListMyCampaigns({ page, pageSize });

  return (
    <Page title="Campaigns">
      <Group>
        <Button
          leftSection={<IconPlus size={16} aria-hidden />}
          disabled={!online}
          renderRoot={(props) => <Link to="/campaigns/new" {...props} />}
        >
          New campaign
        </Button>
      </Group>
      <QueryState query={campaigns}>
        {(result) =>
          result.items.length === 0 ? (
            <Stack gap={4}>
              <Text fw={500}>You&apos;re not in any campaigns yet.</Text>
              <Text c="dimmed">
                Start one with New campaign, or ask an Umpire for their campaign&apos;s join link.
              </Text>
            </Stack>
          ) : (
            <>
              <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
                {result.items.map((campaign) => (
                  <Card key={campaign.id} withBorder padding="lg" radius="md" component="article">
                    <Group justify="space-between" align="flex-start" wrap="nowrap">
                      <Anchor
                        fw={600}
                        size="lg"
                        renderRoot={(props) => (
                          <Link to="/campaigns/$id" params={{ id: campaign.id }} {...props} />
                        )}
                      >
                        {campaign.name}
                      </Anchor>
                      <Badge variant="light" color={campaign.myRole === "Umpire" ? "navy" : "gray"}>
                        {campaign.myRole}
                      </Badge>
                    </Group>
                    <Text size="sm" c="dimmed" mt="xs">
                      Umpire: {campaign.umpireName ?? "none"} ·{" "}
                      {campaign.playerCount === 1
                        ? "1 player"
                        : `${String(campaign.playerCount)} players`}
                    </Text>
                  </Card>
                ))}
              </SimpleGrid>
              {result.totalCount > pageSize && (
                <Pagination
                  total={Math.ceil(result.totalCount / pageSize)}
                  value={page}
                  onChange={(next) => void navigate({ to: "/campaigns", search: { page: next } })}
                  getControlProps={(control) => ({
                    "aria-label": control === "previous" ? "Previous page" : "Next page",
                  })}
                />
              )}
            </>
          )
        }
      </QueryState>
    </Page>
  );
}
