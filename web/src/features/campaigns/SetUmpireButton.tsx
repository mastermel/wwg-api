import { Alert, Button, Group, Modal, Select, Stack, Text } from "@mantine/core";
import { useDebouncedValue, useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconCrown } from "@tabler/icons-react";
import { keepPreviousData, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import {
  getListAllCampaignsQueryKey,
  useListUsers,
  useSetCampaignUmpire,
} from "@/api/generated/endpoints/admin/admin";
import {
  getGetCampaignQueryKey,
  getListCampaignMembersQueryKey,
  getListMyCampaignsQueryKey,
} from "@/api/generated/endpoints/campaigns/campaigns";
import type { CampaignResponse } from "@/api/generated/model";
import { useOnline } from "@/lib/use-online";

/** For Admins: choose any user as the campaign's Umpire (§5.1, umpire-less campaigns). */
export function SetUmpireButton({ campaign }: { campaign: CampaignResponse }) {
  const online = useOnline();
  const [opened, { open, close }] = useDisclosure(false);

  return (
    <>
      <Button
        variant="default"
        leftSection={<IconCrown size={16} aria-hidden />}
        onClick={open}
        disabled={!online}
      >
        {campaign.umpire ? "Change Umpire" : "Set Umpire"}
      </Button>
      {/* Only mounted while open, so it searches users only when asked. */}
      {opened && <SetUmpireModal campaign={campaign} onClose={close} />}
    </>
  );
}

function SetUmpireModal({
  campaign,
  onClose,
}: {
  campaign: CampaignResponse;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const setUmpire = useSetCampaignUmpire();
  const [search, setSearch] = useState("");
  const [debounced] = useDebouncedValue(search.trim(), 300);
  const [userId, setUserId] = useState<string | null>(null);
  const users = useListUsers(
    { search: debounced || undefined, pageSize: 20 },
    { query: { meta: { persist: false }, placeholderData: keepPreviousData } },
  );
  const options = (users.data?.items ?? [])
    .filter((user) => user.id !== campaign.umpire?.userId)
    .map((user) => ({
      value: user.id,
      label: `${user.firstName} ${user.lastName} (${user.email})`,
    }));

  const confirm = async () => {
    if (!userId) return;
    try {
      const updated = await setUmpire.mutateAsync({ id: campaign.id, data: { userId } });
      queryClient.setQueryData(getGetCampaignQueryKey(campaign.id), updated);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getListCampaignMembersQueryKey(campaign.id) }),
        queryClient.invalidateQueries({ queryKey: getListMyCampaignsQueryKey() }),
        queryClient.invalidateQueries({ queryKey: getListAllCampaignsQueryKey() }),
      ]);
      const umpire = updated.umpire;
      notifications.show({
        color: "green",
        message: umpire
          ? `${umpire.firstName} ${umpire.lastName} is now the Umpire.`
          : "The Umpire is set.",
      });
      onClose();
    } catch {
      notifications.show({ color: "red", message: "The Umpire couldn't be set. Try again." });
    }
  };

  return (
    <Modal opened onClose={onClose} title="Choose the Umpire" centered>
      <Stack>
        <Select
          label="New Umpire"
          description="Search by name or email. They don't need to be in the campaign yet."
          placeholder="Start typing a name"
          searchable
          searchValue={search}
          onSearchChange={setSearch}
          // The API has already filtered by the search.
          filter={({ options: all }) => all}
          data={options}
          value={userId}
          onChange={setUserId}
          nothingFoundMessage={users.isFetching ? "Searching…" : "No users match."}
          comboboxProps={{ withinPortal: false }}
        />
        {campaign.umpire && (
          <Alert color="gray">
            <Text size="sm">
              {campaign.umpire.firstName} {campaign.umpire.lastName} will become a Player.
            </Text>
          </Alert>
        )}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={() => void confirm()} loading={setUmpire.isPending} disabled={!userId}>
            Set Umpire
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
