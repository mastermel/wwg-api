import { ActionIcon, Table, Text } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { IconEdit } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { useListSides, useRenameSide } from "@/api/generated/endpoints/sides/sides";
import type { CampaignResponse, SideResponse } from "@/api/generated/model";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";
import { SideFormModal } from "@/features/sides/SideFormModal";
import { useConfirmTarget } from "@/lib/use-confirm-target";
import { useOnline } from "@/lib/use-online";

const armies = (count: number) => (count === 1 ? "1 army" : `${String(count)} armies`);

/**
 * The campaign's two sides (decision 0017): made with it, every army on one. Every member sees
 * them; the Umpire (or an Admin) renames them, and puts each army on one (on the army's page).
 */
export function SidesSection({ campaign }: { campaign: CampaignResponse }) {
  const sides = useListSides(campaign.id);
  const { user } = useSession();
  const online = useOnline();
  const queryClient = useQueryClient();
  const rename = useRenameSide();
  const renaming = useConfirmTarget<SideResponse>();
  const manager = canManage(campaign, user);

  return (
    <Section title="Sides" description="The two sides the armies fight on." flush>
      <QueryState query={sides}>
        {(list) => (
          <Table horizontalSpacing="lg">
            <Table.Tbody>
              {list.map((side) => (
                <Table.Tr key={side.id}>
                  <Table.Td>
                    <Text fw={500} inherit>
                      {side.name}
                    </Text>
                    <Text size="xs" c="dimmed">
                      {armies(side.armyCount)}
                    </Text>
                  </Table.Td>
                  {manager && (
                    <Table.Td ta="right">
                      <ActionIcon
                        variant="subtle"
                        aria-label={`Rename ${side.name}`}
                        disabled={!online}
                        onClick={() => {
                          renaming.open(side);
                        }}
                      >
                        <IconEdit size={16} aria-hidden />
                      </ActionIcon>
                    </Table.Td>
                  )}
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
      </QueryState>
      {renaming.opened && renaming.target && (
        <SideFormModal
          title="Rename side"
          submitLabel="Save"
          defaultName={renaming.target.name}
          onClose={renaming.close}
          onSubmit={async (values) => {
            const target = renaming.target;
            if (!target) return;
            const side = await rename.mutateAsync({ id: target.id, data: values });
            notifications.show({ color: "green", message: `Renamed to ${side.name}.` });
            await refreshCampaign(queryClient, campaign.id);
          }}
        />
      )}
    </Section>
  );
}
