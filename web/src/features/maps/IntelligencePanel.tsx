import { Button, Group, List, Stack, Text } from "@mantine/core";
import { IconMailForward } from "@tabler/icons-react";
import { useState } from "react";
import type { ArmySummary, ReportResponse } from "@/api/generated/model";
import { Section } from "@/components/Section";
import { SendReportModal } from "@/features/maps/SendReportModal";
import { useOnline } from "@/lib/use-online";

interface IntelligencePanelProps {
  campaignId: string;
  reports: readonly ReportResponse[];
  /** The viewer's armies (a commander's own). */
  mine: readonly ArmySummary[];
  armies: readonly ArmySummary[];
  /** The report shown on the map, if any. */
  shown: string | null;
  onShow: (reportId: string | null) => void;
}

const armyName = (armies: readonly ArmySummary[], id: string) =>
  armies.find((a) => a.id === id)?.name ?? "An army";

/**
 * A commander's intelligence (step 49d, decision 0020): reports from allies, kept, each shown on
 * the map on request; reports sent; and sending one.
 */
export function IntelligencePanel({
  campaignId,
  reports,
  mine,
  armies,
  shown,
  onShow,
}: IntelligencePanelProps) {
  const online = useOnline();
  const [sending, setSending] = useState(false);
  const mineIds = new Set(mine.map((a) => a.id));
  const received = reports.filter((r) => mineIds.has(r.toArmyId));
  const sent = reports.filter((r) => mineIds.has(r.fromArmyId));

  return (
    <Section title="Intelligence" description="Reports between allies, carried by courier.">
      <Stack gap="md">
        {received.length > 0 && (
          <Stack gap={4}>
            <Text size="sm" fw={600}>
              Received
            </Text>
            <List size="sm" spacing={8} listStyleType="none" p={0} aria-label="Reports received">
              {received.map((report) => (
                <List.Item key={report.id}>
                  <Stack gap={2}>
                    <Text size="sm">
                      From {armyName(armies, report.fromArmyId)}, sent turn {report.sentTurn}
                      {report.arrivedTurn ? `, arrived turn ${String(report.arrivedTurn)}` : ""}.
                    </Text>
                    {report.note && (
                      <Text size="sm" c="dimmed" style={{ whiteSpace: "pre-wrap" }}>
                        “{report.note}”
                      </Text>
                    )}
                    <Group gap="xs">
                      {report.snapshot && (
                        <Button
                          size="compact-sm"
                          variant={shown === report.id ? "light" : "default"}
                          aria-pressed={shown === report.id}
                          onClick={() => {
                            onShow(shown === report.id ? null : report.id);
                          }}
                        >
                          {shown === report.id
                            ? "Hide their units"
                            : `Show their ${String(report.snapshot.length)} ${report.snapshot.length === 1 ? "unit" : "units"}`}
                        </Button>
                      )}
                      {report.sightingCount > 0 && (
                        <Text size="xs" c="dimmed">
                          {report.sightingCount}{" "}
                          {report.sightingCount === 1 ? "sighting" : "sightings"}, on their turns.
                        </Text>
                      )}
                    </Group>
                  </Stack>
                </List.Item>
              ))}
            </List>
          </Stack>
        )}
        {sent.length > 0 && (
          <Stack gap={4}>
            <Text size="sm" fw={600}>
              Sent
            </Text>
            <List size="sm" spacing={2} aria-label="Reports sent">
              {sent.map((report) => (
                <List.Item key={report.id}>
                  To {armyName(armies, report.toArmyId)}, turn {report.sentTurn}.
                </List.Item>
              ))}
            </List>
          </Stack>
        )}
        {received.length === 0 && sent.length === 0 && (
          <Text size="sm" c="dimmed">
            No reports yet.
          </Text>
        )}
        {mine.length > 0 && (
          <Button
            variant="default"
            leftSection={<IconMailForward size={16} aria-hidden />}
            disabled={!online}
            onClick={() => {
              setSending(true);
            }}
          >
            Send a report
          </Button>
        )}
      </Stack>
      {sending && (
        <SendReportModal
          campaignId={campaignId}
          from={mine}
          armies={armies}
          onClose={() => {
            setSending(false);
          }}
        />
      )}
    </Section>
  );
}
