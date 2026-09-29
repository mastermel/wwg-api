import { Anchor, Button, Table, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconFlag, IconPlus } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useCreateArmy, useListArmies } from "@/api/generated/endpoints/armies/armies";
import { useListCampaignMembers } from "@/api/generated/endpoints/campaigns/campaigns";
import { useListFactions } from "@/api/generated/endpoints/factions/factions";
import type { ArmyCommander, ArmySummary, CampaignResponse } from "@/api/generated/model";
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
import { factionOptions } from "@/features/factions/faction-options";
import { useOnline } from "@/lib/use-online";

/** At most this many armies in a campaign (one per colour); the API refuses a ninth. */
const maxArmies = 8;

const commanderName = (commander: ArmyCommander) => `${commander.firstName} ${commander.lastName}`;

/** A faction's name, or "Unassigned". */
function FactionName({ army }: { army: ArmySummary }) {
  return army.faction ? (
    army.faction.name
  ) : (
    <Text span c="dimmed" inherit>
      Unassigned
    </Text>
  );
}

/**
 * Every army in the campaign: its faction and who commands it. Every member can open any army
 * and see its units. The Umpire (or an Admin) can add armies, up to 8.
 */
export function ArmiesSection({ campaign }: { campaign: CampaignResponse }) {
  const armies = useListArmies(campaign.id);
  const members = useListCampaignMembers(campaign.id);
  const factions = useListFactions(campaign.id);
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
          ? `Every army, its faction and who commands it. A campaign has at most ${String(maxArmies)}.`
          : "Every army, its faction and who commands it."
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
                ? "Add one with New army, and give it a faction and a commander."
                : "The Umpire hasn't added any yet."}
            </EmptyState>
          ) : (
            <Table horizontalSpacing="lg" highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Army</Table.Th>
                  {/* On phones the faction sits under the army, rather than scroll sideways. */}
                  <Table.Th visibleFrom="sm">Faction</Table.Th>
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
                        <FactionName army={army} />
                      </Text>
                    </Table.Td>
                    <Table.Td visibleFrom="sm">
                      <FactionName army={army} />
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
          defaultValues={{
            name: "",
            commanderMemberId: null,
            factionId: null,
            color: freeColor((armies.data ?? []).map((army) => army.color)),
            nation: "None",
          }}
          commanders={commanderOptions(members.data ?? [])}
          factions={factionOptions(factions.data)}
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
