import {
  ActionIcon,
  Button,
  CopyButton,
  Stack,
  Text,
  TextInput,
  Title,
  Tooltip,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconCheck, IconCopy, IconRefresh } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import {
  getGetJoinCodeQueryKey,
  useGetJoinCode,
  useRegenerateJoinCode,
} from "@/api/generated/endpoints/campaigns/campaigns";
import type { CampaignResponse } from "@/api/generated/model";
import { ConfirmModal } from "@/components/ConfirmModal";
import { QueryState } from "@/components/QueryState";
import { useOnline } from "@/lib/use-online";

/** The link to send to Players (Umpire or Admin), and a way to replace it. */
export function JoinLinkSection({ campaign }: { campaign: CampaignResponse }) {
  const joinCode = useGetJoinCode(campaign.id);
  const online = useOnline();
  const queryClient = useQueryClient();
  const regenerate = useRegenerateJoinCode();
  const [confirming, { open, close }] = useDisclosure(false);

  const confirmRegenerate = async () => {
    try {
      const fresh = await regenerate.mutateAsync({ id: campaign.id });
      queryClient.setQueryData(getGetJoinCodeQueryKey(campaign.id), fresh);
      notifications.show({
        color: "green",
        message: "New join link made. The old one no longer works.",
      });
    } catch {
      notifications.show({ color: "red", message: "A new link couldn't be made. Try again." });
    }
    close();
  };

  return (
    <Stack gap="sm" component="section" aria-labelledby="join-link-heading">
      <Title order={2} size="h3" id="join-link-heading">
        Join link
      </Title>
      <QueryState query={joinCode}>
        {({ joinCode: code }) => {
          const link = `${window.location.origin}/join/${code}`;
          return (
            <Stack gap="xs">
              <TextInput
                label="Send this link to your Players"
                value={link}
                readOnly
                onFocus={(event) => {
                  event.currentTarget.select();
                }}
                rightSection={
                  <CopyButton value={link}>
                    {({ copied, copy }) => (
                      <Tooltip label={copied ? "Copied" : "Copy link"} withArrow>
                        <ActionIcon
                          variant="subtle"
                          onClick={copy}
                          aria-label={copied ? "Copied" : "Copy link"}
                        >
                          {copied ? (
                            <IconCheck size={16} aria-hidden />
                          ) : (
                            <IconCopy size={16} aria-hidden />
                          )}
                        </ActionIcon>
                      </Tooltip>
                    )}
                  </CopyButton>
                }
              />
              <Text size="sm" c="dimmed">
                Anyone with the link can join as a Player once they&apos;ve signed in.
              </Text>
              <div>
                <Button
                  variant="default"
                  size="xs"
                  leftSection={<IconRefresh size={14} aria-hidden />}
                  onClick={open}
                  disabled={!online}
                >
                  Make a new link
                </Button>
              </div>
            </Stack>
          );
        }}
      </QueryState>
      <ConfirmModal
        opened={confirming}
        onClose={close}
        title="Make a new join link?"
        confirmLabel="Make a new link"
        onConfirm={() => void confirmRegenerate()}
        loading={regenerate.isPending}
      >
        The current link will stop working. Players who have already joined stay in the campaign.
      </ConfirmModal>
    </Stack>
  );
}
