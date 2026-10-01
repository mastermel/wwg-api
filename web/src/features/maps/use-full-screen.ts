import { useEffect, useState } from "react";

/**
 * The Map page's full screen (on a computer): the map over the whole window, and the browser's own
 * full screen too where it allows it, so the window's frame goes as well. The browser's is the
 * whole page's, not the map's, so drawers and dialogs still show over the map. Esc leaves it (the
 * browser's own Esc, or ours where the browser's isn't on), unless it's closing a dialog.
 */
export function useFullScreen() {
  const [full, setFull] = useState(false);

  useEffect(() => {
    if (!full) return;
    const left = () => {
      if (!document.fullscreenElement) setFull(false);
    };
    const escape = (event: KeyboardEvent) => {
      if (event.key !== "Escape" || document.fullscreenElement) return;
      if (event.target instanceof Element && event.target.closest('[role="dialog"]')) return;
      setFull(false);
    };
    // The page behind stays put: the wheel is the map's.
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    document.addEventListener("fullscreenchange", left);
    document.addEventListener("keydown", escape);
    return () => {
      document.body.style.overflow = overflow;
      document.removeEventListener("fullscreenchange", left);
      document.removeEventListener("keydown", escape);
    };
  }, [full]);

  return {
    full,
    enter: () => {
      setFull(true);
      if (document.fullscreenEnabled) {
        // Refused (no gesture, or a setting): the map still fills the window.
        document.documentElement.requestFullscreen().catch(() => undefined);
      }
    },
    exit: () => {
      setFull(false);
      if (document.fullscreenElement) document.exitFullscreen().catch(() => undefined);
    },
  };
}
