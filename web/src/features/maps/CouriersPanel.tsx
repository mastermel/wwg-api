import { ActionIcon, Badge, Group, List, Text } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { IconHandStop } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import {
  getListCouriersQueryKey,
  useStopCourier,
} from "@/api/generated/endpoints/intelligence/intelligence";
import type { ArmySummary, CourierResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { Section } from "@/components/Section";
import { hexName } from "@/features/maps/hex-grid";
import { errorMessage } from "@/lib/errors";
import { useOnline } from "@/lib/use-online";

/**
 * The couriers on their way, for the Umpire (step 49d, decision 0020): where each is, flagged
 * among the enemy, and stopping one. Nothing when there's none.
 */
export function CouriersPanel({
  campaignId,
  couriers,
  armies,
}: {
  campaignId: string;
  couriers: readonly CourierResponse[];
  armies: readonly ArmySummary[];
}) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const stop = useStopCourier();
  const [stopping, setStopping] = useState<CourierResponse | null>(null);
  if (couriers.length === 0) return null;
  const name = (id: string) => armies.find((a) => a.id === id)?.name ?? "An army";
  const route = (c: CourierResponse) => `${name(c.fromArmyId)} to ${name(c.toArmyId)}`;

  return (
    <Section title="Couriers" description="Reports on their way between allies.">
      <List size="sm" spacing={6} listStyleType="none" p={0} aria-label="Couriers">
        {couriers.map((courier) => (
          <List.Item key={courier.reportId}>
            <Group justify="space-between" wrap="nowrap" gap="xs">
              <Text size="sm">
                {route(courier)}, sent turn {courier.sentTurn}: {hexName(courier)}
                {courier.arrivesNext ? ", arriving next turn" : ""}.
              </Text>
              <Group gap={4} wrap="nowrap">
                {courier.amongTheEnemy && (
                  <Badge color="orange" variant="light">
                    Among the enemy
                  </Badge>
                )}
                <ActionIcon
                  variant="subtle"
                  color="red"
                  aria-label={`Stop the courier from ${route(courier)}`}
                  disabled={!online}
                  onClick={() => {
                    setStopping(courier);
                  }}
                >
                  <IconHandStop size={16} aria-hidden />
                </ActionIcon>
              </Group>
            </Group>
          </List.Item>
        ))}
      </List>
      <ConfirmModal
        opened={stopping !== null}
        onClose={() => {
          setStopping(null);
        }}
        title="Stop the courier?"
        confirmLabel="Stop it"
        loading={stop.isPending}
        onConfirm={() => {
          if (!stopping) return;
          void stop
            .mutateAsync({ id: stopping.reportId })
            .then(() => {
              notifications.show({
                color: "green",
                message: `Stopped the courier from ${route(stopping)}.`,
              });
              setStopping(null);
            })
            .catch((error: unknown) => {
              notifications.show({
                color: "red",
                message: errorMessage(error, "The courier couldn't be stopped. Try again."),
              });
            })
            .finally(
              () =>
                void queryClient.invalidateQueries({
                  queryKey: getListCouriersQueryKey(campaignId),
                }),
            );
        }}
      >
        Captured or lost: the report never arrives.
      </ConfirmModal>
    </Section>
  );
}
