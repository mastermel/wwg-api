import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Badge,
  Box,
  Button,
  Grid,
  Group,
  NumberInput,
  Select,
  Stack,
  Switch,
  Text,
  TextInput,
  useComputedColorScheme,
} from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { IconLock, IconMap } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { Layer, Source } from "react-map-gl/maplibre";
import type { z } from "zod";
import { useGetCampaign } from "@/api/generated/endpoints/campaigns/campaigns";
import { useGetVictorySettings } from "@/api/generated/endpoints/victory/victory";
import {
  getGetCampaignGridQueryKey,
  useGetCampaignGrid,
  useGetCampaignMap,
  useListHexDetails,
  useUpdateHexCell,
  useUpdateHexEdge,
} from "@/api/generated/endpoints/maps/maps";
import type {
  CampaignGridResponse,
  CampaignMapResponse,
  HexDetailResponse,
  MapBounds,
} from "@/api/generated/model";
import { UpdateHexCellBody, UpdateHexEdgeBody } from "@/api/generated/zod/maps/maps.zod";
import { BackLink } from "@/components/BackLink";
import { EmptyState } from "@/components/EmptyState";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { CampaignMap } from "@/features/maps/CampaignMap";
import { hexGrid, hexKey, hexName, type Hex, type HexGrid } from "@/features/maps/hex-grid";
import { mapPalettes } from "@/features/maps/map-style";
import {
  capitalLabels,
  describeSettlement,
  flowFor,
  indexTerrain,
  nearestSide,
  noSettlement,
  roadLabels,
  settlementSizeLabels,
  sideLabels,
  sides,
  storedEdge,
  terrainLabels,
  type Side,
  type TerrainIndex,
} from "@/features/maps/terrain";
import { HexDetailSection } from "@/features/maps/HexDetailSection";
import { HolderField } from "@/features/maps/HolderField";
import { rulesValue, settlementValue } from "@/features/maps/victory";
import { InferTerrainSection } from "@/features/maps/InferTerrainSection";
import { TerrainLayer } from "@/features/maps/TerrainLayer";
import { MapLayersControl } from "@/features/maps/MapLayersControl";
import {
  gameMaxHexesAcross,
  hexesAcross,
  shownRealLayers,
  showsGameLayer,
  useHiddenLayers,
  type GameLayer,
} from "@/features/maps/map-layers";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

// Terrain is live data: nothing of it is saved for offline use.
const live = { query: { meta: { persist: false } } } as const;

const options = <T extends string>(labels: Record<T, string>) =>
  (Object.keys(labels) as T[]).map((value) => ({ value, label: labels[value] }));

/** "Hex (3, −2)": a hex's coordinates, as the Umpire sees them. */

/** The Umpire's terrain editor (decisions 0014 and 0016): a hex's ground and settlement, and its edges. */
export function TerrainPage({ campaignId }: { campaignId: string }) {
  const campaign = useGetCampaign(campaignId);
  const map = useGetCampaignMap(campaignId, live);
  const { user } = useSession();
  const manager = campaign.data !== undefined && canManage(campaign.data, user);

  return (
    <Page
      wide
      title="Terrain"
      summary="Each hex's ground and settlement, and the roads and rivers between them."
      back={
        <BackLink
          renderLink={(props) => (
            <Link to="/campaigns/$id/map" params={{ id: campaignId }} {...props} />
          )}
        >
          Map
        </BackLink>
      }
    >
      <QueryState query={map}>
        {(settings) =>
          campaign.data && !manager ? (
            <Alert
              role="status"
              color="gray"
              icon={<IconLock aria-hidden />}
              title="Only the Umpire can change the terrain"
            >
              The terrain is on the campaign&apos;s Map page.
            </Alert>
          ) : settings.bounds ? (
            <TerrainArea campaignId={campaignId} settings={settings} bounds={settings.bounds} />
          ) : (
            <EmptyState icon={IconMap} title="No map yet">
              Choose the campaign&apos;s area in the map settings first: the grid is laid over it.
            </EmptyState>
          )
        }
      </QueryState>
    </Page>
  );
}

interface TerrainAreaProps {
  campaignId: string;
  settings: CampaignMapResponse;
  bounds: MapBounds;
}

/** The editor, once the campaign's terrain has loaded. */
function TerrainArea({ campaignId, settings, bounds }: TerrainAreaProps) {
  const terrain = useGetCampaignGrid(campaignId, live);
  const details = useListHexDetails(campaignId, live);
  return (
    <QueryState query={terrain}>
      {(loaded) => (
        <TerrainEditor
          campaignId={campaignId}
          settings={settings}
          bounds={bounds}
          terrain={loaded}
          details={details.data ?? []}
        />
      )}
    </QueryState>
  );
}

interface TerrainEditorProps extends TerrainAreaProps {
  terrain: CampaignGridResponse;
  details: HexDetailResponse[];
}

function TerrainEditor({ campaignId, settings, bounds, terrain, details }: TerrainEditorProps) {
  const grid = useMemo(() => hexGrid(bounds, settings.hexSize), [bounds, settings.hexSize]);
  // What the Umpire shows while editing (the Map page's Layers panel, remembered apart).
  const layers = useHiddenLayers(campaignId, "terrain");
  const shownSettings = useMemo(
    () => ({ ...settings, layers: shownRealLayers(settings.layers, layers.hidden) }),
    [settings, layers.hidden],
  );
  const index = useMemo(() => indexTerrain(terrain), [terrain]);
  const [chosen, setChosen] = useState<{ hex: Hex; side: Side } | null>(null);
  const [outside, setOutside] = useState(false);
  // The map opens on the campaign's area: start from that, so a small area's game map is drawn
  // from the first frame (WebKit can miss layers first shown just after the map loads).
  const [zoomedIn, setZoomedIn] = useState(
    () => hexesAcross(bounds, settings.hexSize) <= gameMaxHexesAcross,
  );
  const showsGame = (key: GameLayer) =>
    zoomedIn && showsGameLayer(settings.layers, layers.hidden, key);

  const choose = (point: { longitude: number; latitude: number }) => {
    const hex = grid.hexAt(point);
    setOutside(!grid.contains(hex));
    if (grid.contains(hex)) setChosen({ hex, side: nearestSide(grid, hex, point) });
  };

  return (
    <Grid gap="xl">
      <Grid.Col span={{ base: 12, md: 8, xl: 9 }}>
        <Box h="calc(100dvh - 15rem)" mih={360} pos="relative">
          <Box pos="absolute" top={10} left={10} style={{ zIndex: 2 }}>
            {/* No zoom rule here: the Umpire edits the game map at any zoom. */}
            <MapLayersControl
              campaign={settings.layers}
              layers={layers}
              zoomedIn
              warnings={false}
            />
          </Box>
          <CampaignMap
            settings={shownSettings}
            grid={showsGame("grid")}
            bounds={bounds}
            cursor="crosshair"
            onMapClick={choose}
            onViewChange={(view) => {
              setZoomedIn(hexesAcross(view, settings.hexSize) <= gameMaxHexesAcross);
            }}
          >
            <TerrainLayer
              grid={grid}
              terrain={terrain}
              show={{
                terrain: showsGame("terrain"),
                roads: showsGame("roads"),
                rivers: showsGame("rivers"),
                towns: showsGame("towns"),
                bridges: showsGame("bridges"),
              }}
            />
            {chosen && <ChosenHex grid={grid} hex={chosen.hex} />}
          </CampaignMap>
        </Box>
      </Grid.Col>
      <Grid.Col span={{ base: 12, md: 4, xl: 3 }}>
        <Stack gap="xl">
          {chosen ? (
            <Stack gap="xl">
              <HexForm
                key={hexKey(chosen.hex)}
                campaignId={campaignId}
                hex={chosen.hex}
                index={index}
              />
              <EdgeForm
                // A new form for each edge: its values are that edge's.
                key={`${hexKey(chosen.hex)}:${chosen.side}`}
                campaignId={campaignId}
                hex={chosen.hex}
                side={chosen.side}
                index={index}
                onSide={(side) => {
                  setChosen({ hex: chosen.hex, side });
                }}
              />
              <HexDetailSection
                key={`detail:${hexKey(chosen.hex)}`}
                campaignId={campaignId}
                hex={chosen.hex}
                cell={index.cell(chosen.hex)}
                detail={details.find((d) => d.q === chosen.hex.q && d.r === chosen.hex.r)}
              />
            </Stack>
          ) : (
            <Section title="Choose a hex">
              <Text size="sm">
                {outside
                  ? "That's outside the campaign's grid. Choose a hex inside the area."
                  : "Click a hex on the map to set its terrain. Click near one of its sides to choose that edge."}
              </Text>
            </Section>
          )}
          <InferTerrainSection
            campaignId={campaignId}
            settings={settings}
            bounds={bounds}
            grid={grid}
            terrain={terrain}
          />
        </Stack>
      </Grid.Col>
    </Grid>
  );
}

/** The chosen hex, outlined: the label colour, 9.5:1 or more against the map. */
function ChosenHex({ grid, hex }: { grid: HexGrid; hex: Hex }) {
  const ink = mapPalettes[useComputedColorScheme("light")].label;
  const corners = grid.corners(hex);
  return (
    <Source
      id="chosen-hex"
      type="geojson"
      data={{
        type: "Feature",
        properties: {},
        geometry: { type: "LineString", coordinates: [...corners, corners[0] ?? [0, 0]] },
      }}
    >
      {/* Under the terrain's rivers and roads (layers go on top as they're added), so a river
          along the chosen hex's side still shows. */}
      <Layer
        id="chosen-hex"
        type="line"
        beforeId="terrain-rivers"
        paint={{ "line-color": ink, "line-width": 3 }}
      />
    </Source>
  );
}

type HexValues = z.infer<typeof UpdateHexCellBody>;

function HexForm({
  campaignId,
  hex,
  index,
}: {
  campaignId: string;
  hex: Hex;
  index: TerrainIndex;
}) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const update = useUpdateHexCell();
  const [formError, setFormError] = useState<string | null>(null);
  const cell = index.cell(hex);
  const mode = useGetVictorySettings(campaignId).data?.mode ?? "Rules";
  const form = useForm<HexValues>({
    resolver: zodResolver(UpdateHexCellBody),
    defaultValues: {
      terrain: cell?.terrain ?? "Flat",
      forest: cell?.forest ?? false,
      settlement: {
        ...noSettlement,
        ...cell?.settlement,
        name: cell?.settlement.name ?? "",
        victoryPoints: cell?.settlement.victoryPoints ?? null,
      },
    },
  });
  const { errors } = form.formState;
  const [size, fortress, walled, capital] = useWatch({
    control: form.control,
    name: ["settlement.size", "settlement.fortress", "settlement.walled", "settlement.capital"],
  });
  const place = size !== "None" || fortress;
  // What the rules make it worth, as it's being edited (step 50).
  const rules = rulesValue({ ...noSettlement, size, fortress, walled, capital });
  const { isSubmitting } = form.formState;

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await update.mutateAsync({ id: campaignId, q: hex.q, r: hex.r, data: values });
      notifications.show({ color: "green", message: `Saved ${hexName(hex)}.` });
      await queryClient.invalidateQueries({ queryKey: getGetCampaignGridQueryKey(campaignId) });
    } catch (error) {
      setFormError(applyServerErrors(error, form.setError, ["terrain", "settlement"]));
    }
  });

  return (
    <Section
      title={hexName(hex)}
      description={
        cell
          ? [
              terrainLabels[cell.terrain],
              cell.forest ? "forest" : null,
              describeSettlement(cell.settlement),
            ]
              .filter(Boolean)
              .join(", ")
          : "Flat, with nothing on it."
      }
      actions={
        cell?.setByUmpire && (
          <Badge variant="light" color="gray">
            Set by you
          </Badge>
        )
      }
    >
      <form onSubmit={(event) => void submit(event)} noValidate>
        <Stack gap="sm">
          {formError && (
            <Alert color="red" role="alert">
              {formError}
            </Alert>
          )}
          <Controller
            control={form.control}
            name="terrain"
            render={({ field }) => (
              <Select
                label="Ground"
                data={options(terrainLabels)}
                value={field.value}
                onChange={(value) => {
                  if (value) field.onChange(value);
                }}
                allowDeselect={false}
                comboboxProps={{ withinPortal: false }}
              />
            )}
          />
          <Controller
            control={form.control}
            name="forest"
            render={({ field }) => (
              <Switch
                label="Forest"
                checked={field.value}
                onChange={(event) => {
                  field.onChange(event.currentTarget.checked);
                }}
              />
            )}
          />
          <Controller
            control={form.control}
            name="settlement.size"
            render={({ field }) => (
              <Select
                label="Town or city"
                data={options(settlementSizeLabels)}
                // The API's reason a combination is refused (walls without a town, say).
                error={errors.settlement?.message}
                value={field.value}
                onChange={(value) => {
                  if (!value) return;
                  field.onChange(value);
                  // Walls and capitals belong to a town or city.
                  if (value === "None") {
                    form.setValue("settlement.walled", false);
                    form.setValue("settlement.capital", "None");
                  }
                }}
                allowDeselect={false}
                comboboxProps={{ withinPortal: false }}
              />
            )}
          />
          <Controller
            control={form.control}
            name="settlement.walled"
            render={({ field }) => (
              <Switch
                label="Walled"
                disabled={size === "None"}
                checked={field.value}
                onChange={(event) => {
                  field.onChange(event.currentTarget.checked);
                }}
              />
            )}
          />
          <Controller
            control={form.control}
            name="settlement.fortress"
            render={({ field }) => (
              <Switch
                label="Fortress"
                checked={field.value}
                onChange={(event) => {
                  field.onChange(event.currentTarget.checked);
                }}
              />
            )}
          />
          <Controller
            control={form.control}
            name="settlement.capital"
            render={({ field }) => (
              <Select
                label="Capital"
                data={options(capitalLabels)}
                value={field.value}
                disabled={size === "None"}
                onChange={(value) => {
                  if (value) field.onChange(value);
                }}
                allowDeselect={false}
                comboboxProps={{ withinPortal: false }}
              />
            )}
          />
          <TextInput
            label="Name"
            description="The town, city or fortress's name, shown on the map."
            disabled={size === "None" && !fortress}
            {...form.register("settlement.name", {
              setValueAs: (value: string | null) => value?.trim() ?? "",
            })}
          />
          <Controller
            control={form.control}
            name="settlement.victoryPoints"
            render={({ field }) => (
              <NumberInput
                label="Victory points"
                description={
                  mode === "Chosen"
                    ? "What holding it is worth. Only the settlements you give points count."
                    : `What holding it is worth. Leave it empty for the rules' ${String(rules)}; 0 if it doesn't count.`
                }
                placeholder={mode === "Chosen" ? "0" : String(rules)}
                min={0}
                max={1000}
                allowDecimal={false}
                allowNegative={false}
                clampBehavior="strict"
                // Its step buttons have no accessible names; arrow keys still step.
                hideControls
                disabled={!place}
                value={field.value ?? ""}
                onChange={(value) => {
                  field.onChange(typeof value === "number" ? value : null);
                }}
                onBlur={field.onBlur}
                error={errors.settlement?.victoryPoints?.message}
              />
            )}
          />
          <Group justify="flex-end">
            <Button type="submit" loading={isSubmitting} disabled={!online}>
              Save hex
            </Button>
          </Group>
        </Stack>
      </form>
      {cell && settlementValue(cell.settlement, mode) > 0 && (
        <Stack mt="md">
          <HolderField campaignId={campaignId} hex={hex} />
        </Stack>
      )}
    </Section>
  );
}

type EdgeValues = z.infer<typeof UpdateHexEdgeBody>;

interface EdgeFormProps {
  campaignId: string;
  hex: Hex;
  side: Side;
  index: TerrainIndex;
  onSide: (side: Side) => void;
}

function EdgeForm({ campaignId, hex, side, index, onSide }: EdgeFormProps) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const update = useUpdateHexEdge();
  const [formError, setFormError] = useState<string | null>(null);
  const stored = storedEdge(hex, side);
  const edge = index.edge(stored);
  const form = useForm<EdgeValues>({
    resolver: zodResolver(UpdateHexEdgeBody),
    defaultValues: {
      road: edge?.road ?? "None",
      river: edge?.river ?? false,
      bridge: edge?.bridge ?? false,
      // As this hex sees it: into it or out of it.
      waterway: flowFor(edge?.waterway ?? "None", stored.flipped),
    },
  });
  const river = useWatch({ control: form.control, name: "river" });
  const { isSubmitting } = form.formState;
  const sideName = sideLabels[side].toLowerCase();

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await update.mutateAsync({
        id: campaignId,
        q: stored.q,
        r: stored.r,
        side: stored.side,
        data: { ...values, waterway: flowFor(values.waterway, stored.flipped) },
      });
      notifications.show({ color: "green", message: `Saved the ${sideName} edge.` });
      await queryClient.invalidateQueries({ queryKey: getGetCampaignGridQueryKey(campaignId) });
    } catch (error) {
      setFormError(
        applyServerErrors(error, form.setError, ["road", "river", "bridge", "waterway"]),
      );
    }
  });

  return (
    <Section
      title="Edge"
      description="What crosses this side of the hex, and what lies along it. Only a bridge crosses a river."
      actions={
        edge?.setByUmpire && (
          <Badge variant="light" color="gray">
            Set by you
          </Badge>
        )
      }
    >
      <form onSubmit={(event) => void submit(event)} noValidate>
        <Stack gap="sm">
          {formError && (
            <Alert color="red" role="alert">
              {formError}
            </Alert>
          )}
          <Select
            label="Side"
            data={sides.map((value) => ({ value, label: sideLabels[value] }))}
            value={side}
            onChange={(value) => {
              if (value) onSide(value);
            }}
            allowDeselect={false}
            comboboxProps={{ withinPortal: false }}
          />
          <Controller
            control={form.control}
            name="road"
            render={({ field }) => (
              <Select
                label="Road across it"
                data={options(roadLabels)}
                value={field.value}
                onChange={(value) => {
                  if (value) field.onChange(value);
                }}
                allowDeselect={false}
                comboboxProps={{ withinPortal: false }}
              />
            )}
          />
          <Controller
            control={form.control}
            name="river"
            render={({ field }) => (
              <Switch
                label="River along it"
                checked={field.value}
                onChange={(event) => {
                  field.onChange(event.currentTarget.checked);
                  if (!event.currentTarget.checked) form.setValue("bridge", false);
                }}
              />
            )}
          />
          <Controller
            control={form.control}
            name="bridge"
            render={({ field }) => (
              <Switch
                label="Bridge"
                disabled={!river}
                checked={field.value}
                onChange={(event) => {
                  field.onChange(event.currentTarget.checked);
                }}
              />
            )}
          />
          <Controller
            control={form.control}
            name="waterway"
            render={({ field }) => (
              <Select
                label="Waterway across it"
                description="A navigable river's course, for boats."
                data={[
                  { value: "None", label: "No waterway" },
                  { value: "Out", label: "Flows out of this hex" },
                  { value: "In", label: "Flows into this hex" },
                ]}
                value={field.value}
                onChange={(value) => {
                  if (value) field.onChange(value);
                }}
                allowDeselect={false}
                comboboxProps={{ withinPortal: false }}
              />
            )}
          />
          <Group justify="flex-end">
            <Button type="submit" loading={isSubmitting} disabled={!online}>
              Save edge
            </Button>
          </Group>
        </Stack>
      </form>
    </Section>
  );
}
