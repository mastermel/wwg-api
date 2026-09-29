import { Alert, Box, Button, Grid, Group, Stack, Text } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import {
  IconArrowMoveRight,
  IconCloudOff,
  IconMap,
  IconMapPin,
  IconSettings,
} from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useEffect, useMemo, useRef, useState } from "react";
import { useListArmies } from "@/api/generated/endpoints/armies/armies";
import { useGetCampaign } from "@/api/generated/endpoints/campaigns/campaigns";
import { useGetCampaignMap } from "@/api/generated/endpoints/maps/maps";
import {
  useListPositions,
  useListTurns,
  usePlaceUnit,
} from "@/api/generated/endpoints/turns/turns";
import { useListCampaignUnits } from "@/api/generated/endpoints/units/units";
import type { CampaignMapResponse, MapBounds, MeResponse } from "@/api/generated/model";
import { BackLink } from "@/components/BackLink";
import { EmptyState } from "@/components/EmptyState";
import { LinkButton } from "@/components/LinkButton";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";
import { CampaignMap } from "@/features/maps/CampaignMap";
import { distanceMetres, inBounds, type Point } from "@/features/maps/geo";
import { OrderActions } from "@/features/maps/OrderActions";
import { OrderOverlay, type PendingMove } from "@/features/maps/OrderOverlay";
import { formatDistance } from "@/features/maps/orders";
import { ReviewPanel } from "@/features/maps/ReviewPanel";
import { SetupPanel } from "@/features/maps/SetupPanel";
import { TurnPanel } from "@/features/maps/TurnPanel";
import type { PlacedUnit } from "@/features/maps/stacks";
import { UnitDrawer } from "@/features/maps/UnitDrawer";
import { UnitMarkers } from "@/features/maps/UnitMarkers";
import { useOpenTurns, useOrders } from "@/features/maps/use-orders";
import { useReview } from "@/features/maps/use-review";
import { UnitLegend } from "@/features/units/UnitLegend";
import { errorMessage } from "@/lib/errors";
import { useOnline } from "@/lib/use-online";

// The map is online only: nothing about it is saved for offline use.
const live = { query: { meta: { persist: false } } } as const;

/** The campaign's map, for every member (DESIGN.md §3.13). Online only. */
export function MapPage({ campaignId }: { campaignId: string }) {
  const campaign = useGetCampaign(campaignId);
  const map = useGetCampaignMap(campaignId, live);
  const { user } = useSession();
  const online = useOnline();
  const manager = campaign.data !== undefined && canManage(campaign.data, user);

  const settingsButton = (
    <LinkButton
      variant="default"
      leftSection={<IconSettings size={16} aria-hidden />}
      disabled={!online}
      renderLink={(props) => (
        <Link to="/campaigns/$id/map/settings" params={{ id: campaignId }} {...props} />
      )}
    >
      Map settings
    </LinkButton>
  );

  return (
    <Page
      title="Map"
      actions={manager && settingsButton}
      back={
        <BackLink
          renderLink={(props) => (
            <Link to="/campaigns/$id" params={{ id: campaignId }} {...props} />
          )}
        >
          {campaign.data?.name ?? "The campaign"}
        </BackLink>
      }
    >
      {!online ? (
        <Alert
          role="status"
          color="gray"
          icon={<IconCloudOff aria-hidden />}
          title="The map needs a connection"
        >
          It isn&apos;t saved on this device. It will load when you&apos;re back online.
        </Alert>
      ) : (
        <QueryState query={map}>
          {(settings) =>
            settings.bounds ? (
              <MapWorkspace
                campaignId={campaignId}
                settings={settings}
                bounds={settings.bounds}
                manager={manager}
                user={user}
              />
            ) : (
              <EmptyState icon={IconMap} title="No map yet" action={manager && settingsButton}>
                {manager
                  ? "Choose the campaign's area in the map settings."
                  : "The Umpire hasn't chosen the campaign's area yet."}
              </EmptyState>
            )
          }
        </QueryState>
      )}
    </Page>
  );
}

interface MapWorkspaceProps {
  campaignId: string;
  settings: CampaignMapResponse;
  bounds: MapBounds;
  manager: boolean;
  user: MeResponse | null;
}

/** The map with its units, and beside it what the viewer can do now. */
function MapWorkspace({ campaignId, settings, bounds, manager, user }: MapWorkspaceProps) {
  const queryClient = useQueryClient();
  const turns = useListTurns(campaignId, live);
  const units = useListCampaignUnits(campaignId, live);
  const armies = useListArmies(campaignId, live);
  const setup = turns.data?.stage === "Setup";
  // While setting up, the Umpire works on turn 0's placements; otherwise, where units are now.
  const positions = useListPositions(campaignId, setup && manager ? { turn: 0 } : undefined, {
    query: { meta: { persist: false }, enabled: turns.data !== undefined },
  });
  const place = usePlaceUnit();
  const [placing, setPlacing] = useState<string | null>(null);
  const mapArea = useRef<HTMLDivElement>(null);
  // On a phone the setup list is under the map: once the banner is drawn, bring it and the map
  // back into view (below the header: the Stack's scroll margin).
  const [moving, setMoving] = useState<PlacedUnit | null>(null);
  const [target, setTarget] = useState<Point | null>(null);
  useEffect(() => {
    if (placing || moving) mapArea.current?.scrollIntoView({ block: "start" });
  }, [placing, moving]);
  const [chosen, setChosen] = useState<PlacedUnit[]>([]);
  const [selected, setSelected] = useState<PlacedUnit | null>(null);

  const everyUnit = useMemo(
    () =>
      (units.data ?? []).flatMap((unit) => {
        const army = armies.data?.find((a) => a.id === unit.armyId);
        return army ? [{ unit, army }] : [];
      }),
    [units.data, armies.data],
  );
  const onMap = useMemo<PlacedUnit[]>(
    () =>
      (positions.data ?? []).flatMap((position) => {
        const known = everyUnit.find((u) => u.unit.id === position.unitId);
        return known
          ? [{ ...known, latitude: position.latitude, longitude: position.longitude }]
          : [];
      }),
    [positions.data, everyUnit],
  );
  const placingUnit = everyUnit.find((u) => u.unit.id === placing);

  // Once running, the open turn of every army the viewer can see: the Umpire's, all of them; a
  // commander's, their own.
  const myArmies = useMemo(
    () => (armies.data ?? []).filter((a) => user && a.commander?.userId === user.id),
    [armies.data, user],
  );
  const openTurns = useOpenTurns(
    turns.data?.stage === "Running" ? (manager ? (armies.data ?? []) : myArmies) : [],
  );
  const commanded = manager ? [] : openTurns;
  const orders = useOrders(campaignId);
  const review = useReview(campaignId);
  const openTurn = turns.data?.turns.find((t) => t.closedAt === null);
  const turnOf = (armyId: string) => commanded.find((c) => c.army.id === armyId)?.turn;
  const limitOf = (placed: PlacedUnit) =>
    settings.movementLimits.find((l) => l.unitType === placed.unit.type)?.metres ?? 0;
  const moves: PendingMove[] = [
    ...openTurns.flatMap(({ turn }) =>
      turn && turn.status !== "Completed"
        ? turn.orders.flatMap((order) => {
            const placed = onMap.find((p) => p.unit.id === order.unitId);
            return placed && order.kind === "Move" && placed.unit.id !== moving?.unit.id
              ? [{ placed, to: order }]
              : [];
          })
        : [],
    ),
    ...(moving && target ? [{ placed: moving, to: target }] : []),
  ];

  const stopMoving = () => {
    setMoving(null);
    setTarget(null);
  };
  const chooseTarget = (point: Point) => {
    if (!moving) return;
    const limit = limitOf(moving);
    const problem = !inBounds(point, bounds)
      ? "That's outside the campaign's area."
      : // A metre's grace, as the API allows, for rounding.
        distanceMetres(moving, point) > limit + 1
        ? `That's further than ${moving.unit.name} can move in a turn (${formatDistance(limit, settings.distanceUnit)}).`
        : null;
    if (problem) {
      notifications.show({ color: "red", message: problem });
    } else {
      setTarget(point);
    }
  };
  const confirmMove = async () => {
    const turn = moving && turnOf(moving.army.id);
    if (!moving || !turn || !target) return;
    const order = { kind: "Move", ...target } as const;
    if (await orders.give(moving.army.id, turn.id, moving.unit, order)) stopMoving();
  };

  const placeAt = async (point: { longitude: number; latitude: number }) => {
    if (!placingUnit) return;
    try {
      await place.mutateAsync({ id: placingUnit.unit.id, data: point });
      notifications.show({ color: "green", message: `Placed ${placingUnit.unit.name}.` });
      setPlacing(null);
      await refreshCampaign(queryClient, campaignId);
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, `${placingUnit.unit.name} couldn't be placed. Try again.`),
      });
    }
  };

  const closeDrawer = () => {
    setChosen([]);
    setSelected(null);
  };

  return (
    <Grid gap="xl">
      <Grid.Col span={{ base: 12, md: 8 }}>
        <Stack
          gap="sm"
          ref={mapArea}
          style={{ scrollMarginTop: "calc(var(--app-shell-header-height, 60px) + 0.5rem)" }}
        >
          {placingUnit && (
            <Alert role="status" color="navy" icon={<IconMapPin aria-hidden />}>
              <Group justify="space-between" gap="xs">
                <Text size="sm">
                  Click the map where <strong>{placingUnit.unit.name}</strong> goes.
                </Text>
                <Button
                  size="compact-sm"
                  variant="default"
                  onClick={() => {
                    setPlacing(null);
                  }}
                >
                  Cancel
                </Button>
              </Group>
            </Alert>
          )}
          {moving && (
            <Alert role="status" color="navy" icon={<IconArrowMoveRight aria-hidden />}>
              <Group justify="space-between" gap="xs">
                <Text size="sm">
                  {target ? (
                    <>
                      Move <strong>{moving.unit.name}</strong>{" "}
                      {formatDistance(distanceMetres(moving, target), settings.distanceUnit)} to
                      here?
                    </>
                  ) : (
                    <>
                      Tap the map inside the circle where <strong>{moving.unit.name}</strong> moves
                      to.
                    </>
                  )}
                </Text>
                <Group gap="xs">
                  {target && (
                    <Button
                      size="compact-sm"
                      loading={orders.busy}
                      onClick={() => void confirmMove()}
                    >
                      Confirm
                    </Button>
                  )}
                  <Button size="compact-sm" variant="default" onClick={stopMoving}>
                    Cancel
                  </Button>
                </Group>
              </Group>
            </Alert>
          )}
          {/* The map takes the rest of the screen, and at least enough to be useful. */}
          <Box h="calc(100dvh - 15rem)" mih={360}>
            <CampaignMap
              settings={settings}
              bounds={bounds}
              cursor={placingUnit || moving ? "crosshair" : undefined}
              onMapClick={
                placingUnit ? (point) => void placeAt(point) : moving ? chooseTarget : undefined
              }
            >
              <OrderOverlay
                moves={moves}
                range={moving ? { centre: moving, metres: limitOf(moving) } : undefined}
              />
              <UnitMarkers
                units={onMap}
                onSelect={(stack) => {
                  // While placing, choosing a unit (or stack) puts the new one there too.
                  if (placingUnit) {
                    void placeAt({ longitude: stack.longitude, latitude: stack.latitude });
                    return;
                  }
                  // While moving, choosing a unit (or stack) moves there.
                  if (moving) {
                    chooseTarget({ longitude: stack.longitude, latitude: stack.latitude });
                    return;
                  }
                  setChosen(stack.units);
                  setSelected(null);
                }}
              />
            </CampaignMap>
          </Box>
        </Stack>
      </Grid.Col>
      <Grid.Col span={{ base: 12, md: 4 }}>
        <Stack gap="xl">
          {turns.data &&
            (setup && manager ? (
              <SetupPanel
                campaignId={campaignId}
                turns={turns.data}
                units={everyUnit.map((u) => ({
                  ...u,
                  placed: onMap.some((p) => p.unit.id === u.unit.id),
                }))}
                placing={placing}
                onPlace={setPlacing}
              />
            ) : !setup && manager && openTurn ? (
              <ReviewPanel
                open={openTurn}
                problems={turns.data.startProblems}
                armyTurns={openTurns}
                review={review}
              />
            ) : !setup && commanded.length > 0 && openTurn ? (
              <TurnPanel
                open={openTurn}
                commanded={commanded}
                units={everyUnit
                  .filter((u) => myArmies.some((a) => a.id === u.army.id))
                  .map((u) => ({ ...u, placed: onMap.find((p) => p.unit.id === u.unit.id) }))}
                distanceUnit={settings.distanceUnit}
                orders={orders}
                onChoose={(placed) => {
                  setChosen([placed]);
                  setSelected(null);
                }}
              />
            ) : (
              <Section title={setup ? "Setting up" : `Turn ${String(turns.data.openTurn)}`}>
                <Text size="sm">
                  {setup
                    ? "The Umpire is placing the armies. Your units appear here once the campaign starts."
                    : `${String(turns.data.turns.at(-1)?.submitted ?? 0)} of ${String(turns.data.turns.at(-1)?.armies ?? 0)} armies have submitted this turn.`}
                </Text>
              </Section>
            ))}
          <Section title="Legend">
            <UnitLegend />
          </Section>
        </Stack>
      </Grid.Col>
      <UnitDrawer
        units={chosen}
        selected={selected}
        onSelect={setSelected}
        onClose={closeDrawer}
        actions={
          setup && manager
            ? (unit) => (
                <Button
                  variant="light"
                  leftSection={<IconMapPin size={16} aria-hidden />}
                  onClick={() => {
                    setPlacing(unit.unit.id);
                    closeDrawer();
                  }}
                >
                  Move on the map
                </Button>
              )
            : (unit) => {
                const turn = turnOf(unit.army.id);
                return (
                  turn?.open && (
                    <OrderActions
                      placed={unit}
                      turn={turn}
                      distanceUnit={settings.distanceUnit}
                      busy={orders.busy}
                      onMove={() => {
                        setMoving(unit);
                        setTarget(null);
                        closeDrawer();
                      }}
                      onHold={() => {
                        void orders
                          .give(unit.army.id, turn.id, unit.unit, {
                            kind: "Hold",
                            latitude: null,
                            longitude: null,
                          })
                          .then((saved) => {
                            if (saved) closeDrawer();
                          });
                      }}
                    />
                  )
                );
              }
        }
      />
    </Grid>
  );
}
