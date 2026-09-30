import { Alert, Badge, Button, Group, NumberInput, Stack, Table, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import {
  getGetMovementTableQueryKey,
  useGetMovementTable,
  useResetMovementTable,
  useSaveMovementTable,
} from "@/api/generated/endpoints/turns/turns";
import type { Ground, MovementClass, MovementTableResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import {
  classLabels,
  groundLabels,
  landClasses,
  landGrounds,
  ratesOf,
  waterGrounds,
} from "@/features/maps/movement";
import { errorMessage } from "@/lib/errors";
import { useOnline } from "@/lib/use-online";

const live = { query: { meta: { persist: false } } } as const;

/** The most hexes a turn the API takes. */
const maxHexes = 20;

type Rates = Record<string, number | undefined>;
const key = (movementClass: MovementClass, ground: Ground) => `${movementClass}/${ground}`;

/** Every cell of the table: land classes on land, boats on water. */
const cells: [MovementClass, Ground][] = [
  ...landClasses.flatMap((c) => landGrounds.map((g): [MovementClass, Ground] => [c, g])),
  ...waterGrounds.map((g): [MovementClass, Ground] => ["Boat", g]),
];

/**
 * The campaign's movement table (step 44): hexes a turn for each class on each ground, the rule
 * book's until the Umpire changes it. Its own form: it's saved apart from the map settings.
 */
export function MovementTableSection({ campaignId }: { campaignId: string }) {
  const table = useGetMovementTable(campaignId, live);
  return (
    <Section
      title="Movement"
      description="Hexes a turn for each kind of unit on each ground; 0 where it can't go. Half a hex takes two turns."
      flush
    >
      <QueryState query={table}>
        {(loaded) => (
          <MovementTableForm key={JSON.stringify(loaded)} campaignId={campaignId} table={loaded} />
        )}
      </QueryState>
    </Section>
  );
}

function MovementTableForm({
  campaignId,
  table,
}: {
  campaignId: string;
  table: MovementTableResponse;
}) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const save = useSaveMovementTable();
  const reset = useResetMovementTable();
  const [resetting, confirmReset] = useDisclosure(false);
  const [failure, setFailure] = useState<string | null>(null);
  const rates = ratesOf(table);
  const [values, setValues] = useState<Rates>(() =>
    Object.fromEntries(cells.map(([c, g]) => [key(c, g), rates(c, g)])),
  );
  const complete = Object.values(values).every((v) => typeof v === "number");
  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: getGetMovementTableQueryKey(campaignId) });

  const submit = async () => {
    setFailure(null);
    try {
      await save.mutateAsync({
        id: campaignId,
        data: {
          rates: cells.map(([c, g]) => ({ class: c, ground: g, hexes: values[key(c, g)] ?? 0 })),
        },
      });
      notifications.show({ color: "green", message: "Saved the movement table." });
      await refresh();
    } catch (error) {
      setFailure(errorMessage(error, "The movement table couldn't be saved. Try again."));
    }
  };

  const confirmedReset = async () => {
    try {
      await reset.mutateAsync({ id: campaignId });
      notifications.show({ color: "green", message: "Back to the rule book's movement table." });
      await refresh();
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "The movement table couldn't be reset. Try again."),
      });
    }
    confirmReset.close();
  };

  return (
    <Stack gap="sm" pb="md">
      {failure && (
        <Alert color="red" role="alert" mx="lg">
          {failure}
        </Alert>
      )}
      <RatesTable
        classes={landClasses}
        grounds={landGrounds}
        values={values}
        onChange={setValues}
        minWidth={560}
      />
      <RatesTable
        classes={["Boat"]}
        grounds={waterGrounds}
        values={values}
        onChange={setValues}
        minWidth={320}
      />
      <Group justify="space-between" px="lg">
        {table.rules ? (
          <Badge variant="light" color="gray">
            The rule book&apos;s
          </Badge>
        ) : (
          <Button variant="subtle" onClick={confirmReset.open} disabled={!online}>
            Use the rule book&apos;s
          </Button>
        )}
        <Group gap="xs">
          {!complete && (
            <Text size="sm" c="dimmed">
              Give every one a number.
            </Text>
          )}
          <Button
            onClick={() => void submit()}
            loading={save.isPending}
            disabled={!online || !complete}
          >
            Save movement table
          </Button>
        </Group>
      </Group>
      <ConfirmModal
        opened={resetting}
        onClose={confirmReset.close}
        title="Use the rule book's movement table?"
        confirmLabel="Use the rule book's"
        color="navy"
        onConfirm={() => void confirmedReset()}
        loading={reset.isPending}
      >
        Your changes to the table are dropped. Orders given from now on go by the rule book&apos;s.
      </ConfirmModal>
    </Stack>
  );
}

interface RatesTableProps {
  classes: readonly MovementClass[];
  grounds: readonly Ground[];
  values: Rates;
  onChange: (change: (current: Rates) => Rates) => void;
  minWidth: number;
}

/** A block of the table: these classes on these grounds, one input each. */
function RatesTable({ classes, grounds, values, onChange, minWidth }: RatesTableProps) {
  return (
    // Wider than a phone: it scrolls sideways inside itself, not the page.
    <Table.ScrollContainer minWidth={minWidth}>
      <Table horizontalSpacing="sm" verticalSpacing={6}>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Unit</Table.Th>
            {grounds.map((ground) => (
              <Table.Th key={ground} ta="center">
                {groundLabels[ground]}
              </Table.Th>
            ))}
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {classes.map((movementClass) => (
            <Table.Tr key={movementClass}>
              <Table.Th scope="row" fw={500}>
                {classLabels[movementClass]}
              </Table.Th>
              {grounds.map((ground) => (
                <Table.Td key={ground}>
                  <NumberInput
                    aria-label={`${classLabels[movementClass]} on ${groundLabels[ground].toLowerCase()}`}
                    value={values[key(movementClass, ground)] ?? ""}
                    onChange={(value) => {
                      onChange((current) => ({
                        ...current,
                        [key(movementClass, ground)]: typeof value === "number" ? value : undefined,
                      }));
                    }}
                    min={0}
                    max={maxHexes}
                    step={0.5}
                    decimalScale={1}
                    allowNegative={false}
                    clampBehavior="strict"
                    // Its step buttons have no accessible names; arrow keys still step.
                    hideControls
                    size="xs"
                    w={64}
                    mx="auto"
                  />
                </Table.Td>
              ))}
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>
    </Table.ScrollContainer>
  );
}
