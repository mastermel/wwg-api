import { List, Stack, Text } from "@mantine/core";
import { useListPointsHistory } from "@/api/generated/endpoints/army-units/army-units";
import { describePointsChange } from "@/features/maps/points-history";

const live = { query: { meta: { persist: false } } } as const;

/**
 * A unit's points history (step 47, decision 0018), in the unit drawer: every change once the
 * campaign started, oldest first. Nothing while there's none.
 */
export function UnitPointsHistory({ unitId }: { unitId: string }) {
  const history = useListPointsHistory(unitId, live);
  if (!history.data?.length) return null;
  return (
    <Stack gap={4}>
      <Text size="sm" fw={600} id={`points-history-${unitId}`}>
        Points history
      </Text>
      <List size="sm" spacing={2} aria-labelledby={`points-history-${unitId}`}>
        {history.data.map((change) => (
          <List.Item key={`${String(change.turn)}-${change.at}`}>
            {describePointsChange(change)}
          </List.Item>
        ))}
      </List>
    </Stack>
  );
}
