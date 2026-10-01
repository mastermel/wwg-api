import { ActionIcon, Alert, Box, Button, Grid, Group, Stack, Switch, Text } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import {
  IconArrowMoveRight,
  IconCloudOff,
  IconHistory,
  IconMap,
  IconMapPin,
  IconMaximize,
  IconMinimize,
  IconMountain,
  IconSettings,
} from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useMediaQuery } from "@mantine/hooks";
import { useEffect, useMemo, useRef, useState } from "react";
import { useListArmies } from "@/api/generated/endpoints/armies/armies";
import {
  useGetCampaign,
  useGetCampaignCalendar,
  useGetCampaignConcentration,
} from "@/api/generated/endpoints/campaigns/campaigns";
import {
  useGetCampaignGrid,
  useGetCampaignMap,
  useListHexDetails,
} from "@/api/generated/endpoints/maps/maps";
import { useGetMovementTable } from "@/api/generated/endpoints/turns/turns";
import {
  useListMarches,
  useListPositions,
  useListTurns,
  usePlaceUnit,
} from "@/api/generated/endpoints/turns/turns";
import { useListCampaignUnits } from "@/api/generated/endpoints/army-units/army-units";
import {
  useListCouriers,
  useListReports,
} from "@/api/generated/endpoints/intelligence/intelligence";
import { useListSightings } from "@/api/generated/endpoints/sightings/sightings";
import { useGetScoreboard, useGetVictorySettings } from "@/api/generated/endpoints/victory/victory";
import {
  useGetSupply,
  useGetSupplySettings,
  useListDepots,
} from "@/api/generated/endpoints/supply/supply";
import type {
  CampaignMapResponse,
  DepotResponse,
  MapBounds,
  MeResponse,
} from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
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
import { DepotFormModal, type DepotDraft } from "@/features/maps/DepotFormModal";
import { DepotMarkers } from "@/features/maps/DepotMarkers";
import { DepotsPanel } from "@/features/maps/DepotsPanel";
import { describeSupply } from "@/features/maps/supply";
import { SupplyWarnings } from "@/features/maps/SupplyWarnings";
import { depotName, useDepots } from "@/features/maps/use-depots";
import { CampaignMap } from "@/features/maps/CampaignMap";
import { afterOrders, depotThreats, hexWarnings } from "@/features/maps/contact";
import type { Point } from "@/features/maps/geo";
import { hexGrid, hexKey, type Hex } from "@/features/maps/hex-grid";
import {
  budgetFor,
  classOf,
  pathTo,
  ratesOf,
  reach,
  shareOfTheWay,
  stepCost,
} from "@/features/maps/movement";
import { indexTerrain } from "@/features/maps/terrain";
import { canForceMarch, forceMarchBonus, moveCost } from "@/features/maps/marches";
import { OrderActions } from "@/features/maps/OrderActions";
import { OrderOverlay, type PendingMove } from "@/features/maps/OrderOverlay";
import { PastTurnPanel } from "@/features/maps/PastTurnPanel";
import { hexes } from "@/features/maps/orders";
import { ReviewPanel } from "@/features/maps/ReviewPanel";
import { SetupPanel } from "@/features/maps/SetupPanel";
import { TurnList } from "@/features/maps/TurnList";
import { HexDetailsList } from "@/features/maps/HexDetailsList";
import { MapLegend } from "@/features/maps/MapLegend";
import classes from "@/features/maps/MapPage.module.css";
import { describeHex } from "@/features/maps/hex-info";
import { useFullScreen } from "@/features/maps/use-full-screen";
import { HexInfoPopup } from "@/features/maps/HexInfoPopup";
import { SelectedHexLayer } from "@/features/maps/SelectedHexLayer";
import { CouriersPanel } from "@/features/maps/CouriersPanel";
import { HoldingFlags } from "@/features/maps/HoldingFlags";
import { ScoreboardPanel } from "@/features/maps/ScoreboardPanel";
import { IntelligencePanel } from "@/features/maps/IntelligencePanel";
import { SightingMarkers } from "@/features/maps/SightingMarkers";
import { SnapshotMarkers } from "@/features/maps/SnapshotMarkers";
import { sightingsFor } from "@/features/maps/sightings";
import { SightingsPanel } from "@/features/maps/SightingsPanel";
import { HexWarningsLayer } from "@/features/maps/HexWarningsLayer";
import {
  gameMaxHexesAcross,
  hexesAcross,
  shownRealLayers,
  useHiddenLayers,
  type GameLayer,
} from "@/features/maps/map-layers";
import { MapLayersControl } from "@/features/maps/MapLayersControl";
import { TerrainLayer } from "@/features/maps/TerrainLayer";
import { TurnPanel } from "@/features/maps/TurnPanel";
import type { PlacedUnit } from "@/features/maps/stacks";
import { UnitDrawer } from "@/features/maps/UnitDrawer";
import { UnitMarkers } from "@/features/maps/UnitMarkers";
import { useOpenTurns, useOrders } from "@/features/maps/use-orders";
import { useReview } from "@/features/maps/use-review";
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
      wide
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
  // The move being chosen is a force march (step 47): a flat hex's worth further, by day.
  const [forceMarch, setForceMarch] = useState(false);
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
                headingInto:
                  position.progress !== null && position.path.length > 0
                    ? { hex: position.path.at(-1) ?? position, progress: position.progress }
                    : undefined,
                livesOffTheLand: position.livesOffTheLand,
              },
            ]
          : [];
      }),
    [positions.data, everyUnit],
  );
  const placingUnit = everyUnit.find((u) => u.unit.id === placing);
  // Depots (step 48a): those the viewer may see, and the Umpire placing, moving or changing one.
  const depots = useListDepots(campaignId, live);
  const supplySettings = useGetSupplySettings(campaignId, live);
  // Supply (step 48): the viewer's armies' (the Umpire's, every army's), once running.
  const supply = useGetSupply(campaignId, {
    query: { meta: { persist: false }, enabled: turns.data?.stage === "Running" },
  });
  // Sightings (step 49b): the viewer's armies' (the Umpire's, every army's), every turn's.
  const sightings = useListSightings(campaignId, {
    query: { meta: { persist: false }, enabled: turns.data?.stage === "Running" },
  });
  const viewingTurn = past ?? turns.data?.openTurn ?? 0;
  const drawnSightings = useMemo(
    // The Umpire sees the units themselves; a commander, what was seen of them.
    () => (manager ? [] : sightingsFor(sightings.data ?? [], viewingTurn)),
    [manager, sightings.data, viewingTurn],
  );
  const sightedTurns = useMemo(
    () => new Set((sightings.data ?? []).map((s) => s.turn)),
    [sightings.data],
  );
  // Intelligence between allies (step 49d): the viewer's reports, and the Umpire's couriers.
  const running = turns.data?.stage === "Running";
  const reports = useListReports(campaignId, {
    query: { meta: { persist: false }, enabled: running },
  });
  const couriers = useListCouriers(campaignId, {
    query: { meta: { persist: false }, enabled: running && manager },
  });
  const [shownReport, setShownReport] = useState<string | null>(null);
  // Victory points (step 50): the totals, and the holders the viewer may know.
  const scoreboard = useGetScoreboard(campaignId, live);
  const victorySettings = useGetVictorySettings(campaignId, live);
  const outOfSupply = useMemo(
    () =>
      new Set(
        (supply.data?.units ?? []).filter((s) => s.state === "Unsupplied").map((s) => s.unitId),
      ),
    [supply.data],
  );
  const depotChanges = useDepots(campaignId);
  const [placingDepot, setPlacingDepot] = useState<{
    draft: DepotDraft;
    depot?: DepotResponse;
  } | null>(null);
  const [depotForm, setDepotForm] = useState<DepotResponse | "new" | null>(null);
  const [removingDepot, setRemovingDepot] = useState<DepotResponse | null>(null);

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
  // What the viewer shows on the map (its Layers panel): of what the campaign's settings show,
  // the real map's layers as they choose; the game map's too, once close enough to read it.
  const layers = useHiddenLayers(campaignId);
  // The map opens on the campaign's area: start from that, so a small area's game map is drawn
  // from the first frame (WebKit can miss layers first shown just after the map loads).
  const [zoomedIn, setZoomedIn] = useState(
    () => hexesAcross(bounds, settings.hexSize) <= gameMaxHexesAcross,
  );
  const shownSettings = useMemo(
    () => ({ ...settings, layers: shownRealLayers(settings.layers, layers.hidden) }),
    [settings, layers.hidden],
  );
  const showGame = (key: GameLayer) =>
    settings.layers.grid && zoomedIn && !layers.hidden.game.includes(key);
  const terrain = useGetCampaignGrid(campaignId, live);
  const movementTable = useGetMovementTable(campaignId, live);
  const calendar = useGetCampaignCalendar(campaignId, live);
  const concentration = useGetCampaignConcentration(campaignId, {
    query: { meta: { persist: false }, enabled: manager },
  });
  const costs = useMemo(
    () => ({ rates: ratesOf(movementTable.data), terrain: indexTerrain(terrain.data) }),
    [movementTable.data, terrain.data],
  );
  // While moving: the hexes the unit can reach this turn by the terrain and the campaign's
  // table, and (for the Umpire, who can go past that after a warning, closed steps too; decision
  // 0011) anywhere in the grid.
  const withinTurn = useMemo(
    () =>
      moving
        ? reach(grid, moving.hex, moving.unit.type, {
            ...costs,
            carried: moving.headingInto,
            // Further or less by the time of day, for some nations' infantry (step 45), and
            // further by force march (step 47).
            budget:
              budgetFor(
                costs.rates,
                moving.unit.type,
                moving.unit.nation,
                openTurn?.part,
                calendar.data,
              ) + (forceMarch ? forceMarchBonus(costs.rates, moving.unit.type) : 0),
          })
        : null,
    [grid, moving, costs, openTurn?.part, calendar.data, forceMarch],
  );
  // The moving unit's forced marches, for what the move costs: its commander's and the Umpire's.
  const marches = useListMarches(moving?.army.id ?? "", {
    query: { meta: { persist: false }, enabled: moving !== null },
  });
  const movingCost = moving
    ? moveCost(
        marches.data?.find((m) => m.unitId === moving.unit.id),
        forceMarch,
      )
    : null;
  const reachable = useMemo(
    () =>
      moving && manager
        ? reach(grid, moving.hex, moving.unit.type, { ...costs, budget: Infinity })
        : withinTurn,
    [grid, moving, manager, withinTurn, costs],
  );
  // Contact and concentration, for the Umpire (step 46): in the open turn from the orders as
  // given, on a past one from where the units ended up.
  // Where the open turn's orders as given leave the units (the Umpire's warnings and sightings).
  const afterTheOrders = useMemo(
    () =>
      afterOrders(
        onMap,
        openTurns.flatMap(({ turn }) => turn?.orders ?? []),
      ),
    [onMap, openTurns],
  );
  const warnings = useMemo(
    () =>
      manager && !setup && concentration.data
        ? hexWarnings(past !== null ? onMap : afterTheOrders, concentration.data, costs.terrain)
        : [],
    [manager, setup, concentration.data, past, onMap, afterTheOrders, costs.terrain],
  );
  // Depots with the other side's units in their hex: the Umpire captures or destroys them.
  const threats = useMemo(
    () =>
      manager && !setup
        ? depotThreats(
            past !== null
              ? onMap
              : afterOrders(
                  onMap,
                  openTurns.flatMap(({ turn }) => turn?.orders ?? []),
                ),
            depots.data ?? [],
            armies.data ?? [],
          )
        : [],
    [manager, setup, past, onMap, openTurns, depots.data, armies.data],
  );
  // A hex's card (what the viewer knows of it): pinned by a click or tap on the map, and on a
  // screen with a mouse, a label for the hex it's over. Only while not placing or moving.
  const hexDetails = useListHexDetails(campaignId, live);
  const canHover = useMediaQuery("(hover: hover) and (pointer: fine)");
  const [pinned, setPinned] = useState<Hex | null>(null);
  const [hovered, setHovered] = useState<Hex | null>(null);
  const idle = !placingUnit && !placingDepot && !moving;
  const shownHex = idle ? (pinned ?? hovered) : null;
  const hexInfo = useMemo(
    () =>
      shownHex
        ? describeHex(
            shownHex,
            costs.terrain,
            hexDetails.data ?? [],
            depots.data ?? [],
            armies.data ?? [],
            scoreboard.data?.settlements,
            victorySettings.data?.mode,
          )
        : null,
    [
      shownHex,
      costs.terrain,
      hexDetails.data,
      depots.data,
      armies.data,
      scoreboard.data,
      victorySettings.data,
    ],
  );
  const hoverAt = (point: Point | null) => {
    const hex = point && idle ? grid.hexAt(point) : null;
    const inside = hex && grid.contains(hex) ? hex : null;
    setHovered((was) => (was && inside && hexKey(was) === hexKey(inside) ? was : inside));
  };
  const targetPath = target && reachable ? pathTo(reachable, target) : null;
  // Part of the way into it, when it takes more than a turn (the commander's moves only).
  const targetProgress = target && !manager ? withinTurn?.get(hexKey(target))?.progress : undefined;
  // Why the Umpire's move breaks the rules, if it crosses a closed step: the first one's reason.
  const closedStep =
    moving && targetPath
      ? targetPath
          .map((hex, i) =>
            stepCost(
              costs.rates,
              costs.terrain,
              classOf(moving.unit.type),
              i === 0 ? moving.hex : (targetPath[i - 1] ?? hex),
              hex,
            ),
          )
          .find((step) => step.closedBecause !== null)?.closedBecause
      : undefined;
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
    setForceMarch(false);
  };
  const chooseTarget = (point: Point) => {
    if (!moving) return;
    const hex = grid.hexAt(point);
    const problem = !grid.contains(hex)
      ? "That's outside the campaign's area."
      : hexKey(hex) === hexKey(moving.hex)
        ? `${moving.unit.name} is there already: choose another hex, or Hold.`
        : !reachable?.has(hexKey(hex))
          ? `${moving.unit.name} can't get there this turn: it's too far, or the way is closed.`
          : null;
    if (problem) {
      notifications.show({ color: "red", message: problem });
    } else {
      setTarget(hex);
    }
  };
  // Whether a unit lives off the land this turn (step 48b): as its order says, or as it did.
  const livingOffTheLand = (placed: PlacedUnit) =>
    turnOf(placed.army.id)?.orders.find((o) => o.unitId === placed.unit.id)?.livesOffTheLand ??
    placed.livesOffTheLand ??
    false;
  const confirmMove = async () => {
    const turn = moving && turnOf(moving.army.id);
    if (!moving || !turn || !targetPath) return;
    const order = {
      kind: "Move",
      path: targetPath,
      forceMarch,
      livesOffTheLand: livingOffTheLand(moving),
    } as const;
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

  const placeDepot = async (point: Point) => {
    if (!placingDepot) return;
    const hex = grid.hexAt(point);
    if (!grid.contains(hex)) {
      notifications.show({ color: "red", message: "That's outside the campaign's area." });
      return;
    }
    const { draft, depot } = placingDepot;
    const data = { kind: draft.kind, name: draft.name || null, q: hex.q, r: hex.r };
    const saved = depot
      ? await depotChanges.update(depot, data)
      : await depotChanges.create(draft.armyId, data);
    if (saved) setPlacingDepot(null);
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

  const desktop = useMediaQuery("(min-width: 62em)");
  const fullScreen = useFullScreen();
  // Full screen is a computer's: a phone's map is most of the screen already.
  const full = desktop && fullScreen.full;
  // The map, with what's being placed or moved above it; on a computer, it can fill the screen.
  const mapColumn = (
    <Stack
      gap="sm"
      ref={mapArea}
      className={full ? classes.full : undefined}
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
      {placingDepot && (
        <Alert role="status" color="navy" icon={<IconMapPin aria-hidden />}>
          <Group justify="space-between" gap="xs">
            <Text size="sm">
              Click the map where <strong>{placingDepot.draft.name || "the depot"}</strong> goes.
            </Text>
            <Button
              size="compact-sm"
              variant="default"
              onClick={() => {
                setPlacingDepot(null);
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
                  {targetProgress === undefined ? (
                    <>
                      Move <strong>{moving.unit.name}</strong> {hexes(targetPath.length)} to here?
                    </>
                  ) : (
                    <>
                      Move <strong>{moving.unit.name}</strong>{" "}
                      {targetPath.length > 1 ? `${hexes(targetPath.length - 1)}, and ` : ""}
                      {shareOfTheWay(targetProgress)} of the way into here? It takes more than a
                      turn: it gets there next turn, going on.
                    </>
                  )}
                  {pastTurn &&
                    (closedStep
                      ? ` It can't go that way (${closedStep}), but you may take it there.`
                      : " That's further than it can go in a turn.")}
                </>
              ) : (
                <>
                  Tap a shaded hex for where <strong>{moving.unit.name}</strong> moves to.
                </>
              )}
              {movingCost && <> {movingCost}</>}
            </Text>
            {canForceMarch(costs.rates, moving.unit.type, openTurn?.part) && (
              <Switch
                size="sm"
                label="Force march (a hex further)"
                checked={forceMarch}
                onChange={(event) => {
                  setForceMarch(event.currentTarget.checked);
                  // The shaded hexes change: choose again.
                  setTarget(null);
                }}
              />
            )}
            <Group gap="xs">
              {target && (
                <Button size="compact-sm" loading={orders.busy} onClick={() => void confirmMove()}>
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
      <Box className={classes.mapBox}>
        <Group gap="xs" pos="absolute" top={10} left={10} style={{ zIndex: 2 }}>
          <MapLayersControl
            campaign={settings.layers}
            layers={layers}
            zoomedIn={zoomedIn}
            warnings={manager}
          />
          {desktop &&
            (full ? (
              <Button
                variant="default"
                leftSection={<IconMinimize size={18} aria-hidden />}
                title="Or press Esc"
                onClick={fullScreen.exit}
              >
                Exit full screen
              </Button>
            ) : (
              <ActionIcon
                variant="default"
                size="lg"
                aria-label="Full screen"
                onClick={fullScreen.enter}
              >
                <IconMaximize size={18} aria-hidden />
              </ActionIcon>
            ))}
        </Group>
        <CampaignMap
          settings={shownSettings}
          grid={showGame("grid")}
          onHover={canHover ? hoverAt : undefined}
          onViewChange={(view) => {
            setZoomedIn(hexesAcross(view, settings.hexSize) <= gameMaxHexesAcross);
          }}
          bounds={bounds}
          cursor={placingUnit || placingDepot || moving ? "crosshair" : undefined}
          onMapClick={
            placingUnit
              ? (point) => void placeAt(point)
              : placingDepot
                ? (point) => void placeDepot(point)
                : moving
                  ? chooseTarget
                  : (point) => {
                      const hex = grid.hexAt(point);
                      setPinned(grid.contains(hex) ? hex : null);
                    }
          }
        >
          {/* Mounted from the start and hidden by zoom: added later, WebKit can miss them. */}
          {settings.layers.grid && terrain.data && (
            <TerrainLayer
              grid={grid}
              terrain={terrain.data}
              show={{
                terrain: showGame("terrain"),
                roads: showGame("roads"),
                rivers: showGame("rivers"),
                towns: showGame("towns"),
                bridges: showGame("bridges"),
              }}
            />
          )}
          <HexWarningsLayer grid={grid} warnings={warnings} visible={showGame("warnings")} />
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
          <SelectedHexLayer grid={grid} hex={idle ? pinned : null} />
          <DepotMarkers depots={depots.data ?? []} armies={armies.data ?? []} />
          {showGame("towns") && (
            <HoldingFlags
              settlements={scoreboard.data?.settlements ?? []}
              armies={armies.data ?? []}
            />
          )}
          <SightingMarkers sightings={drawnSightings} armies={armies.data ?? []} />
          <SnapshotMarkers
            report={reports.data?.find((r) => r.id === shownReport)}
            armies={armies.data ?? []}
          />
          {shownHex && hexInfo && (
            <HexInfoPopup
              at={grid.centre(shownHex)}
              info={hexInfo}
              pinned={pinned !== null}
              onClose={() => {
                setPinned(null);
              }}
            />
          )}
          <UnitMarkers
            units={onMap}
            outOfSupply={past === null ? outOfSupply : undefined}
            highlight={manager ? highlighted : null}
            onSelect={(stack) => {
              // While placing, choosing a unit (or stack) puts the new one there too.
              if (placingUnit) {
                void placeAt({ longitude: stack.longitude, latitude: stack.latitude });
                return;
              }
              // While placing a depot, choosing a unit (or stack) puts it in that hex.
              if (placingDepot) {
                void placeDepot({ longitude: stack.longitude, latitude: stack.latitude });
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
  );
  // What the viewer does this turn: the Umpire's review, a commander's orders, a past turn.
  const turnPanel =
    turns.data &&
    (past !== null && turns.data.turns[past] ? (
      <PastTurnPanel
        turn={turns.data.turns[past]}
        armies={armies.data ?? []}
        armyTurns={openTurns}
        units={units.data ?? []}
        openTurn={turns.data.openTurn}
        warnings={warnings}
        threats={threats}
        terrain={costs.terrain}
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
        campaignId={campaignId}
        places={afterTheOrders}
        open={openTurn}
        problems={turns.data.startProblems}
        armyTurns={openTurns}
        units={units.data ?? []}
        review={review}
        warnings={warnings}
        threats={threats}
        terrain={costs.terrain}
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
    ));
  const otherPanels = (
    <>
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
      {turns.data?.stage === "Running" && past === null && (
        <SupplyWarnings
          supply={supply.data}
          units={units.data ?? []}
          depots={depots.data ?? []}
          armyIds={(manager ? (armies.data ?? []) : myArmies).map((a) => a.id)}
        />
      )}
      <DepotsPanel
        depots={depots.data ?? []}
        armies={armies.data ?? []}
        manager={manager}
        busy={depotChanges.busy}
        onAdd={() => {
          setDepotForm("new");
        }}
        onMove={(depot) => {
          setPlacingDepot({
            draft: { armyId: depot.armyId, kind: depot.kind, name: depot.name ?? "" },
            depot,
          });
          mapArea.current?.scrollIntoView({ block: "start" });
        }}
        onEdit={setDepotForm}
        onRemove={setRemovingDepot}
      />
      {running && !manager && myArmies.length > 0 && (
        <IntelligencePanel
          campaignId={campaignId}
          reports={reports.data ?? []}
          mine={myArmies}
          armies={armies.data ?? []}
          shown={shownReport}
          onShow={setShownReport}
        />
      )}
      {manager && (
        <CouriersPanel
          campaignId={campaignId}
          couriers={couriers.data ?? []}
          armies={armies.data ?? []}
        />
      )}
      <ScoreboardPanel scoreboard={scoreboard.data} armies={armies.data ?? []} />
      <SightingsPanel
        sightings={sightings.data ?? []}
        armies={armies.data ?? []}
        turn={viewingTurn}
        manager={manager}
      />
      {turns.data && !setup && (
        <TurnList
          sighted={sightedTurns}
          turns={turns.data}
          viewing={past ?? turns.data.openTurn}
          onView={view}
          manager={manager}
        />
      )}
    </>
  );
  const legend = (
    <Section title="Legend">
      <MapLegend umpire={manager} collapsible={desktop} />
    </Section>
  );
  const overlays = (
    <>
      {depotForm && (
        <DepotFormModal
          title={depotForm === "new" ? "Add a depot" : `Edit ${depotName(depotForm)}`}
          submitLabel={depotForm === "new" ? "Place it on the map" : "Save"}
          armies={armies.data ?? []}
          chooseArmy={depotForm === "new"}
          initial={
            depotForm === "new"
              ? { armyId: armies.data?.[0]?.id ?? "", kind: "Main", name: "" }
              : { armyId: depotForm.armyId, kind: depotForm.kind, name: depotForm.name ?? "" }
          }
          onSubmit={(draft) => {
            if (depotForm === "new") {
              setDepotForm(null);
              setPlacingDepot({ draft });
              mapArea.current?.scrollIntoView({ block: "start" });
              return;
            }
            const data = {
              kind: draft.kind,
              name: draft.name || null,
              q: depotForm.q,
              r: depotForm.r,
            };
            void depotChanges.update(depotForm, data).then((saved) => {
              if (saved) setDepotForm(null);
            });
          }}
          onClose={() => {
            setDepotForm(null);
          }}
        />
      )}
      <ConfirmModal
        opened={removingDepot !== null}
        onClose={() => {
          setRemovingDepot(null);
        }}
        title={`Remove ${removingDepot ? depotName(removingDepot) : "the depot"}?`}
        confirmLabel="Remove depot"
        loading={depotChanges.busy}
        onConfirm={() => {
          if (!removingDepot) return;
          void depotChanges.remove(removingDepot).then((removed) => {
            if (removed) setRemovingDepot(null);
          });
        }}
      >
        It was captured, destroyed or given up: its army&apos;s units no longer draw supply from it.
      </ConfirmModal>
      <UnitDrawer
        units={chosen}
        selected={selected}
        onSelect={setSelected}
        onClose={closeDrawer}
        showsMarches={(unit) => manager || myArmies.some((a) => a.id === unit.army.id)}
        supplyOf={(unit) => {
          const found = supply.data?.units.find((s) => s.unitId === unit.unit.id);
          return found && past === null ? describeSupply(found, depots.data ?? []) : undefined;
        }}
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
                        offTheLand={
                          supplySettings.data?.offTheLandNations.includes(unit.unit.nation)
                            ? {
                                on: livingOffTheLand(unit),
                                onChange: (on) => {
                                  // The order stays as it is, with or without living off the land.
                                  const given = turn.orders.find((o) => o.unitId === unit.unit.id);
                                  void orders.give(
                                    unit.army.id,
                                    turn.id,
                                    unit.unit,
                                    given?.kind === "Move"
                                      ? {
                                          kind: "Move",
                                          path: given.path,
                                          forceMarch: given.forceMarch,
                                          livesOffTheLand: on,
                                        }
                                      : { kind: "Hold", path: null, livesOffTheLand: on },
                                    on
                                      ? `${unit.unit.name} will live off the land.`
                                      : `${unit.unit.name} will draw on its supply.`,
                                  );
                                },
                              }
                            : undefined
                        }
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
                              livesOffTheLand: livingOffTheLand(unit),
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
    </>
  );

  // On a computer: the map with the turn panel and legend beside it, and the rest in columns
  // beneath. On a phone or tablet: one column, the map first and the legend last.
  return desktop ? (
    <>
      <Stack gap="xl">
        <Group align="flex-start" wrap="nowrap" gap="xl">
          <Box style={{ flex: 1, minWidth: 0 }}>{mapColumn}</Box>
          <Stack gap="xl" className={classes.side}>
            {turnPanel}
            {legend}
          </Stack>
        </Group>
        <div className={classes.panels}>{otherPanels}</div>
      </Stack>
      {overlays}
    </>
  ) : (
    <Grid gap="xl">
      <Grid.Col span={12}>{mapColumn}</Grid.Col>
      <Grid.Col span={12}>
        <Stack gap="xl">
          {turnPanel}
          {otherPanels}
          {legend}
        </Stack>
      </Grid.Col>
      {overlays}
    </Grid>
  );
}
