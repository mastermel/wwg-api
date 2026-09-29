import { Badge, Button, Table, Text, VisuallyHidden } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import {
  useListCampaignMembers,
  useRemoveCampaignMember,
} from "@/api/generated/endpoints/campaigns/campaigns";
import type { CampaignMemberResponse, CampaignResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { QueryState } from "@/components/QueryState";
import { Section } from "@/components/Section";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { useConfirmTarget } from "@/lib/use-confirm-target";
import { useOnline } from "@/lib/use-online";
import { refreshCampaign } from "@/features/campaigns/campaign-cache";

const fullName = (member: CampaignMemberResponse) => `${member.firstName} ${member.lastName}`;

/**
 * The campaign's members and the army each Player commands; the Umpire (or an Admin) can remove
 * Players.
 */
export function MembersSection({ campaign }: { campaign: CampaignResponse }) {
  const members = useListCampaignMembers(campaign.id);
  const { user } = useSession();
  const online = useOnline();
  const queryClient = useQueryClient();
  const remove = useRemoveCampaignMember();
  const removing = useConfirmTarget<CampaignMemberResponse>();
  const manager = canManage(campaign, user);

  const confirmRemove = async (member: CampaignMemberResponse) => {
    try {
      await remove.mutateAsync({ id: campaign.id, memberId: member.id });
      notifications.show({ color: "green", message: `Removed ${fullName(member)}.` });
      // Their army (if any) is now unassigned, so the armies change too.
      await refreshCampaign(queryClient, campaign.id);
    } catch {
      notifications.show({ color: "red", message: "They couldn't be removed. Try again." });
    }
    removing.close();
  };

  return (
    <Section
      title="Members"
      description="The Umpire and the Players, and what each commands."
      flush
    >
      <QueryState query={members}>
        {(list) => (
          <Table horizontalSpacing="lg" highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Name</Table.Th>
                {/* Phones show the role under the name instead, rather than squeeze it. */}
                <Table.Th visibleFrom="sm">Role</Table.Th>
                <Table.Th>Army</Table.Th>
                {manager && (
                  <Table.Th>
                    <VisuallyHidden>Actions</VisuallyHidden>
                  </Table.Th>
                )}
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {list.map((member) => (
                <Table.Tr key={member.id}>
                  <Table.Td>
                    {fullName(member)}
                    {member.userId === user?.id && (
                      <Text span c="dimmed" inherit>
                        {" "}
                        (you)
                      </Text>
                    )}
                    <Badge
                      size="xs"
                      mt={4}
                      display="block"
                      w="fit-content"
                      hiddenFrom="sm"
                      variant={member.role === "Umpire" ? "filled" : "light"}
                    >
                      {member.role}
                    </Badge>
                  </Table.Td>
                  <Table.Td visibleFrom="sm">
                    <Badge variant={member.role === "Umpire" ? "filled" : "light"}>
                      {member.role}
                    </Badge>
                  </Table.Td>
                  <Table.Td>
                    {member.army?.name ?? (
                      <Text span c="dimmed" inherit>
                        {member.role === "Player" ? "None" : "–"}
                      </Text>
                    )}
                  </Table.Td>
                  {manager && (
                    <Table.Td ta="right">
                      {member.role === "Player" && (
                        <Button
                          size="xs"
                          color="red"
                          variant="subtle"
                          disabled={!online}
                          onClick={() => {
                            removing.open(member);
                          }}
                          aria-label={`Remove ${fullName(member)}`}
                        >
                          Remove
                        </Button>
                      )}
                    </Table.Td>
                  )}
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
      </QueryState>
      <ConfirmModal
        opened={removing.opened}
        onClose={removing.close}
        title="Remove this Player?"
        confirmLabel="Remove"
        onConfirm={() => {
          if (removing.target) void confirmRemove(removing.target);
        }}
        loading={remove.isPending}
      >
        {removing.target && fullName(removing.target)} will lose access to {campaign.name}. They can
        join again with the join link.
      </ConfirmModal>
    </Section>
  );
}
