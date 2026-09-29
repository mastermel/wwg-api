import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Box,
  Button,
  Grid,
  Group,
  NumberInput,
  SegmentedControl,
  Select,
  Stack,
  Switch,
  Text,
  useComputedColorScheme,
} from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { IconCrop, IconLock } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useRef, useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { Layer, Source, type MapRef } from "react-map-gl/maplibre";
import { z } from "zod";
import { useGetCampaign } from "@/api/generated/endpoints/campaigns/campaigns";
import {
  getGetCampaignMapQueryKey,
  useGetCampaignMap,
  useUpdateCampaignMap,
} from "@/api/generated/endpoints/maps/maps";
import type { CampaignMapResponse, MapBounds, MapLayers, PlaceResult } from "@/api/generated/model";
import { UpdateCampaignMapBody } from "@/api/generated/zod/maps/maps.zod";
import { BackLink } from "@/components/BackLink";
import { LinkButton } from "@/components/LinkButton";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { CampaignMap } from "@/features/maps/CampaignMap";
import { PlaceSearch } from "@/features/maps/PlaceSearch";
import {
  distanceUnitLabels,
  labelLanguages,
  maxDistance,
  toMetres,
  toUnit,
} from "@/features/maps/map-units";
import { unitTypeLabels } from "@/features/units/unit-types";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

/** Where the Umpire starts choosing, before there's an area: Europe. */
const europe: MapBounds = { west: -10, south: 36, east: 30, north: 60 };

// Movement limits are entered in the campaign's unit, and saved in metres.
const SettingsForm = UpdateCampaignMapBody.omit({ movementLimits: true }).extend({
  limits: z.array(
    z.object({
      unitType: UpdateCampaignMapBody.shape.movementLimits.element.shape.unitType,
      distance: z.number({ error: "Enter a distance." }).min(0),
    }),
  ),
});

type SettingsValues = z.infer<typeof SettingsForm>;

/** The form path of a movement limit's distance. */
const limitPath = (index: number) =>
  `limits.${String(index)}.distance` as `limits.${number}.distance`;

const layerSwitches: { key: keyof MapLayers; label: string }[] = [
  { key: "roads", label: "Main roads" },
  { key: "places", label: "Towns and cities" },
  { key: "water", label: "Rivers and water" },
  { key: "forests", label: "Forests" },
  { key: "hills", label: "Hills" },
  { key: "contours", label: "Contour lines" },
];

/** The campaign's map settings (Umpire, Admin; DESIGN.md §3.13). */
export function MapSettingsPage({ campaignId }: { campaignId: string }) {
  const campaign = useGetCampaign(campaignId);
  const map = useGetCampaignMap(campaignId, { query: { meta: { persist: false } } });
  const { user } = useSession();
  const manager = campaign.data !== undefined && canManage(campaign.data, user);

  return (
    <Page
      title="Map settings"
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
              title="Only the Umpire can change the map"
            >
              The map is on the campaign&apos;s Map page.
            </Alert>
          ) : (
            <SettingsFormView campaignId={campaignId} settings={settings} />
          )
        }
      </QueryState>
    </Page>
  );
}

function SettingsFormView({
  campaignId,
  settings,
}: {
  campaignId: string;
  settings: CampaignMapResponse;
}) {
  const online = useOnline();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const update = useUpdateCampaignMap();
  const mapRef = useRef<MapRef>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<SettingsValues>({
    resolver: zodResolver(SettingsForm),
    defaultValues: {
      bounds: settings.bounds,
      labelLanguage: settings.labelLanguage,
      distanceUnit: settings.distanceUnit,
      layers: settings.layers,
      limits: settings.movementLimits.map((limit) => ({
        unitType: limit.unitType,
        distance: toUnit(limit.metres, settings.distanceUnit),
      })),
    },
  });
  const { errors, isSubmitting } = form.formState;
  const [bounds, layers, labelLanguage, distanceUnit, limits] = useWatch({
    control: form.control,
    name: ["bounds", "layers", "labelLanguage", "distanceUnit", "limits"],
  });
  const unit = distanceUnitLabels[distanceUnit].short;

  const useThisView = () => {
    const view = mapRef.current?.getBounds();
    if (!view) return;
    form.setValue(
      "bounds",
      {
        west: view.getWest(),
        south: view.getSouth(),
        east: view.getEast(),
        north: view.getNorth(),
      },
      { shouldDirty: true },
    );
  };

  const flyTo = (place: PlaceResult) => {
    const target = mapRef.current;
    if (!target) return;
    if (place.bounds) {
      const b = place.bounds;
      target.fitBounds([b.west, b.south, b.east, b.north], { padding: 40 });
    } else {
      target.flyTo({ center: [place.longitude, place.latitude], zoom: 10 });
    }
  };

  // Changing the unit keeps the distances: 20 km becomes 12.4 mi.
  const changeUnit = (next: SettingsValues["distanceUnit"]) => {
    form.setValue(
      "limits",
      limits.map((limit) => ({
        ...limit,
        distance: toUnit(toMetres(limit.distance, distanceUnit), next),
      })),
    );
    form.setValue("distanceUnit", next, { shouldDirty: true });
  };

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      const saved = await update.mutateAsync({
        id: campaignId,
        data: {
          bounds: values.bounds,
          labelLanguage: values.labelLanguage,
          distanceUnit: values.distanceUnit,
          layers: values.layers,
          movementLimits: values.limits.map((limit) => ({
            unitType: limit.unitType,
            metres: toMetres(limit.distance, values.distanceUnit),
          })),
        },
      });
      queryClient.setQueryData(getGetCampaignMapQueryKey(campaignId), saved);
      notifications.show({ color: "green", message: "Saved the map settings." });
      await navigate({ to: "/campaigns/$id/map", params: { id: campaignId } });
    } catch (error) {
      setFormError(applyServerErrors(error, form.setError, ["bounds", "labelLanguage"]));
    }
  });

  // The preview follows the form, so switching a layer or the language shows at once.
  const preview: CampaignMapResponse = { ...settings, layers, labelLanguage, distanceUnit };

  return (
    <form onSubmit={(event) => void submit(event)} noValidate>
      <Stack gap="xl">
        {formError && (
          <Alert color="red" role="alert">
            {formError}
          </Alert>
        )}
        <Grid gap="xl">
          <Grid.Col span={{ base: 12, md: 8 }}>
            <Section
              title="Area"
              description="Everyone's map stays inside it. Frame it on the map, then use that view."
            >
              <Stack>
                <PlaceSearch campaignId={campaignId} onChoose={flyTo} />
                <Box h={420}>
                  <CampaignMap
                    settings={preview}
                    bounds={settings.bounds ?? europe}
                    free
                    mapRef={mapRef}
                  >
                    {bounds && <AreaOutline bounds={bounds} />}
                  </CampaignMap>
                </Box>
                <Group justify="space-between">
                  <Text size="sm" c="dimmed">
                    {bounds ? "The outline is the campaign's area." : "No area chosen yet."}
                  </Text>
                  <Button
                    variant="default"
                    leftSection={<IconCrop size={16} aria-hidden />}
                    onClick={useThisView}
                  >
                    Use this view
                  </Button>
                </Group>
              </Stack>
            </Section>
          </Grid.Col>
          <Grid.Col span={{ base: 12, md: 4 }}>
            <Stack gap="xl">
              <Section title="What the map shows">
                <Stack gap="sm">
                  {layerSwitches.map(({ key, label }) => (
                    <Controller
                      key={key}
                      control={form.control}
                      name={`layers.${key}`}
                      render={({ field }) => (
                        <Switch
                          label={label}
                          checked={field.value}
                          onChange={(event) => {
                            field.onChange(event.currentTarget.checked);
                          }}
                        />
                      )}
                    />
                  ))}
                  <Controller
                    control={form.control}
                    name="labelLanguage"
                    render={({ field }) => (
                      <Select
                        label="Place names in"
                        description="Where a place has no name in it, its own."
                        data={labelLanguages}
                        value={field.value}
                        onChange={(value) => {
                          if (value) field.onChange(value);
                        }}
                        allowDeselect={false}
                        error={errors.labelLanguage?.message}
                        comboboxProps={{ withinPortal: false }}
                      />
                    )}
                  />
                </Stack>
              </Section>
              <Section
                title="Movement per turn"
                description="How far each type of unit can move in one turn, in a straight line."
              >
                <Stack gap="sm">
                  <SegmentedControl
                    aria-label="Distances in"
                    data={[
                      { value: "Kilometres", label: distanceUnitLabels.Kilometres.long },
                      { value: "Miles", label: distanceUnitLabels.Miles.long },
                    ]}
                    value={distanceUnit}
                    onChange={(value) => {
                      changeUnit(value);
                    }}
                  />
                  {limits.map((limit, index) => (
                    <Controller
                      key={limit.unitType}
                      control={form.control}
                      name={limitPath(index)}
                      render={({ field }) => (
                        <NumberInput
                          label={unitTypeLabels[limit.unitType]}
                          suffix={` ${unit}`}
                          min={0}
                          max={maxDistance(distanceUnit)}
                          decimalScale={1}
                          value={field.value}
                          onChange={(value) => {
                            field.onChange(typeof value === "number" ? value : undefined);
                          }}
                          error={errors.limits?.[index]?.distance?.message}
                        />
                      )}
                    />
                  ))}
                </Stack>
              </Section>
            </Stack>
          </Grid.Col>
        </Grid>
        <Group justify="flex-end">
          <LinkButton
            variant="default"
            renderLink={(props) => (
              <Link to="/campaigns/$id/map" params={{ id: campaignId }} {...props} />
            )}
          >
            Cancel
          </LinkButton>
          <Button type="submit" loading={isSubmitting} disabled={!online}>
            Save map settings
          </Button>
        </Group>
      </Stack>
    </form>
  );
}

/** The chosen area, outlined on the map: 6:1 or more against its land and water, either scheme. */
function AreaOutline({ bounds: b }: { bounds: MapBounds }) {
  const scheme = useComputedColorScheme("light");
  return (
    <Source
      id="area"
      type="geojson"
      data={{
        type: "Feature",
        properties: {},
        geometry: {
          type: "Polygon",
          coordinates: [
            [
              [b.west, b.south],
              [b.east, b.south],
              [b.east, b.north],
              [b.west, b.north],
              [b.west, b.south],
            ],
          ],
        },
      }}
    >
      <Layer
        id="area-outline"
        type="line"
        paint={{
          "line-color": scheme === "dark" ? "#9db8ff" : "#1c3f94",
          "line-width": 3,
          "line-dasharray": [2, 1],
        }}
      />
    </Source>
  );
}
