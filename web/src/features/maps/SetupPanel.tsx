import { Alert, Button, Group, List, Stack, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconFlag3, IconMapPin } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { useStartCampaign } from "@/api/generated/endpoints/turns/turns";
import type { CampaignTurnsResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { Section } from "@/components/Section";
import { ArmyBadge } from "@/features/armies/identity/ArmyBadge";
import { armyColorVar } from "@/features/armies/identity/army-colors";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";
import type { PlacedUnit } from "@/features/maps/stacks";
import { UnitSymbol } from "@/features/units/UnitSymbol";
import { errorMessage } from "@/lib/errors";
import { useOnline } from "@/lib/use-online";

interface SetupPanelProps {
  campaignId: string;
  turns: CampaignTurnsResponse;
  /** Every unit, with where it's placed (null: not yet), by army. */
  units: { unit: PlacedUnit["unit"]; army: PlacedUnit["army"]; placed: boolean }[];
  /** The unit being placed, if one is. */
  placing: string | null;
  onPlace: (unitId: string) => void;
}

/**
 * The Umpire's setup (turn 0, DESIGN.md §3.13): every unit and whether it's on the map yet, what
 * stops the campaign starting, and Start campaign.
 */
export function SetupPanel({ campaignId, turns, units, placing, onPlace }: SetupPanelProps) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const start = useStartCampaign();
  const [confirming, { open, close }] = useDisclosure(false);
  const armies = [...new Map(units.map((u) => [u.army.id, u.army])).values()];

  const confirmStart = async () => {
    try {
      await start.mutateAsync({ id: campaignId });
      notifications.show({ color: "green", message: "The campaign has started: turn 1 is open." });
      await refreshCampaign(queryClient, campaignId);
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "The campaign couldn't be started. Try again."),
      });
    }
    close();
  };

  return (
    <Section title="Setting up" description="Place every unit on the map, then start the campaign.">
      <Stack>
        {turns.startProblems.length > 0 && (
          <Alert role="status" color="gray" title="Before it can start">
            <List size="sm" spacing={2}>
              {turns.startProblems.map((problem) => (
                <List.Item key={problem}>{problem}</List.Item>
              ))}
            </List>
          </Alert>
        )}
        {armies.map((army) => (
          <Stack key={army.id} gap={6}>
            <Text fw={600} size="sm">
              <ArmyBadge army={army} />
            </Text>
            {units
              .filter((u) => u.army.id === army.id)
              .map(({ unit, placed }) => (
                <Group key={unit.id} justify="space-between" wrap="nowrap" gap="xs">
                  <Group gap="xs" wrap="nowrap" miw={0}>
                    <UnitSymbol type={unit.type} color={armyColorVar(army.color)} width={26} />
                    <div style={{ minWidth: 0 }}>
                      <Text size="sm" truncate>
                        {unit.name}
                      </Text>
                      <Text size="xs" c="dimmed">
                        {placed ? "On the map" : "Not placed yet"}
                      </Text>
                    </div>
                  </Group>
                  <Button
                    size="compact-sm"
                    variant={placed ? "subtle" : "light"}
                    leftSection={<IconMapPin size={14} aria-hidden />}
                    disabled={!online || placing === unit.id}
                    aria-label={`${placed ? "Move" : "Place"} ${unit.name}`}
                    onClick={() => {
                      onPlace(unit.id);
                    }}
                  >
                    {placed ? "Move" : "Place"}
                  </Button>
                </Group>
              ))}
          </Stack>
        ))}
        <Button
          leftSection={<IconFlag3 size={16} aria-hidden />}
          disabled={!online || turns.startProblems.length > 0}
          onClick={open}
        >
          Start campaign
        </Button>
      </Stack>
      <ConfirmModal
        opened={confirming}
        onClose={close}
        title="Start the campaign?"
        confirmLabel="Start campaign"
        color="navy"
        onConfirm={() => void confirmStart()}
        loading={start.isPending}
      >
        Where units are now becomes turn 0, and turn 1 opens: every army&apos;s commander can give
        orders. Units can&apos;t be taken off the map, or armies and units deleted, after this.
      </ConfirmModal>
    </Section>
  );
}
