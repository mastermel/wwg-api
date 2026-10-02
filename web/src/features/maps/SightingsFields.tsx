import {
  Badge,
  Button,
  Checkbox,
  Group,
  SegmentedControl,
  Select,
  Stack,
  Text,
} from "@mantine/core";
import { IconPlus } from "@tabler/icons-react";
import { useState } from "react";
import type { ArmySummary, ForceSize, SightingStrength } from "@/api/generated/model";
import type { UnitInHex } from "@/features/maps/contact";
import { hexKey, hexName } from "@/features/maps/hex-grid";
import { suggestedSize } from "@/features/maps/sightings";
import type { SightingEntry } from "@/features/maps/sighting-entries";
import { unitTypeLabels } from "@/features/units/unit-types";

const strengths: { value: SightingStrength; label: string }[] = [
  { value: "Hidden", label: "Hidden" },
  { value: "Rough", label: "Rough" },
  { value: "Exact", label: "Exact" },
];

const sizes: { value: ForceSize; label: string }[] = [
  { value: "Small", label: "Small" },
  { value: "Medium", label: "Medium" },
  { value: "Large", label: "Large" },
];

interface SightingsFieldsProps {
  entries: readonly SightingEntry[];
  onChange: (entries: SightingEntry[]) => void;
  armies: readonly ArmySummary[];
  /** Where the units will be, for the hexes a sighting can be added for. */
  places: readonly UnitInHex[];
}

/**
 * The start-turn dialog's sightings (step 49b, decision 0020): each army's sight of the other
 * side's hexes, prefilled, for the Umpire to change, leave out or add to.
 */
export function SightingsFields({ entries, onChange, armies, places }: SightingsFieldsProps) {
  const [adding, setAdding] = useState<{ armyId: string | null; hex: string | null } | null>(null);
  const change = (index: number, changes: Partial<SightingEntry>) => {
    onChange(entries.map((e, i) => (i === index ? { ...e, ...changes } : e)));
  };
  const armyName = (id: string) => armies.find((a) => a.id === id)?.name ?? "An army";
  // The other side's hexes an army has no sighting of yet, for adding one.
  const addable = (armyId: string) => {
    const side = armies.find((a) => a.id === armyId)?.side.id;
    const taken = new Set(
      entries.filter((e) => e.observingArmyId === armyId).map((e) => hexKey(e)),
    );
    const hexes = new Map<string, UnitInHex[]>();
    for (const place of places.filter((p) => p.army.side.id !== side)) {
      const key = hexKey(place.hex);
      if (!taken.has(key)) hexes.set(key, [...(hexes.get(key) ?? []), place]);
    }
    return [...hexes.values()].map((units) => ({
      value: hexKey(units[0]?.hex ?? { q: 0, r: 0 }),
      label: `${hexName(units[0]?.hex ?? { q: 0, r: 0 })}: ${units.map((u) => u.unit.name).join(", ")}`,
      units,
    }));
  };
  const add = () => {
    if (!adding?.armyId || !adding.hex) return;
    const found = addable(adding.armyId).find((h) => h.value === adding.hex);
    if (!found) return;
    const [q = 0, r = 0] = found.value.split(",").map(Number);
    const units = found.units.map((u) => ({
      unitId: u.unit.id,
      armyId: u.army.id,
      name: u.unit.name,
      type: u.unit.type,
      points: u.unit.points,
      afloat: (u.boats?.length ?? 0) > 0,
    }));
    onChange([
      ...entries,
      {
        observingArmyId: adding.armyId,
        q,
        r,
        whereabouts: "Reported (spies, scouts)",
        screened: false,
        units,
        byHand: true,
        include: true,
        showsHex: true,
        showsArmies: true,
        showsTypes: true,
        showsAfloat: units.some((u) => u.afloat),
        strength: "Rough",
        size: suggestedSize(units.reduce((sum, u) => sum + u.points, 0)),
      },
    ]);
    setAdding(null);
  };

  return (
    <Stack gap="xs">
      <Text size="sm" fw={600}>
        Sightings
      </Text>
      <Text size="xs" c="dimmed">
        What each army sees of the other side as the next turn starts: choose what it learns, or
        leave one out.
      </Text>
      {entries.length === 0 && (
        <Text size="sm" c="dimmed">
          No army has the other side in sight.
        </Text>
      )}
      {entries.map((entry, index) => {
        const label = `${armyName(entry.observingArmyId)} sees ${hexName(entry)}`;
        return (
          <Stack
            key={`${entry.observingArmyId}-${hexKey(entry)}`}
            gap={6}
            p="xs"
            style={{ border: "1px solid var(--mantine-color-default-border)", borderRadius: 4 }}
            role="group"
            aria-label={label}
          >
            <Group justify="space-between" wrap="nowrap" gap="xs">
              <Checkbox
                label={label}
                checked={entry.include}
                onChange={(event) => {
                  change(index, { include: event.currentTarget.checked });
                }}
              />
              {entry.screened && (
                <Badge color="orange" variant="light">
                  Possible screen
                </Badge>
              )}
              {entry.byHand && (
                <Badge color="gray" variant="light">
                  Added
                </Badge>
              )}
            </Group>
            <Text size="xs" c="dimmed">
              {entry.whereabouts}.{" "}
              {entry.units
                .map((u) => `${u.name} (${unitTypeLabels[u.type]}, ${String(u.points)} points)`)
                .join("; ")}
              .
            </Text>
            {entry.include && (
              <>
                <Group gap="md">
                  <Checkbox
                    size="xs"
                    label="The hex"
                    checked={entry.showsHex}
                    onChange={(event) => {
                      change(index, { showsHex: event.currentTarget.checked });
                    }}
                  />
                  <Checkbox
                    size="xs"
                    label="Army and nation"
                    checked={entry.showsArmies}
                    onChange={(event) => {
                      change(index, { showsArmies: event.currentTarget.checked });
                    }}
                  />
                  <Checkbox
                    size="xs"
                    label="Unit types"
                    checked={entry.showsTypes}
                    onChange={(event) => {
                      change(index, { showsTypes: event.currentTarget.checked });
                    }}
                  />
                  {entry.units.some((u) => u.afloat) && (
                    <Checkbox
                      size="xs"
                      label="On boats"
                      checked={entry.showsAfloat}
                      onChange={(event) => {
                        change(index, { showsAfloat: event.currentTarget.checked });
                      }}
                    />
                  )}
                </Group>
                <Group gap="xs" align="flex-end">
                  <Stack gap={2}>
                    <Text size="xs" fw={500} id={`strength-${String(index)}`}>
                      Strength
                    </Text>
                    <SegmentedControl
                      size="xs"
                      aria-labelledby={`strength-${String(index)}`}
                      data={strengths}
                      value={entry.strength}
                      onChange={(value) => {
                        change(index, { strength: value });
                      }}
                    />
                  </Stack>
                  {entry.strength === "Rough" && (
                    <Select
                      size="xs"
                      w={110}
                      label="Size"
                      data={sizes}
                      value={entry.size}
                      allowDeselect={false}
                      onChange={(value) => {
                        if (value) change(index, { size: value });
                      }}
                      comboboxProps={{ withinPortal: false }}
                    />
                  )}
                </Group>
              </>
            )}
          </Stack>
        );
      })}
      {adding ? (
        <Stack gap="xs">
          <Select
            size="xs"
            label="Army told"
            data={armies.map((a) => ({ value: a.id, label: a.name }))}
            value={adding.armyId}
            onChange={(value) => {
              setAdding({ armyId: value, hex: null });
            }}
            comboboxProps={{ withinPortal: false }}
          />
          {adding.armyId && (
            <Select
              size="xs"
              label="What it's told of"
              data={addable(adding.armyId)}
              value={adding.hex}
              onChange={(value) => {
                setAdding({ ...adding, hex: value });
              }}
              comboboxProps={{ withinPortal: false }}
            />
          )}
          <Group gap="xs">
            <Button size="compact-sm" disabled={!adding.hex} onClick={add}>
              Add
            </Button>
            <Button
              size="compact-sm"
              variant="default"
              onClick={() => {
                setAdding(null);
              }}
            >
              Cancel
            </Button>
          </Group>
        </Stack>
      ) : (
        <Button
          size="compact-sm"
          variant="subtle"
          leftSection={<IconPlus size={14} aria-hidden />}
          onClick={() => {
            setAdding({ armyId: null, hex: null });
          }}
          style={{ alignSelf: "flex-start" }}
        >
          Add a sighting (spies, scouts)
        </Button>
      )}
    </Stack>
  );
}
