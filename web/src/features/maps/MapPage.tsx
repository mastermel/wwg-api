import { Alert, Box, Button, Grid, Group, Stack, Text } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import {
  IconArrowMoveRight,
  IconCloudOff,
  IconHistory,
  IconMap,
  IconMapPin,
  IconMountain,
  IconSettings,
} from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useEffect, useMemo, useRef, useState } from "react";
import { useListArmies } from "@/api/generated/endpoints/armies/armies";
import { useGetCampaign } from "@/api/generated/endpoints/campaigns/campaigns";
import { useGetCampaignGrid, useGetCampaignMap } from "@/api/generated/endpoints/maps/maps";
import {
  useListPositions,
  useListTurns,
  usePlaceUnit,
} from "@/api/generated/endpoints/turns/turns";
import { useListCampaignUnits } from "@/api/generated/endpoints/army-units/army-units";
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
import { ArmiesPanel } from "@/features/maps/ArmiesPanel";
import { CampaignMap } from "@/features/maps/CampaignMap";
import type { Point } from "@/features/maps/geo";
import { hexGrid, hexKey, type Hex } from "@/features/maps/hex-grid";
import { flatRate, pathTo, reach } from "@/features/maps/movement";
import { OrderActions } from "@/features/maps/OrderActions";
import { OrderOverlay, type PendingMove } from "@/features/maps/OrderOverlay";
import { PastTurnPanel } from "@/features/maps/PastTurnPanel";
import { hexes } from "@/features/maps/orders";
import { ReviewPanel } from "@/features/maps/ReviewPanel";
import { SetupPanel } from "@/features/maps/SetupPanel";
import { TurnList } from "@/features/maps/TurnList";
import { HexDetailsList } from "@/features/maps/HexDetailsList";
import { TerrainLayer } from "@/features/maps/TerrainLayer";
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
  const terrainButton = (
    <LinkButton
      variant="default"
      leftSection={<IconMountain size={16} aria-hidden />}
      disabled={!online}
      renderLink={(props) => (
        <Link to="/campaigns/$id/map/terrain" params={{ id: campaignId }} {...props} />
      )}
    >
      Terrain
    </LinkButton>
  );

  return (
    <Page
      title="Map"
      actions={
        manager && (
          <Group gap="xs">
            {map.data?.bounds && terrainButton}
            {settingsButton}
          </Group>
        )
      }
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
  // A past turn chosen from the turn list; null for now (the open turn).
  const [viewing, setViewing] = useState<number | null>(null);
  // The army the Umpire picked out on the map, if any.
  const [highlighted, setHighlighted] = useState<string | null>(null);
  const past = !setup && viewing !== null && viewing !== turns.data?.openTurn ? viewing : null;
  // While setting up, the Umpire works on turn 0's placements; for a past turn, where units were
  // after it; otherwise, where units are now.
  const positionsOf = setup && manager ? { turn: 0 } : past !== null ? { turn: past } : undefined;
  const positions = useListPositions(campaignId, positionsOf, {
    query: { meta: { persist: false }, enabled: turns.data !== undefined },
  });
  const place = usePlaceUnit();
  const [placing, setPlacing] = useState<string | null>(null);
  const mapArea = useRef<HTMLDivElement>(null);
  // On a phone the setup list is under the map: once the banner is drawn, bring it and the map
  // back into view (below the header: the Stack's scroll margin).
  const [moving, setMoving] = useState<PlacedUnit | null>(null);
  const [target, setTarget] = useState<Hex | null>(null);
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
          ? [
              {
                ...known,
                hex: { q: position.q, r: position.r },
                latitude: position.latitude,
                longitude: position.longitude,
              },
            ]
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
  const turnOf = (armyId: string) => openTurns.find((c) => c.army.id === armyId)?.turn;
  const grid = useMemo(() => hexGrid(bounds, settings.hexSize), [bounds, settings.hexSize]);
  const terrain = useGetCampaignGrid(campaignId, live);
  // While moving: the hexes the unit can reach this turn, and (for the Umpire, who can go past
  // that after a warning; decision 0011) anywhere in the grid.
  const withinTurn = useMemo(
    () => (moving ? reach(grid, moving.hex, moving.unit.type) : null),
    [grid, moving],
  );
  const reachable = useMemo(
    () => (moving && manager ? reach(grid, moving.hex, moving.unit.type, Infinity) : withinTurn),
    [grid, moving, manager, withinTurn],
  );
  const targetPath = target && reachable ? pathTo(reachable, target) : null;
  const pastTurn = target !== null && !withinTurn?.has(hexKey(target));
  const lineOf = (from: Hex, path: readonly Hex[]) =>
    [from, ...path].map((hex): [number, number] => {
      const { longitude, latitude } = grid.centre(hex);
      return [longitude, latitude];
    });
  const allMoves: PendingMove[] =
    past !== null
      ? []
      : [
          ...openTurns.flatMap(({ turn }) =>
            turn && turn.status !== "Completed"
              ? turn.orders.flatMap((order) => {
                  const placed = onMap.find((p) => p.unit.id === order.unitId);
                  return placed && order.kind === "Move" && placed.unit.id !== moving?.unit.id
                    ? [
                        {
                          placed,
                          to: order,
                          // A move from before the grid has no path: a straight line.
                          line: lineOf(
                            placed.hex,
                            order.path.length > 0 ? order.path : [{ q: order.q, r: order.r }],
                          ),
                        },
                      ]
                    : [];
                })
              : [],
          ),
          ...(moving && target && targetPath
            ? [{ placed: moving, to: grid.centre(target), line: lineOf(moving.hex, targetPath) }]
            : []),
        ];

  // A picked-out army's moves only; the rest would clutter it.
  const moves = highlighted ? allMoves.filter((m) => m.placed.army.id === highlighted) : allMoves;

  const stopMoving = () => {
    setMoving(null);
    setTarget(null);
  };
  const chooseTarget = (point: Point) => {
    if (!moving) return;
    const hex = grid.hexAt(point);
    const problem = !grid.contains(hex)
      ? "That's outside the campaign's area."
      : hexKey(hex) === hexKey(moving.hex)
        ? `${moving.unit.name} is there already: choose another hex, or Hold.`
        : !reachable?.has(hexKey(hex))
          ? `That's further than ${moving.unit.name} can move in a turn (${hexes(flatRate(moving.unit.type))}).`
          : null;
    if (problem) {
      notifications.show({ color: "red", message: problem });
    } else {
      setTarget(hex);
    }
  };
  const confirmMove = async () => {
    const turn = moving && turnOf(moving.army.id);
    if (!moving || !turn || !targetPath) return;
    const order = { kind: "Move", path: targetPath } as const;
    if (await orders.give(moving.army.id, turn.id, moving.unit, order)) stopMoving();
  };

  const placeAt = async (point: { longitude: number; latitude: number }) => {
    if (!placingUnit) return;
    const hex = grid.hexAt(point);
    if (!grid.contains(hex)) {
      notifications.show({ color: "red", message: "That's outside the campaign's area." });
      return;
    }
    try {
      await place.mutateAsync({ id: placingUnit.unit.id, data: hex });
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
  const view = (number: number) => {
    setViewing(number);
    stopMoving();
    setPlacing(null);
    closeDrawer();
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
          {past !== null && (
            <Alert role="status" color="gray" icon={<IconHistory aria-hidden />}>
              <Group justify="space-between" gap="xs">
                <Text size="sm">
                  {past === 0
                    ? "Showing where the Umpire placed the units."
                    : `Showing where the units were after turn ${String(past)}.`}
                </Text>
                <Button
                  size="compact-sm"
                  variant="default"
                  onClick={() => {
                    setViewing(null);
                  }}
                >
                  Back to now
                </Button>
              </Group>
            </Alert>
          )}
          {moving && (
            <Alert role="status" color="navy" icon={<IconArrowMoveRight aria-hidden />}>
              <Group justify="space-between" gap="xs">
                <Text size="sm">
                  {target && targetPath ? (
                    <>
                      Move <strong>{moving.unit.name}</strong> {hexes(targetPath.length)} to here?
                      {pastTurn && ` That's past its ${hexes(flatRate(moving.unit.type))} a turn.`}
                    </>
                  ) : (
                    <>
                      Tap a shaded hex for where <strong>{moving.unit.name}</strong> moves to.
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
                      {pastTurn ? "Move anyway" : "Confirm"}
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
              {settings.layers.grid && terrain.data && (
                <TerrainLayer grid={grid} terrain={terrain.data} />
              )}
              <OrderOverlay
                moves={moves}
                reachable={
                  withinTurn
                    ? [...withinTurn.values()]
                        .filter(({ from }) => from !== null)
                        .map(({ hex }) => grid.corners(hex))
                    : undefined
                }
              />
              <UnitMarkers
                units={onMap}
                highlight={manager ? highlighted : null}
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
            (past !== null && turns.data.turns[past] ? (
              <PastTurnPanel
                turn={turns.data.turns[past]}
                armies={armies.data ?? []}
                armyTurns={openTurns}
                units={units.data ?? []}
                openTurn={turns.data.openTurn}
                onBack={() => {
                  setViewing(null);
                }}
              />
            ) : setup && manager ? (
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
                units={units.data ?? []}
                review={review}
              />
            ) : !setup && commanded.length > 0 && openTurn ? (
              <TurnPanel
                open={openTurn}
                commanded={commanded}
                units={everyUnit
                  .filter((u) => myArmies.some((a) => a.id === u.army.id))
                  .map((u) => ({ ...u, placed: onMap.find((p) => p.unit.id === u.unit.id) }))}
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
          <HexDetailsList campaignId={campaignId} />
          {manager && (armies.data?.length ?? 0) > 0 && (
            <ArmiesPanel
              armies={armies.data ?? []}
              onMap={onMap.reduce(
                (counts, p) => counts.set(p.army.id, (counts.get(p.army.id) ?? 0) + 1),
                new Map<string, number>(),
              )}
              highlighted={highlighted}
              onHighlight={setHighlighted}
            />
          )}
          {turns.data && !setup && (
            <TurnList
              turns={turns.data}
              viewing={past ?? turns.data.openTurn}
              onView={view}
              manager={manager}
            />
          )}
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
          past !== null
            ? undefined
            : setup && manager
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
                        // The Umpire can change a Submitted turn too (decision 0011).
                        editable={
                          turn.status === "Draft" || (manager && turn.status === "Submitted")
                        }
                        onUndo={() => {
                          void orders.undo(unit.army.id, turn.id, unit.unit).then((undone) => {
                            if (undone) closeDrawer();
                          });
                        }}
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
                              path: null,
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
