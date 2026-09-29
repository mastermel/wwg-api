import { Group } from "@mantine/core";
import type { CSSProperties, ReactNode } from "react";
import type { ArmyColor, Nation } from "@/api/generated/model";
import classes from "@/features/armies/identity/ArmyBadge.module.css";
import { armyColorVar } from "@/features/armies/identity/army-colors";
import { NationFlag } from "@/features/armies/identity/NationFlag";
import { nationLabel } from "@/features/armies/identity/nations";

interface ArmyBadgeProps {
  army: { name: string; color: ArmyColor; nation: Nation };
  /** What to show as the name, e.g. a link to the army; the army's name otherwise. */
  children?: ReactNode;
}

/**
 * An army's name with its flag, framed in its colour: how the app shows an army everywhere, so
 * it's recognisable at a glance. The name is always there, so colour is never the only signal.
 */
export function ArmyBadge({ army, children }: ArmyBadgeProps) {
  const color = armyColorVar(army.color);
  return (
    <Group component="span" gap={8} wrap="nowrap" display="inline-flex" maw="100%">
      <span
        className={classes.flag}
        style={{ "--army-color": color } as CSSProperties}
        title={nationLabel(army.nation)}
      >
        <NationFlag nation={army.nation} plainColor={color} />
      </span>
      <span>{children ?? army.name}</span>
    </Group>
  );
}
