import { Badge, Button, Stack, Table, Text, Title, VisuallyHidden } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import {
  getGetCampaignQueryKey,
  getListCampaignMembersQueryKey,
  getListMyCampaignsQueryKey,
  useListCampaignMembers,
  useRemoveCampaignMember,
} from "@/api/generated/endpoints/campaigns/campaigns";
import type { CampaignMemberResponse, CampaignResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { QueryState } from "@/components/QueryState";
import { useSession } from "@/features/auth/session-context";
import { canManage } from "@/features/campaigns/campaign-access";
import { useOnline } from "@/lib/use-online";

const fullName = (member: CampaignMemberResponse) => `${member.firstName} ${member.lastName}`;

/** The campaign's members; the Umpire (or an Admin) can remove Players. */
export function MembersSection({ campaign }: { campaign: CampaignResponse }) {
  const members = useListCampaignMembers(campaign.id);
  const { user } = useSession();
  const online = useOnline();
  const queryClient = useQueryClient();
  const remove = useRemoveCampaignMember();
  const [removing, setRemoving] = useState<CampaignMemberResponse | null>(null);
  const manager = canManage(campaign, user);

  const confirmRemove = async (member: CampaignMemberResponse) => {
    try {
      await remove.mutateAsync({ id: campaign.id, memberId: member.id });
      notifications.show({ color: "green", message: `Removed ${fullName(member)}.` });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getListCampaignMembersQueryKey(campaign.id) }),
        queryClient.invalidateQueries({ queryKey: getGetCampaignQueryKey(campaign.id) }),
        queryClient.invalidateQueries({ queryKey: getListMyCampaignsQueryKey() }),
      ]);
    } catch {
      notifications.show({ color: "red", message: "They couldn't be removed. Try again." });
    }
    setRemoving(null);
  };

  return (
    <Stack gap="sm" component="section" aria-labelledby="members-heading">
      <Title order={2} size="h3" id="members-heading">
        Members
      </Title>
      <QueryState query={members}>
        {(list) => (
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Name</Table.Th>
                <Table.Th>Role</Table.Th>
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
                      <Text span c="dimmed">
                        {" "}
                        (you)
                      </Text>
                    )}
                  </Table.Td>
                  <Table.Td>
                    <Badge variant={member.role === "Umpire" ? "filled" : "light"}>
                      {member.role}
                    </Badge>
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
                            setRemoving(member);
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
        opened={removing !== null}
        onClose={() => {
          setRemoving(null);
        }}
        title="Remove this Player?"
        confirmLabel="Remove"
        onConfirm={() => {
          if (removing) void confirmRemove(removing);
        }}
        loading={remove.isPending}
      >
        {removing && fullName(removing)} will lose access to {campaign.name}. They can join again
        with the join link.
      </ConfirmModal>
    </Stack>
  );
}
