import { Button, Group, Notification } from "@mantine/core";
import { IconRefresh } from "@tabler/icons-react";
import { useRegisterSW } from "virtual:pwa-register/react";

/**
 * Registers the service worker and, when a new version has been deployed and downloaded, offers
 * to reload into it. Until then the app keeps running the version it started with.
 */
export function UpdatePrompt() {
  const {
    needRefresh: [needRefresh, setNeedRefresh],
    updateServiceWorker,
  } = useRegisterSW();

  if (!needRefresh) {
    return null;
  }

  return (
    <Notification
      title="Update available"
      withCloseButton
      onClose={() => {
        setNeedRefresh(false);
      }}
      closeButtonProps={{ "aria-label": "Not now" }}
      role="status"
      style={{ position: "fixed", insetInlineEnd: 16, bottom: 80, zIndex: 300, maxWidth: 360 }}
    >
      A new version of WWG Campaigner is ready.
      <Group mt="xs">
        <Button
          size="xs"
          leftSection={<IconRefresh size={16} aria-hidden />}
          onClick={() => {
            if (navigator.serviceWorker.controller) {
              // Activate the waiting version, then reload into it.
              void updateServiceWorker(true);
            } else {
              // First visit: no worker controls this page yet, so the new version is already
              // active and there's nothing to hand over. Just reload.
              window.location.reload();
            }
          }}
        >
          Reload
        </Button>
      </Group>
    </Notification>
  );
}
