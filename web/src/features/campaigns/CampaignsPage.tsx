import {
  Anchor,
  Badge,
  Button,
  Card,
  Group,
  Pagination,
  Paper,
  SimpleGrid,
  Stack,
  Text,
} from "@mantine/core";
import { IconCrown, IconPlus, IconSwords, IconUsers } from "@tabler/icons-react";
import { Link, useNavigate } from "@tanstack/react-router";
import { useListMyCampaigns } from "@/api/generated/endpoints/campaigns/campaigns";
import { EmptyState } from "@/components/EmptyState";
import { Page } from "@/components/Page";
import classes from "@/features/campaigns/CampaignsPage.module.css";
import { QueryState } from "@/components/QueryState";
import { useOnline } from "@/lib/use-online";

const pageSize = 25;

export function CampaignsPage({ page = 1 }: { page?: number }) {
  const navigate = useNavigate();
  const online = useOnline();
  const campaigns = useListMyCampaigns({ page, pageSize });

  const newCampaign = (
    <Button
      leftSection={<IconPlus size={16} aria-hidden />}
      disabled={!online}
      renderRoot={(props) => <Link to="/campaigns/new" {...props} />}
    >
      New campaign
    </Button>
  );

  return (
    <Page title="Campaigns" summary="The campaigns you run or play in." actions={newCampaign}>
      <QueryState query={campaigns}>
        {(result) =>
          result.items.length === 0 ? (
            <Paper withBorder>
              <EmptyState icon={IconSwords} title="You're not in any campaigns yet.">
                Start one with New campaign, or ask an Umpire for their campaign&apos;s join link.
              </EmptyState>
            </Paper>
          ) : (
            <Stack gap="lg">
              <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }} spacing="lg">
                {result.items.map((campaign) => (
                  <Card
                    key={campaign.id}
                    withBorder
                    padding="lg"
                    component="article"
                    className={classes.card}
                    data-role={campaign.myRole}
                  >
                    <Group justify="space-between" align="flex-start" wrap="nowrap" gap="sm">
                      <Anchor
                        fw={650}
                        size="lg"
                        underline="never"
                        className={classes.link}
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
                    <Stack gap={4} mt="md">
                      <Group gap={6} wrap="nowrap">
                        <IconCrown size={16} aria-hidden color="var(--mantine-color-dimmed)" />
                        <Text size="sm" c="dimmed">
                          Umpire: {campaign.umpireName ?? "none"}
                        </Text>
                      </Group>
                      <Group gap={6} wrap="nowrap">
                        <IconUsers size={16} aria-hidden color="var(--mantine-color-dimmed)" />
                        <Text size="sm" c="dimmed">
                          {campaign.playerCount === 1
                            ? "1 player"
                            : `${String(campaign.playerCount)} players`}
                        </Text>
                      </Group>
                    </Stack>
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
            </Stack>
          )
        }
      </QueryState>
    </Page>
  );
}
