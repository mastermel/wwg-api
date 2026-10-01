import { Menu, useMantineColorScheme, type MantineColorScheme } from "@mantine/core";
import { IconCheck, IconDeviceDesktop, IconMoon, IconSun } from "@tabler/icons-react";

const choices: { value: MantineColorScheme; label: string; icon: typeof IconSun }[] = [
  { value: "auto", label: "Match the system", icon: IconDeviceDesktop },
  { value: "light", label: "Light", icon: IconSun },
  { value: "dark", label: "Dark", icon: IconMoon },
];

/**
 * The account menu's theme choice: the system's (the default), or light or dark, which this
 * device remembers (Mantine keeps it in localStorage; public/color-scheme.js reads it before the
 * first paint).
 */
export function ThemeMenuItems() {
  const { colorScheme, setColorScheme } = useMantineColorScheme();
  return (
    <>
      <Menu.Label>Theme</Menu.Label>
      {choices.map(({ value, label, icon: ChoiceIcon }) => (
        <Menu.Item
          key={value}
          // Mantine keeps its items' menuitem role, where aria-checked isn't allowed.
          aria-current={colorScheme === value ? "true" : undefined}
          leftSection={<ChoiceIcon size={16} aria-hidden />}
          rightSection={colorScheme === value ? <IconCheck size={14} aria-hidden /> : undefined}
          onClick={() => {
            setColorScheme(value);
          }}
        >
          {label}
        </Menu.Item>
      ))}
    </>
  );
}
