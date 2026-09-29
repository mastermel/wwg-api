import { Anchor, Button, Table, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconFlag, IconPlus } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useCreateArmy, useListArmies } from "@/api/generated/endpoints/armies/armies";
import { useListCampaignMembers } from "@/api/generated/endpoints/campaigns/campaigns";
import type { ArmyCommander, CampaignResponse } from "@/api/generated/model";
import { EmptyState } from "@/components/EmptyState";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { canViewArmy, commanderOptions } from "@/features/armies/army-access";
import { ArmyFormModal } from "@/features/armies/ArmyFormModal";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { useOnline } from "@/lib/use-online";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";

const commanderName = (commander: ArmyCommander) => `${commander.firstName} ${commander.lastName}`;

/** Every army in the campaign and who commands it. The Umpire (or an Admin) can add armies. */
export function ArmiesSection({ campaign }: { campaign: CampaignResponse }) {
  const armies = useListArmies(campaign.id);
  const members = useListCampaignMembers(campaign.id);
  const { user } = useSession();
  const online = useOnline();
  const queryClient = useQueryClient();
  const create = useCreateArmy();
  const [creating, { open, close }] = useDisclosure(false);
  const manager = canManage(campaign, user);

  return (
    <Section
      title="Armies"
      description="Every army and who commands it."
      flush
      actions={
        manager && (
          <Button
            size="xs"
            leftSection={<IconPlus size={14} aria-hidden />}
            onClick={open}
            disabled={!online}
          >
            New army
          </Button>
        )
      }
    >
      <QueryState query={armies}>
        {(list) =>
          list.length === 0 ? (
            <EmptyState icon={IconFlag} title="No armies yet">
              {manager
                ? "Add one with New army, and give it a commander."
                : "The Umpire hasn't added any yet."}
            </EmptyState>
          ) : (
            <Table horizontalSpacing="lg" highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Army</Table.Th>
                  <Table.Th>Commander</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {list.map((army) => (
                  <Table.Tr key={army.id}>
                    <Table.Td>
                      {canViewArmy(campaign, army.commander, user) ? (
                        <Anchor
                          renderRoot={(props) => (
                            <Link
                              to="/campaigns/$id/armies/$armyId"
                              params={{ id: campaign.id, armyId: army.id }}
                              {...props}
                            />
                          )}
                        >
                          {army.name}
                        </Anchor>
                      ) : (
                        army.name
                      )}
                    </Table.Td>
                    <Table.Td>
                      {army.commander ? (
                        commanderName(army.commander)
                      ) : (
                        <Text span c="dimmed" inherit>
                          Unassigned
                        </Text>
                      )}
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          )
        }
      </QueryState>
      {creating && (
        <ArmyFormModal
          title="New army"
          submitLabel="Add army"
          commanders={commanderOptions(members.data ?? [])}
          onClose={close}
          onSubmit={async (values) => {
            const army = await create.mutateAsync({ id: campaign.id, data: values });
            notifications.show({ color: "green", message: `Added ${army.name}.` });
            await refreshCampaign(queryClient, campaign.id);
          }}
        />
      )}
    </Section>
  );
}
