import type {
  ArmySide,
  ArmySummary,
  ArmyUnitResponse,
  CampaignConcentrationResponse,
} from "@/api/generated/model";
import { hexKey, hexName, type Hex } from "@/features/maps/hex-grid";
import type { TerrainIndex } from "@/features/maps/terrain";

/**
 * Contact and concentration (step 46, decision 0017): the hexes holding units of both sides, and
 * each side's hexes over a limit, for the Umpire to warn of. The same rules for the open turn
 * (from the orders as given) and past ones (from where the units ended up).
 */

/** A unit in a hex, with its army (for its side). */
export interface UnitInHex {
  unit: ArmyUnitResponse;
  army: ArmySummary;
  hex: Hex;
}

/** A side's counted points in a hex, and whether they're over the hex's limits. */
export interface SideInHex {
  side: ArmySide;
  infantry: number;
  cavalry: number;
  overInfantry: boolean;
  overCavalry: boolean;
}

/** A hex to warn of: contact, a side over a limit, or both. */
export interface HexWarning {
  hex: Hex;
  /** Units of both sides are in it. */
  contact: boolean;
  /** Each side in it, by name. */
  sides: SideInHex[];
  /** Its limits, doubled in a City or a fortress. */
  infantryLimit: number;
  cavalryLimit: number;
  doubled: boolean;
}

/** The hexes to warn of, north to south and west to east. */
export function hexWarnings(
  units: readonly UnitInHex[],
  settings: CampaignConcentrationResponse,
  terrain: TerrainIndex,
): HexWarning[] {
  const byHex = new Map<string, { hex: Hex; sides: Map<string, SideInHex> }>();
  for (const { unit, army, hex } of units) {
    const key = hexKey(hex);
    const entry = byHex.get(key) ?? { hex, sides: new Map<string, SideInHex>() };
    byHex.set(key, entry);
    const side = entry.sides.get(army.side.id) ?? {
      side: army.side,
      infantry: 0,
      cavalry: 0,
      overInfantry: false,
      overCavalry: false,
    };
    entry.sides.set(army.side.id, side);
    if (settings.infantryTypes.includes(unit.type)) side.infantry += unit.points;
    if (settings.cavalryTypes.includes(unit.type)) side.cavalry += unit.points;
  }

  const warnings: HexWarning[] = [];
  for (const { hex, sides } of byHex.values()) {
    const settlement = terrain.cell(hex)?.settlement;
    // A large city or a fortress holds twice as many; a walled town doesn't (decision 0017).
    const doubled = settlement?.size === "City" || settlement?.fortress === true;
    const infantryLimit = settings.infantryLimit * (doubled ? 2 : 1);
    const cavalryLimit = settings.cavalryLimit * (doubled ? 2 : 1);
    const inHex = [...sides.values()]
      .sort((x, y) => x.side.name.localeCompare(y.side.name))
      .map((side) => ({
        ...side,
        overInfantry: side.infantry > infantryLimit,
        overCavalry: side.cavalry > cavalryLimit,
      }));
    const contact = inHex.length > 1;
    if (contact || inHex.some((side) => side.overInfantry || side.overCavalry)) {
      warnings.push({ hex, contact, sides: inHex, infantryLimit, cavalryLimit, doubled });
    }
  }
  return warnings.sort((a, b) => a.hex.r - b.hex.r || a.hex.q - b.hex.q);
}

/** Where the units will be once the orders as given are carried out: each one's order, or still. */
export function afterOrders<T extends UnitInHex>(
  units: readonly T[],
  orders: readonly { unitId: string; q: number; r: number }[],
): T[] {
  return units.map((placed) => {
    const order = orders.find((o) => o.unitId === placed.unit.id);
    return order ? { ...placed, hex: { q: order.q, r: order.r } } : placed;
  });
}

const points = (n: number) => `${String(n)} ${n === 1 ? "point" : "points"}`;

/** A warning's hex in words: "Hex (3, −2), Brussels". */
export function warningPlace(warning: HexWarning, terrain: TerrainIndex) {
  const name = terrain.cell(warning.hex)?.settlement.name;
  return name ? `${hexName(warning.hex)}, ${name}` : hexName(warning.hex);
}

/** A warning in words, a sentence each: contact, then each side over a limit. */
export function describeWarning(warning: HexWarning): string[] {
  const sentences: string[] = [];
  if (warning.contact) {
    sentences.push(`Contact: ${warning.sides.map((s) => s.side.name).join(" and ")}.`);
  }
  const inCity = warning.doubled ? " (doubled here)" : "";
  for (const side of warning.sides) {
    if (side.overInfantry) {
      sentences.push(
        `${side.side.name} has ${points(side.infantry)} of infantry, over ${String(warning.infantryLimit)}${inCity}.`,
      );
    }
    if (side.overCavalry) {
      sentences.push(
        `${side.side.name} has ${points(side.cavalry)} of cavalry, over ${String(warning.cavalryLimit)}${inCity}.`,
      );
    }
  }
  return sentences;
}
