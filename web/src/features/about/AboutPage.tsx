import { Box, ColorSwatch, Group, Loader, SimpleGrid, Text } from "@mantine/core";
import { useGetHealth } from "@/api/generated/endpoints/health/health";
import type { HealthStatus } from "@/api/generated/model";
import { Page } from "@/components/Page";
import { Section } from "@/components/Section";
import { ApiError } from "@/lib/api-fetch";
import { appVersion } from "@/lib/app-version";

const statusColors: Record<HealthStatus, string> = {
  Healthy: "green",
  Degraded: "yellow",
  Unhealthy: "red",
};

export function AboutPage() {
  return (
    <Page title="About" summary="The Wasatch Wargamers campaign app.">
      <Box maw={720}>
        <Section title="This version" description="Useful when reporting a problem.">
          <SimpleGrid cols={{ base: 1, xs: 2 }} spacing="lg">
            <div>
              <Text size="sm" c="dimmed">
                Version
              </Text>
              <Text fw={500}>{appVersion}</Text>
            </div>
            <div>
              <Text size="sm" c="dimmed">
                Server status
              </Text>
              <ApiHealth />
            </div>
          </SimpleGrid>
        </Section>
      </Box>
    </Page>
  );
}

/**
 * The status in words, with a coloured dot beside it: the word carries the meaning (and the
 * contrast), the dot is extra. White on Mantine's green falls short of AA.
 */
function Status({ label, color }: { label: string; color: string }) {
  return (
    <Group gap={8} wrap="nowrap">
      <ColorSwatch color={`var(--mantine-color-${color}-6)`} size={10} withShadow={false} />
      <Text fw={500}>{label}</Text>
    </Group>
  );
}

function ApiHealth() {
  // Live status: never saved for offline use.
  const health = useGetHealth({ query: { meta: { persist: false } } });

  if (health.fetchStatus === "paused") {
    return <Status label="Offline" color="gray" />;
  }

  if (health.isPending) {
    return <Loader size="xs" role="status" aria-label="Checking the server" />;
  }

  // An unhealthy API answers 503 with the same body, which arrives as an error.
  const status: HealthStatus | undefined = health.isSuccess
    ? health.data.status
    : health.error instanceof ApiError
      ? health.error.body?.status
      : undefined;

  if (!status) {
    return <Status label="Unreachable" color="gray" />;
  }

  return <Status label={status} color={statusColors[status]} />;
}
