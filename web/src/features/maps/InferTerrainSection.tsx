import { Alert, Button, Group, Progress, Stack, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconWand } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import {
  getGetCampaignGridQueryKey,
  useSaveCampaignGrid,
} from "@/api/generated/endpoints/maps/maps";
import type { CampaignGridResponse, CampaignMapResponse, MapBounds } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { Section } from "@/components/Section";
import { hexCount, maxDrawnHexes, type HexGrid } from "@/features/maps/hex-grid";
import { inferTerrain } from "@/features/maps/inference/infer";
import { buildSources } from "@/features/maps/inference/sources";
import { loadTiles } from "@/features/maps/inference/tiles";
import { errorMessage } from "@/lib/errors";
import { useOnline } from "@/lib/use-online";

interface InferTerrainSectionProps {
  campaignId: string;
  settings: CampaignMapResponse;
  bounds: MapBounds;
  grid: HexGrid;
  terrain: CampaignGridResponse;
}

type Step = { label: string; done: number; total: number } | null;

const plural = (count: number, one: string, many: string) =>
  `${count.toLocaleString()} ${count === 1 ? one : many}`;

/**
 * Infers the whole grid's terrain from the map's data, in the browser (decision 0014: the map's
 * tiles, as it draws them), and saves it. What the Umpire set stays; the rest is replaced.
 */
export function InferTerrainSection({
  campaignId,
  settings,
  bounds,
  grid,
  terrain,
}: InferTerrainSectionProps) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const save = useSaveCampaignGrid();
  const [step, setStep] = useState<Step>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [confirming, confirm] = useDisclosure(false);
  const inferred = [...terrain.cells, ...terrain.edges].some((item) => !item.setByUmpire);
  const tooMany = hexCount(bounds, settings.hexSize) > maxDrawnHexes;

  const run = async () => {
    confirm.close();
    setFailure(null);
    try {
      setStep({ label: "Loading the map's data", done: 0, total: 1 });
      const { vectorTiles, elevationTiles } = await loadTiles(
        bounds,
        settings.hexSize,
        (done, total) => {
          setStep({ label: "Loading the map's data", done, total });
        },
      );
      const sources = buildSources(vectorTiles, elevationTiles, settings.labelLanguage);
      const result = await inferTerrain(grid, settings.hexSize, sources, (done, total) => {
        setStep({ label: "Working out the hexes", done, total });
      });
      setStep({ label: "Saving", done: 0, total: 1 });
      await save.mutateAsync({ id: campaignId, data: result });
      await queryClient.invalidateQueries({ queryKey: getGetCampaignGridQueryKey(campaignId) });
      notifications.show({
        color: "green",
        message: `Inferred the terrain: ${plural(result.cells.length, "hex", "hexes")} and ${plural(result.edges.length, "edge", "edges")} with something on them.`,
      });
    } catch (error) {
      setFailure(errorMessage(error, "The terrain couldn't be inferred. Try again."));
    } finally {
      setStep(null);
    }
  };

  return (
    <Section
      title="Infer terrain"
      description="Reads the map's heights, forests, water, towns, roads and rivers into every hex. Hexes and edges you've set yourself stay as they are."
    >
      <Stack gap="sm">
        {failure && (
          <Alert color="red" role="alert">
            {failure}
          </Alert>
        )}
        {step && (
          <div role="status">
            <Text size="sm">
              {step.total > 1
                ? `${step.label}: ${step.done.toLocaleString()} of ${step.total.toLocaleString()}…`
                : `${step.label}…`}
            </Text>
            <Progress
              value={step.total > 0 ? (100 * step.done) / step.total : 0}
              aria-label={step.label}
              mt={4}
            />
          </div>
        )}
        {tooMany && (
          <Text size="sm" c="dimmed">
            The grid has too many hexes to infer: choose larger hexes or a smaller area.
          </Text>
        )}
        <Group>
          <Button
            variant="light"
            leftSection={<IconWand size={16} aria-hidden />}
            onClick={inferred ? confirm.open : () => void run()}
            loading={step !== null}
            disabled={!online || tooMany}
          >
            {inferred ? "Infer again" : "Infer terrain"}
          </Button>
        </Group>
      </Stack>
      <ConfirmModal
        opened={confirming}
        onClose={confirm.close}
        title="Infer the terrain again?"
        confirmLabel="Infer again"
        color="navy"
        onConfirm={() => void run()}
      >
        What was inferred before is replaced. Hexes and edges you&apos;ve set yourself stay as they
        are.
      </ConfirmModal>
    </Section>
  );
}
