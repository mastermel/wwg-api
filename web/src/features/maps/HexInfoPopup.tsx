import { List, Text } from "@mantine/core";
import { Popup } from "react-map-gl/maplibre";
import type { HexInfo } from "@/features/maps/hex-info";
import classes from "@/features/maps/HexInfoPopup.module.css";
import type { Point } from "@/features/maps/geo";

interface HexInfoPopupProps {
  at: Point;
  info: HexInfo;
  /** Chosen (a click or tap), with everything known and a close button; else a hover label. */
  pinned: boolean;
  onClose: () => void;
}

/**
 * What the viewer knows of a hex, at the hex, inside a CampaignMap: a label with its ground while
 * the mouse is over it, and on a click or tap, a card with everything known of it.
 */
export function HexInfoPopup({ at, info, pinned, onClose }: HexInfoPopupProps) {
  return (
    <Popup
      longitude={at.longitude}
      latitude={at.latitude}
      anchor="bottom"
      offset={12}
      closeButton={pinned}
      closeOnClick={false}
      onClose={onClose}
      maxWidth="280px"
      className={classes.popup}
    >
      <div role={pinned ? "dialog" : "tooltip"} aria-label={info.title}>
        <Text size="sm" fw={600} pr={pinned ? "md" : 0}>
          {info.title}
        </Text>
        {pinned ? (
          <List size="sm" spacing={2} mt={4} listStyleType="none" p={0}>
            {info.lines.map((line) => (
              <List.Item key={line}>{line}</List.Item>
            ))}
          </List>
        ) : (
          <Text size="xs">{info.summary}</Text>
        )}
      </div>
    </Popup>
  );
}
