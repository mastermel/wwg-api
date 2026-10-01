import { ActionIcon, Button, Divider, Drawer, Popover, Stack, Switch, Text } from "@mantine/core";
import { useDisclosure, useMediaQuery } from "@mantine/hooks";
import { IconStack2 } from "@tabler/icons-react";
import type { MapLayers } from "@/api/generated/model";
import {
  gameLayers,
  gameMaxHexesAcross,
  realLayers,
  type useHiddenLayers,
} from "@/features/maps/map-layers";

interface MapLayersControlProps {
  /** The campaign's map settings: only what they show can be shown. */
  campaign: MapLayers;
  layers: ReturnType<typeof useHiddenLayers>;
  /** Whether the view is close enough to draw the game map. */
  zoomedIn: boolean;
  /** Whether the viewer sees the Umpire's warnings (contact and concentration). */
  warnings: boolean;
}

/**
 * The Map page's Layers panel, from a button over the map's top-left corner (a popover; on a phone, a
 * sheet from the bottom): the viewer shows or hides the real map's layers and the game map's, of
 * those the campaign's map settings show, or either map whole by its own switch. Remembered on
 * this device.
 */
export function MapLayersControl({ campaign, layers, zoomedIn, warnings }: MapLayersControlProps) {
  const { hidden, toggleReal, toggleGame, toggleGroup, reset } = layers;
  const realShown = !hidden.groups.includes("real");
  const gameShown = !hidden.groups.includes("game");
  const real = realLayers.filter(({ key }) => campaign[key]);
  // The game map is all or nothing in the campaign's settings (its grid).
  const game = campaign.grid ? gameLayers.filter(({ key }) => key !== "warnings" || warnings) : [];
  const changed = hidden.real.length > 0 || hidden.game.length > 0 || hidden.groups.length > 0;
  // A map's own switch: off hides all its layers, keeping theirs as they were for when it's on.
  const groupSwitch = (group: "real" | "game", label: string, shown: boolean) => (
    <Switch
      size="sm"
      label={label}
      checked={shown}
      onChange={() => {
        toggleGroup(group);
      }}
      styles={{ label: { fontWeight: 600 } }}
    />
  );

  const phone = useMediaQuery("(max-width: 48em)");
  const [opened, { toggle, close }] = useDisclosure(false);
  const button = (
    <ActionIcon
      variant="default"
      size="lg"
      aria-label="Map layers"
      onClick={phone ? toggle : undefined}
    >
      <IconStack2 size={18} aria-hidden />
    </ActionIcon>
  );
  const panel = (
    <Stack gap="sm">
      {real.length > 0 && (
        <Stack gap={6} role="group" aria-label="Real map">
          {groupSwitch("real", "Real map", realShown)}
          {real.map(({ key, label }) => (
            <Switch
              key={key}
              size="sm"
              ml="md"
              label={label}
              disabled={!realShown}
              checked={!hidden.real.includes(key)}
              onChange={() => {
                toggleReal(key);
              }}
            />
          ))}
        </Stack>
      )}
      {real.length > 0 && game.length > 0 && <Divider />}
      {game.length > 0 && (
        <Stack gap={6} role="group" aria-label="Game map">
          {groupSwitch("game", "Game map", gameShown)}
          {gameShown && !zoomedIn && (
            <Text size="xs" c="dimmed">
              Zoom in to see it: it&apos;s drawn once {gameMaxHexesAcross} hexes or fewer fill the
              map.
            </Text>
          )}
          {game.map(({ key, label }) => (
            <Switch
              key={key}
              size="sm"
              ml="md"
              label={label}
              disabled={!gameShown}
              checked={!hidden.game.includes(key)}
              onChange={() => {
                toggleGame(key);
              }}
            />
          ))}
        </Stack>
      )}
      {real.length === 0 && game.length === 0 && (
        <Text size="sm">The Umpire has turned off every layer in the map settings.</Text>
      )}
      {changed && (
        <Button size="compact-sm" variant="subtle" onClick={reset}>
          Show everything again
        </Button>
      )}
    </Stack>
  );

  // On a phone it would run past the screen's foot: a sheet from the bottom instead.
  return phone ? (
    <>
      {button}
      <Drawer
        opened={opened}
        onClose={close}
        position="bottom"
        size="auto"
        title="Map layers"
        closeButtonProps={{ "aria-label": "Close" }}
      >
        {panel}
      </Drawer>
    </>
  ) : (
    <Popover position="bottom-start" shadow="md" withArrow trapFocus>
      <Popover.Target>{button}</Popover.Target>
      <Popover.Dropdown maw={280}>{panel}</Popover.Dropdown>
    </Popover>
  );
}
