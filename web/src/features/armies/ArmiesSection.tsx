import { Anchor, Button, Table, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconFlag, IconPlus } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useCreateArmy, useListArmies } from "@/api/generated/endpoints/armies/armies";
import { useListCampaignMembers } from "@/api/generated/endpoints/campaigns/campaigns";
import { useListSides } from "@/api/generated/endpoints/sides/sides";
import type { ArmyCommander, CampaignResponse } from "@/api/generated/model";
import { EmptyState } from "@/components/EmptyState";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { commanderOptions } from "@/features/armies/army-access";
import { ArmyFormModal } from "@/features/armies/ArmyFormModal";
import { ArmyBadge } from "@/features/armies/identity/ArmyBadge";
import { freeColor } from "@/features/armies/identity/army-colors";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";
import { sideOptions } from "@/features/sides/side-options";
import { useOnline } from "@/lib/use-online";

/** At most this many armies in a campaign (one per colour); the API refuses a ninth. */
const maxArmies = 8;

const commanderName = (commander: ArmyCommander) => `${commander.firstName} ${commander.lastName}`;

/**
 * Every army in the campaign: its side and who commands it. Every member can open any army
 * and see its units. The Umpire (or an Admin) can add armies, up to 8.
 */
export function ArmiesSection({ campaign }: { campaign: CampaignResponse }) {
  const armies = useListArmies(campaign.id);
  const members = useListCampaignMembers(campaign.id);
  const sides = useListSides(campaign.id);
  const { user } = useSession();
  const online = useOnline();
  const queryClient = useQueryClient();
  const create = useCreateArmy();
  const [creating, { open, close }] = useDisclosure(false);
  const manager = canManage(campaign, user);
  const full = (armies.data?.length ?? 0) >= maxArmies;

  return (
    <Section
      title="Armies"
      description={
        manager && full
          ? `Every army, its side and who commands it. A campaign has at most ${String(maxArmies)}.`
          : "Every army, its side and who commands it."
      }
      flush
      actions={
        manager && (
          <Button
            size="xs"
            leftSection={<IconPlus size={14} aria-hidden />}
            onClick={open}
            disabled={!online || full}
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
                ? "Add one with New army, and give it a side and a commander."
                : "The Umpire hasn't added any yet."}
            </EmptyState>
          ) : (
            <Table horizontalSpacing="lg" highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Army</Table.Th>
                  {/* On phones the side sits under the army, rather than scroll sideways. */}
                  <Table.Th visibleFrom="sm">Side</Table.Th>
                  <Table.Th>Commander</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {list.map((army) => (
                  <Table.Tr key={army.id}>
                    <Table.Td>
                      <ArmyBadge army={army}>
                        <Anchor
                          inherit
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
                      </ArmyBadge>
                      <Text size="xs" c="dimmed" hiddenFrom="sm">
                        {army.side.name}
                      </Text>
                    </Table.Td>
                    <Table.Td visibleFrom="sm">{army.side.name}</Table.Td>
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
          defaultValues={{
            name: "",
            commanderMemberId: null,
            // The first side, to start with: every army is on one (decision 0017).
            sideId: sides.data?.[0]?.id ?? "",
            color: freeColor((armies.data ?? []).map((army) => army.color)),
            nation: "None",
            factionIds: [],
          }}
          commanders={commanderOptions(members.data ?? [])}
          sides={sideOptions(sides.data)}
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
