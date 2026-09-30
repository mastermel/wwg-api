import type { ReactNode } from "react";
import type { UnitType } from "@/api/generated/model";

/**
 * A unit's symbol, in the style of NATO's (APP-6): a frame in its army's colour, with a glyph for
 * the arm. Drawn by the app, since the standard has nothing for several of the rule book's types
 * (decision 0014): infantry is a cross, cavalry a slash, artillery a dot, engineers the
 * standard's bridge, a supply train a bar across the foot, siege artillery two dots. Letters
 * mark the rest: L light infantry, G partisans (guerrillas), S scouts and M medium cavalry (on
 * the slash); an oval (armour) is heavy cavalry, and a slash (mounted) with the dot horse
 * artillery. A black frame and a white halo keep it clear on any map, light or dark.
 */

const W = 36;
const H = 24;
const ink = "#111111";

const cross = <path d={`M0,0 L${String(W)},${String(H)} M${String(W)},0 L0,${String(H)}`} />;
const slash = <path d={`M0,${String(H)} L${String(W)},0`} />;
const dot = <circle cx={W / 2} cy={H / 2} r={4.5} fill={ink} stroke="none" />;
const letter = (text: string) => (
  <text
    x={W / 2}
    y={H - 2.5}
    textAnchor="middle"
    fontSize={8}
    fontWeight={700}
    fontFamily="system-ui, sans-serif"
    fill={ink}
    stroke="#ffffff"
    strokeWidth={2.5}
    paintOrder="stroke"
  >
    {text}
  </text>
);

const marked = (glyph: ReactNode, text: string) => (
  <>
    {glyph}
    {letter(text)}
  </>
);

const glyphs: Record<UnitType, ReactNode> = {
  LineInfantry: cross,
  FootArtillery: dot,
  // The standard's engineer: a bar with uprights, like a bridge on its piers.
  Engineers: <path d="M8,9 L28,9 M8,9 L8,15 M18,9 L18,15 M28,9 L28,15" />,
  LightInfantry: marked(cross, "L"),
  Partisans: marked(cross, "G"),
  LightCavalry: slash,
  Scouts: marked(slash, "S"),
  MediumCavalry: marked(slash, "M"),
  HeavyCavalry: (
    <>
      {slash}
      <rect x={9} y={7.5} width={18} height={9} rx={4.5} fill="none" />
    </>
  ),
  HorseArtillery: (
    <>
      {slash}
      {dot}
    </>
  ),
  // The standard's supply: a bar across the frame's foot.
  SupplyTrain: <path d={`M0,${String(H - 6)} L${String(W)},${String(H - 6)}`} />,
  // Two guns' dots: the heavy pieces.
  SiegeArtillery: (
    <>
      <circle cx={W / 2 - 6} cy={H / 2} r={4} fill={ink} stroke="none" />
      <circle cx={W / 2 + 6} cy={H / 2} r={4} fill={ink} stroke="none" />
    </>
  ),
};

interface UnitSymbolProps {
  type: UnitType;
  /** The frame's fill: the army's colour, e.g. `armyColorVar(color)`. */
  color: string;
  /** Width in pixels; the height follows (3:2). */
  width?: number;
}

/** A unit's symbol. Decorative: say what the unit is in text (or an aria-label) beside it. */
export function UnitSymbol({ type, color, width = 30 }: UnitSymbolProps) {
  const pad = 3;
  return (
    <svg
      viewBox={`${String(-pad)} ${String(-pad)} ${String(W + pad * 2)} ${String(H + pad * 2)}`}
      width={width}
      height={(width * (H + pad * 2)) / (W + pad * 2)}
      aria-hidden
      focusable="false"
      style={{ display: "block", flexShrink: 0, overflow: "visible" }}
    >
      {/* The halo, then the frame; the glyph is clipped to the frame. */}
      <rect
        x={-1.5}
        y={-1.5}
        width={W + 3}
        height={H + 3}
        fill="none"
        stroke="#ffffff"
        strokeWidth={3}
      />
      <rect x={0} y={0} width={W} height={H} fill={color} stroke={ink} strokeWidth={1.5} />
      <svg
        x={0}
        y={0}
        width={W}
        height={H}
        viewBox={`0 0 ${String(W)} ${String(H)}`}
        overflow="hidden"
      >
        <g stroke={ink} strokeWidth={1.5} fill="none">
          {glyphs[type]}
        </g>
      </svg>
    </svg>
  );
}
