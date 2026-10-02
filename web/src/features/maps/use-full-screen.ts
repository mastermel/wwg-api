import { useEffect, useState } from "react";

/**
 * The Map page's full screen (on a computer): the map over the whole window, and the browser's own
 * full screen too where it allows it, so the window's frame goes as well. The browser's is the
 * whole page's, not the map's, so drawers and dialogs still show over the map. Esc leaves it (the
 * browser's own Esc, or ours where the browser's isn't on), unless it's closing a dialog.
 */
export function useFullScreen(
  /** Whether it's on offer (a computer's screen): narrowed past that, it ends. */
  available = true,
) {
  const [chosen, setFull] = useState(false);
  // Narrowed past a computer's screen, it ends, and doesn't come back as the window widens (set
  // while rendering, as React suggests for state that follows a prop).
  const [wasAvailable, setWasAvailable] = useState(available);
  if (available !== wasAvailable) {
    setWasAvailable(available);
    if (!available) setFull(false);
  }
  const full = available && chosen;

  // Left with the browser's full screen on (the window narrowed, or the page left): it ends.
  useEffect(() => {
    if (full) return;
    if (document.fullscreenElement) document.exitFullscreen().catch(() => undefined);
  }, [full]);
  useEffect(
    () => () => {
      if (document.fullscreenElement) document.exitFullscreen().catch(() => undefined);
    },
    [],
  );

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
      if (!available) return;
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
