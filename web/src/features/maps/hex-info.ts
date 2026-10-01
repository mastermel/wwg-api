import type {
  ArmySummary,
  DepotResponse,
  HexDetailResponse,
  SettlementScoreResponse,
  VictoryPointsMode,
} from "@/api/generated/model";
import { describeDetail } from "@/features/maps/hex-detail";
import { settlementValue, victoryPoints } from "@/features/maps/victory";
import { hexKey, hexName, type Hex } from "@/features/maps/hex-grid";
import {
  describeSettlement,
  flowFor,
  sideLabels,
  sides,
  storedEdge,
  terrainLabels,
  type Side,
  type TerrainIndex,
} from "@/features/maps/terrain";

/** What the viewer knows of a hex, for the map's hex card: a title and a line per fact. */
export interface HexInfo {
  title: string;
  /** Its ground and forest, in a few words: the hover label. */
  summary: string;
  lines: string[];
}

const list = (items: readonly string[]) =>
  items.length < 2
    ? (items[0] ?? "")
    : `${items.slice(0, -1).join(", ")} and ${items.at(-1) ?? ""}`;

const toward = (found: readonly Side[]) => list(found.map((s) => sideLabels[s].toLowerCase()));

/**
 * Everything the viewer knows of a hex (the Map page's hex card): its ground and forest, its town,
 * city or fortress, the roads, rivers, bridges and waterways on its six sides, the actual terrain
 * if the viewer has been shown it (decision 0016), and the depots in it they may see.
 */
export function describeHex(
  hex: Hex,
  terrain: TerrainIndex,
  details: readonly HexDetailResponse[],
  depots: readonly DepotResponse[],
  armies: readonly ArmySummary[],
  /** The settlements whose holders the viewer may know (step 50). */
  holdings: readonly SettlementScoreResponse[] = [],
  /** Which settlements are worth victory points. */
  mode: VictoryPointsMode = "Rules",
): HexInfo {
  const cell = terrain.cell(hex);
  const ground = terrainLabels[cell?.terrain ?? "Flat"];
  const summary = cell?.forest ? `${ground}, forest` : ground;
  const lines: string[] = [`${summary}.`];

  const settlement = cell && describeSettlement(cell.settlement);
  if (settlement) lines.push(`${settlement}.`);
  const worth = cell ? settlementValue(cell.settlement, mode) : 0;
  if (worth > 0) {
    const holding = holdings.find((h) => hexKey(h) === hexKey(hex));
    const holder = holding?.armyId && armies.find((a) => a.id === holding.armyId)?.name;
    lines.push(
      `Worth ${victoryPoints(worth)}${holder ? `; held by ${holder}` : holding ? "; held by no one" : ""}.`,
    );
  }

  const edges = sides.map((side) => {
    const at = storedEdge(hex, side);
    return { side, flipped: at.flipped, edge: terrain.edge(at) };
  });
  const good = edges.filter((e) => e.edge?.road === "Good").map((e) => e.side);
  const poor = edges.filter((e) => e.edge?.road === "Poor").map((e) => e.side);
  if (good.length > 0) lines.push(`Good road to the ${toward(good)}.`);
  if (poor.length > 0) lines.push(`Poor road to the ${toward(poor)}.`);
  const rivers = edges.filter((e) => e.edge?.river);
  if (rivers.length > 0) {
    const bridged = rivers.filter((e) => e.edge?.bridge).map((e) => e.side);
    lines.push(
      `River along the ${toward(rivers.map((e) => e.side))} ${rivers.length === 1 ? "side" : "sides"}` +
        (bridged.length > 0 ? `, bridged to the ${toward(bridged)}.` : "."),
    );
  }
  const waterways = edges.filter((e) => e.edge && e.edge.waterway !== "None");
  for (const { side, flipped, edge } of waterways) {
    const flow = edge ? flowFor(edge.waterway, flipped) : "None";
    lines.push(
      `Waterway to the ${sideLabels[side].toLowerCase()}${flow === "Out" ? ", downstream" : flow === "In" ? ", upstream" : ""}.`,
    );
  }

  const detail = details.find((d) => hexKey(d) === hexKey(hex));
  if (detail) lines.push(`Actual terrain: ${describeDetail(detail)}`);

  for (const depot of depots.filter((d) => hexKey(d) === hexKey(hex))) {
    const army = armies.find((a) => a.id === depot.armyId)?.name ?? "an army";
    const kind = depot.kind === "Main" ? "main depot" : "intermediate depot";
    lines.push(
      `${depot.name ? `${depot.name}: ` : ""}${army}'s ${kind}.`.replace(/^./, (c) =>
        c.toUpperCase(),
      ),
    );
  }

  const name = cell?.settlement.name;
  return { title: name ? `${hexName(hex)}, ${name}` : hexName(hex), summary, lines };
}
