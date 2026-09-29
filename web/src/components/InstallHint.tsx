import { Alert, Button, Group, Text } from "@mantine/core";
import { IconDeviceMobile, IconDownload } from "@tabler/icons-react";
import { useEffect, useState } from "react";

const dismissedKey = "wwg:install-hint-dismissed";

/** Chrome's install prompt event (not in TypeScript's DOM types). */
interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>;
}

function isStandalone() {
  return (
    window.matchMedia("(display-mode: standalone)").matches ||
    (navigator as Navigator & { standalone?: boolean }).standalone === true
  );
}

function isIos() {
  const ua = navigator.userAgent;
  // iPadOS reports itself as a Mac; its touch screen gives it away.
  return /iPhone|iPad|iPod/.test(ua) || (ua.includes("Macintosh") && navigator.maxTouchPoints > 1);
}

function wasDismissed() {
  try {
    return localStorage.getItem(dismissedKey) === "true";
  } catch {
    return false;
  }
}

/**
 * A small, dismissible hint to install the app. Chrome (desktop and Android) gets an Install
 * button using its own prompt; iOS has no prompt, so it gets instructions instead. Hidden once
 * the app is installed, or after "Not now".
 */
export function InstallHint() {
  const [installEvent, setInstallEvent] = useState<BeforeInstallPromptEvent | null>(null);
  const [dismissed, setDismissed] = useState(wasDismissed);

  useEffect(() => {
    const onPrompt = (event: Event) => {
      // Keep the browser's own mini-infobar from showing; we offer the button instead.
      event.preventDefault();
      setInstallEvent(event as BeforeInstallPromptEvent);
    };
    const onInstalled = () => {
      setInstallEvent(null);
    };
    window.addEventListener("beforeinstallprompt", onPrompt);
    window.addEventListener("appinstalled", onInstalled);
    return () => {
      window.removeEventListener("beforeinstallprompt", onPrompt);
      window.removeEventListener("appinstalled", onInstalled);
    };
  }, []);

  const ios = isIos();
  if (dismissed || isStandalone() || (!installEvent && !ios)) {
    return null;
  }

  const dismiss = () => {
    setDismissed(true);
    try {
      localStorage.setItem(dismissedKey, "true");
    } catch {
      // Storage unavailable (e.g. private mode): the hint just comes back next time.
    }
  };

  return (
    <Alert
      role="status"
      icon={<IconDeviceMobile aria-hidden />}
      title="Install WWG Campaigner"
      withCloseButton
      closeButtonLabel="Not now"
      onClose={dismiss}
      mb="lg"
    >
      {installEvent ? (
        <Group justify="space-between" gap="sm">
          <Text size="sm">Add it to your device to open it like an app, even offline.</Text>
          <Button
            size="xs"
            leftSection={<IconDownload size={16} aria-hidden />}
            onClick={() => {
              void installEvent.prompt().then(() => {
                // The prompt can only be used once.
                setInstallEvent(null);
              });
            }}
          >
            Install
          </Button>
        </Group>
      ) : (
        <Text size="sm">
          Tap the Share button, then Add to Home Screen. The installed app keeps its own sign-in,
          separate from Safari, so you'll sign in once more there.
        </Text>
      )}
    </Alert>
  );
}
