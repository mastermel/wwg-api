import type { ReactNode } from "react";
import type { Nation } from "@/api/generated/model";

/**
 * Simplified flags of the nations and states of the Napoleonic wars: their colours and main
 * shapes, without eagles, arms or crowns, so they read at icon size. Drawn for the app on a 3:2
 * canvas (30 × 20). None is a plain flag in the army's colour.
 */

const W = 30;
const H = 20;

/** Horizontal stripes, top to bottom; `weights` gives their relative heights. */
function horizontal(colors: string[], weights = colors.map(() => 1)): ReactNode {
  const total = weights.reduce((a, b) => a + b, 0);
  let y = 0;
  return colors.map((fill, i) => {
    const height = (H * (weights[i] ?? 1)) / total;
    const stripe = <rect key={i} x={0} y={y} width={W} height={height} fill={fill} />;
    y += height;
    return stripe;
  });
}

/** Vertical stripes, left to right, equal widths. */
function vertical(colors: string[]): ReactNode {
  const width = W / colors.length;
  return colors.map((fill, i) => (
    <rect key={i} x={i * width} y={0} width={width} height={H} fill={fill} />
  ));
}

/** A Nordic cross: `cross` on `field`, the upright a third of the way in. */
function nordic(field: string, cross: string): ReactNode {
  return (
    <>
      <rect width={W} height={H} fill={field} />
      <rect x={9} y={0} width={4} height={H} fill={cross} />
      <rect x={0} y={8} width={W} height={4} fill={cross} />
    </>
  );
}

const flags: Record<Exclude<Nation, "None">, () => ReactNode> = {
  France: () => vertical(["#002654", "#ffffff", "#ce1126"]),
  Britain: () => (
    <>
      <rect width={W} height={H} fill="#012169" />
      <path d="M0,0 L30,20 M30,0 L0,20" stroke="#ffffff" strokeWidth={4} />
      <path d="M0,0 L30,20 M30,0 L0,20" stroke="#c8102e" strokeWidth={1.4} />
      <path d="M15,0 V20 M0,10 H30" stroke="#ffffff" strokeWidth={6} />
      <path d="M15,0 V20 M0,10 H30" stroke="#c8102e" strokeWidth={3.4} />
    </>
  ),
  Prussia: () => horizontal(["#ffffff", "#000000"]),
  Austria: () => horizontal(["#000000", "#f5c400"]),
  Russia: () => (
    <>
      <rect width={W} height={H} fill="#ffffff" />
      <path d="M0,0 L30,20 M30,0 L0,20" stroke="#0039a6" strokeWidth={4} />
    </>
  ),
  Spain: () => horizontal(["#aa151b", "#f1bf00", "#aa151b"], [1, 2, 1]),
  Portugal: () => (
    <>
      <rect width={W} height={H} fill="#ffffff" />
      <path d="M11,5 H19 V11 Q19,16 15,17 Q11,16 11,11 Z" fill="#1c3f94" />
    </>
  ),
  Sweden: () => nordic("#006aa7", "#fecc00"),
  Denmark: () => nordic("#c8102e", "#ffffff"),
  Holland: () => horizontal(["#ae1c28", "#ffffff", "#21468b"]),
  // Blue and white lozenges, in offset rows (the SVG clips those at the edges).
  Bavaria: () => (
    <>
      <rect width={W} height={H} fill="#ffffff" />
      {[0, 1, 2, 3].flatMap((row) =>
        [-1, 0, 1, 2, 3, 4, 5].map((col) => {
          const x = col * 6 + (row % 2) * 3;
          const y = row * 5;
          return (
            <path
              key={`${String(row)}-${String(col)}`}
              d={`M${String(x + 3)},${String(y)} L${String(x + 6)},${String(y + 2.5)} L${String(x + 3)},${String(y + 5)} L${String(x)},${String(y + 2.5)} Z`}
              fill="#0098d4"
            />
          );
        }),
      )}
    </>
  ),
  Saxony: () => horizontal(["#ffffff", "#009a49"]),
  Wurttemberg: () => horizontal(["#000000", "#dd0000"]),
  Westphalia: () => horizontal(["#ffffff", "#0053a0"]),
  Baden: () => horizontal(["#f5c400", "#dd0000", "#f5c400"]),
  Warsaw: () => horizontal(["#ffffff", "#dc143c"]),
  Italy: () => (
    <>
      <rect width={W} height={H} fill="#ce2b37" />
      <path d="M15,1 L29,10 L15,19 L1,10 Z" fill="#ffffff" />
      <rect x={10.5} y={5.5} width={9} height={9} fill="#009246" />
    </>
  ),
  Naples: () => (
    <>
      <rect width={W} height={H} fill="#1c3f94" />
      <rect x={3} y={3} width={24} height={14} fill="#ffffff" />
      <rect x={6} y={6} width={18} height={8} fill="#c8102e" />
    </>
  ),
  Brunswick: () => (
    <>
      <rect width={W} height={H} fill="#111111" />
      <rect x={0} y={15} width={W} height={2.5} fill="#5b9bd5" />
      <rect x={0} y={17.5} width={W} height={2.5} fill="#f5c400" />
    </>
  ),
  Hanover: () => horizontal(["#f5c400", "#ffffff"]),
  Ottoman: () => (
    <>
      <rect width={W} height={H} fill="#e30a17" />
      <circle cx={12} cy={10} r={5} fill="#ffffff" />
      <circle cx={13.3} cy={10} r={4} fill="#e30a17" />
      <path
        d="M21.80,10.00 L20.05,10.62 L20.00,12.47 L18.88,11.00 L17.10,11.53 L18.15,10.00 L17.10,8.47 L18.88,9.00 L20.00,7.53 L20.05,9.38 Z"
        fill="#ffffff"
      />
    </>
  ),
};

interface NationFlagProps {
  nation: Nation;
  /** Fills the plain flag (None); a CSS colour such as `armyColorVar(color)`. */
  plainColor: string;
  /** Width in pixels; the height follows (3:2). */
  width?: number;
}

/** A nation's simplified flag. Decorative: the army's name is always beside it. */
export function NationFlag({ nation, plainColor, width = 24 }: NationFlagProps) {
  return (
    <svg
      viewBox={`0 0 ${String(W)} ${String(H)}`}
      width={width}
      height={(width * H) / W}
      aria-hidden
      focusable="false"
      style={{ display: "block", flexShrink: 0, overflow: "hidden" }}
    >
      {nation === "None" ? <rect width={W} height={H} fill={plainColor} /> : flags[nation]()}
    </svg>
  );
}
