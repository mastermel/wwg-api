import { Group, SimpleGrid, Text } from "@mantine/core";
import { UnitType } from "@/api/generated/model";
import { UnitSymbol } from "@/features/units/UnitSymbol";
import { unitTypeLabels } from "@/features/units/unit-types";

/** What each unit symbol means (the map's legend). Frames are grey here: on the map, the army's colour. */
export function UnitLegend() {
  return (
    <SimpleGrid
      cols={{ base: 2, sm: 1 }}
      spacing={6}
      verticalSpacing={6}
      component="ul"
      p={0}
      m={0}
    >
      {Object.values(UnitType).map((type) => (
        <Group key={type} component="li" gap="xs" wrap="nowrap" style={{ listStyle: "none" }}>
          <UnitSymbol type={type} color="var(--mantine-color-silver-3)" width={28} />
          <Text size="sm">{unitTypeLabels[type]}</Text>
        </Group>
      ))}
    </SimpleGrid>
  );
}
