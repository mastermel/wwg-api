import { Anchor, Button, Group, Paper, Table, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconBooks, IconPlus } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import {
  getListFactionsQueryKey,
  useCreateFaction,
  useListFactions,
} from "@/api/generated/endpoints/library/library";
import { EmptyState } from "@/components/EmptyState";
import { Page } from "@/components/Page";
import { QueryState } from "@/components/QueryState";
import { NationFlag } from "@/features/armies/identity/NationFlag";
import { useSession } from "@/features/auth/session-context";
import { FactionFormModal } from "@/features/library/FactionFormModal";
import { canEditLibrary } from "@/features/library/library-access";
import { useOnline } from "@/lib/use-online";

/**
 * The library's factions (decision 0015): the club's units, grouped, that every campaign's armies
 * choose from. Everyone sees them; Managers and Admins add them.
 */
export function LibraryPage() {
  const factions = useListFactions();
  const { user } = useSession();
  const online = useOnline();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const create = useCreateFaction();
  const [creating, createModal] = useDisclosure(false);
  const editor = canEditLibrary(user);

  return (
    <Page
      title="Library"
      summary="The club's factions and their units, which every campaign's armies are made from."
      actions={
        editor && (
          <Button
            leftSection={<IconPlus size={16} aria-hidden />}
            onClick={createModal.open}
            disabled={!online}
          >
            New faction
          </Button>
        )
      }
    >
      <QueryState query={factions}>
        {(list) => (
          <Paper withBorder>
            {list.length === 0 ? (
              <EmptyState icon={IconBooks} title="No factions yet">
                {editor
                  ? "Add one with New faction, then its units."
                  : "A Manager hasn't added any yet."}
              </EmptyState>
            ) : (
              <Table horizontalSpacing="lg" verticalSpacing="sm" highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Faction</Table.Th>
                    <Table.Th ta="right">Units</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {list.map((faction) => (
                    <Table.Tr key={faction.id}>
                      <Table.Td>
                        <Group gap="sm" wrap="nowrap">
                          <NationFlag
                            nation={faction.nation}
                            plainColor="var(--mantine-color-gray-5)"
                          />
                          <Anchor
                            fw={500}
                            renderRoot={(props) => (
                              <Link to="/library/$id" params={{ id: faction.id }} {...props} />
                            )}
                          >
                            {faction.name}
                          </Anchor>
                        </Group>
                      </Table.Td>
                      <Table.Td ta="right">
                        <Text inherit>{faction.unitCount}</Text>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            )}
          </Paper>
        )}
      </QueryState>
      {creating && (
        <FactionFormModal
          title="New faction"
          submitLabel="Add faction"
          onClose={createModal.close}
          onSubmit={async (values) => {
            const faction = await create.mutateAsync({ data: values });
            notifications.show({ color: "green", message: `Added ${faction.name}.` });
            await queryClient.invalidateQueries({ queryKey: getListFactionsQueryKey() });
            await navigate({ to: "/library/$id", params: { id: faction.id } });
          }}
        />
      )}
    </Page>
  );
}
