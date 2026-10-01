import { Accordion, Group, SimpleGrid, Stack, Text, useComputedColorScheme } from "@mantine/core";
import { IconBuildingWarehouse, IconFlagFilled } from "@tabler/icons-react";
import { createContext, useContext, type ReactNode } from "react";
import depotClasses from "@/features/maps/DepotMarkers.module.css";
import flagClasses from "@/features/maps/HoldingFlags.module.css";
import { mapPalettes } from "@/features/maps/map-style";
import { UnitLegend } from "@/features/units/UnitLegend";

type Palette = (typeof mapPalettes)[keyof typeof mapPalettes];

// A flat-topped hex, 28 wide and 24 high, as the grid draws them.
const hexPoints = "7,1 21,1 27,12 21,23 7,23 1,12";

/** A sample on a patch of land, as on the map. Decorative: its label says what it is. */
function Sample({ p, children }: { p: Palette; children: ReactNode }) {
  return (
    <svg width={32} height={24} viewBox="0 0 32 24" aria-hidden style={{ flexShrink: 0 }}>
      <rect width={32} height={24} rx={3} fill={p.land} />
      {children}
    </svg>
  );
}

function Item({ sample, label }: { sample: ReactNode; label: string }) {
  return (
    <Group component="li" gap="xs" wrap="nowrap" style={{ listStyle: "none" }}>
      {sample}
      <Text size="sm">{label}</Text>
    </Group>
  );
}

// Whether the legend's groups open and close (one at a time): on a computer, beside the map.
const Collapsible = createContext(false);

function LegendGroup({
  title,
  children,
  list = true,
}: {
  title: string;
  children: ReactNode;
  /** Its children are list items (the default), or a list of their own. */
  list?: boolean;
}) {
  const collapsible = useContext(Collapsible);
  const content = list ? (
    <SimpleGrid
      cols={{ base: 2, sm: 1 }}
      spacing={6}
      verticalSpacing={6}
      component="ul"
      p={0}
      m={0}
      aria-label={title}
    >
      {children}
    </SimpleGrid>
  ) : (
    children
  );
  return collapsible ? (
    <Accordion.Item value={title}>
      <Accordion.Control>
        <Text size="sm" fw={600}>
          {title}
        </Text>
      </Accordion.Control>
      <Accordion.Panel>{content}</Accordion.Panel>
    </Accordion.Item>
  ) : (
    <Stack gap={6}>
      <Text size="sm" fw={600}>
        {title}
      </Text>
      {content}
    </Stack>
  );
}

/** A hex tinted as the terrain layer tints it, over land, with the grid's outline. */
const hex = (p: Palette, fill: string, opacity: number) => (
  <Sample p={p}>
    <polygon
      points={hexPoints}
      transform="translate(2 0)"
      fill={fill}
      fillOpacity={opacity}
      stroke={p.label}
      strokeOpacity={0.35}
    />
  </Sample>
);

/** A line across the sample, as the terrain layer draws roads, rivers and waterways. */
const line = (p: Palette, color: string, width: number, dash?: string) => (
  <Sample p={p}>
    <line
      x1={3}
      y1={12}
      x2={29}
      y2={12}
      stroke={color}
      strokeWidth={width}
      strokeDasharray={dash}
      strokeLinecap={dash ? "butt" : "round"}
    />
  </Sample>
);

/** A place's circle, as the terrain layer draws towns, cities and fortresses. */
const place = (p: Palette, radius: number, fill: string, stroke: string, strokeWidth: number) => (
  <Sample p={p}>
    <circle cx={16} cy={12} r={radius} fill={fill} stroke={stroke} strokeWidth={strokeWidth} />
  </Sample>
);

/**
 * The map's legend (DESIGN.md §3.13): the unit symbols, and the game map's terrain, roads, rivers,
 * places and markers, drawn in the current scheme's map colours.
 */
export function MapLegend({
  umpire,
  collapsible = false,
}: {
  umpire: boolean;
  /** Each group opens and closes, one at a time (beside the map, on a computer). */
  collapsible?: boolean;
}) {
  const p = mapPalettes[useComputedColorScheme("light")];
  const groups = (
    <>
      <LegendGroup title="Units" list={false}>
        <UnitLegend />
      </LegendGroup>
      <LegendGroup title="Terrain">
        <Item sample={hex(p, p.land, 0)} label="Flat" />
        <Item sample={hex(p, p.lowHill, 0.45)} label="Low hills" />
        <Item sample={hex(p, p.highHill, 0.45)} label="High hills" />
        <Item sample={hex(p, p.mountain, 0.45)} label="Mountains" />
        <Item sample={hex(p, p.waterHex, 0.45)} label="Water (a lake)" />
        <Item sample={hex(p, p.forestHex, 0.35)} label="Forest" />
      </LegendGroup>
      <LegendGroup title="Roads, rivers and bridges">
        <Item sample={line(p, p.road, 3)} label="Good road" />
        <Item sample={line(p, p.road, 2, "6 4")} label="Poor road" />
        <Item sample={line(p, p.river, 4)} label="River (along a hexside)" />
        <Item sample={line(p, p.river, 2, "2 2")} label="Waterway (navigable)" />
        <Item
          sample={
            <Sample p={p}>
              <line x1={16} y1={2} x2={16} y2={22} stroke={p.river} strokeWidth={4} />
              <circle cx={16} cy={12} r={4} fill={p.road} stroke={p.label} strokeWidth={1} />
            </Sample>
          }
          label="Bridge"
        />
      </LegendGroup>
      <LegendGroup title="Towns and cities">
        <Item sample={place(p, 5, p.label, p.halo, 1)} label="Town" />
        <Item sample={place(p, 7, p.label, p.halo, 1)} label="City" />
        <Item sample={place(p, 5, p.label, p.halo, 3)} label="Walled town" />
        <Item sample={place(p, 5, p.label, p.fortress, 3)} label="With a fortress" />
        <Item sample={place(p, 5, p.fortress, p.fortress, 3)} label="Fortress" />
      </LegendGroup>
      <LegendGroup title="Markers">
        <Item
          sample={
            <span className={depotClasses.depot} style={{ borderColor: "#5c6370" }}>
              <IconBuildingWarehouse size={16} aria-hidden />
            </span>
          }
          label="Depot (its army's colour)"
        />
        <Item
          sample={
            <span
              className={depotClasses.depot}
              data-kind="Intermediate"
              style={{ borderColor: "#5c6370" }}
            >
              <IconBuildingWarehouse size={16} aria-hidden />
            </span>
          }
          label="Intermediate depot"
        />
        <Item
          sample={
            <svg width={32} height={24} viewBox="0 0 32 24" aria-hidden>
              <circle cx={16} cy={12} r={7} fill="#a3261f" stroke="#ffffff" strokeWidth={2} />
              <text x={16} y={16} textAnchor="middle" fontSize={11} fontWeight={700} fill="#ffffff">
                !
              </text>
            </svg>
          }
          label="Out of supply"
        />
        <Item
          sample={
            <span className={flagClasses.flag} style={{ display: "inline-flex" }}>
              <IconFlagFilled size={14} color="#5c6370" aria-hidden />
            </span>
          }
          label="Held by (its army's colour)"
        />
        <Item
          sample={
            <Sample p={p}>
              <polygon
                points={hexPoints}
                transform="translate(2 0)"
                fill={p.label}
                fillOpacity={0.12}
                stroke={p.label}
                strokeOpacity={0.5}
              />
            </Sample>
          }
          label="Where a unit can move"
        />
        {umpire && (
          <Item
            sample={
              <Sample p={p}>
                <polygon
                  points={hexPoints}
                  transform="translate(2 0)"
                  fill={p.warning}
                  fillOpacity={0.15}
                  stroke={p.warning}
                  strokeWidth={2}
                />
              </Sample>
            }
            label="Contact or concentration"
          />
        )}
      </LegendGroup>
    </>
  );
  return (
    <Collapsible.Provider value={collapsible}>
      {collapsible ? (
        <Accordion defaultValue="Units" variant="filled" chevronPosition="right">
          {groups}
        </Accordion>
      ) : (
        <Stack gap="md">{groups}</Stack>
      )}
    </Collapsible.Provider>
  );
}
