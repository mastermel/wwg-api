import { Badge, Group, Loader, Text } from "@mantine/core";
import { useGetHealth } from "@/api/generated/endpoints/health/health";
import type { HealthStatus } from "@/api/generated/model";
import { Page } from "@/components/Page";
import { ApiError } from "@/lib/api-fetch";
import { appVersion } from "@/lib/app-version";

const statusColors: Record<HealthStatus, string> = {
  Healthy: "green",
  Degraded: "yellow",
  Unhealthy: "red",
};

export function AboutPage() {
  return (
    <Page title="About">
      <Text>WWG Campaigner is the Wasatch Wargamers campaign app.</Text>
      <Group gap="xs">
        <Text fw={500}>Version:</Text>
        <Text>{appVersion}</Text>
      </Group>
      <Group gap="xs">
        <Text fw={500}>Server status:</Text>
        <ApiHealth />
      </Group>
    </Page>
  );
}

function ApiHealth() {
  const health = useGetHealth();

  if (health.isPending) {
    return <Loader size="xs" aria-label="Checking the server" />;
  }

  // An unhealthy API answers 503 with the same body, which arrives as an error.
  const status: HealthStatus | undefined = health.isSuccess
    ? health.data.status
    : health.error instanceof ApiError
      ? health.error.body?.status
      : undefined;

  if (!status) {
    return <Badge color="gray">Unreachable</Badge>;
  }

  return <Badge color={statusColors[status]}>{status}</Badge>;
}
