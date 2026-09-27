import { AppShell, Group, NavLink, Text, UnstyledButton } from "@mantine/core";
import { IconInfoCircle, IconSwords, type Icon } from "@tabler/icons-react";
import { Link, Outlet, useMatchRoute } from "@tanstack/react-router";
import { BrandMark } from "@/components/BrandMark";
import { InstallHint } from "@/components/InstallHint";
import { UpdatePrompt } from "@/components/UpdatePrompt";

interface NavItem {
  to: "/campaigns" | "/about";
  label: string;
  icon: Icon;
}

const navItems: NavItem[] = [
  { to: "/campaigns", label: "Campaigns", icon: IconSwords },
  { to: "/about", label: "About", icon: IconInfoCircle },
];

/**
 * The app frame: a header, a sidebar on desktop, and a bottom tab bar on phones (below Mantine's
 * `sm` breakpoint). Both navigations use the same items; CSS shows one or the other, and they
 * have distinct labels so they're never two identical landmarks.
 */
export function AppLayout() {
  const matchRoute = useMatchRoute();
  const isActive = (to: NavItem["to"]) => Boolean(matchRoute({ to, fuzzy: true }));

  return (
    <AppShell
      header={{ height: 56 }}
      navbar={{ width: 240, breakpoint: "sm", collapsed: { mobile: true } }}
      footer={{ height: { base: 64, sm: 0 } }}
      padding="lg"
    >
      <AppShell.Header px="lg">
        <Group h="100%">
          <UnstyledButton component={Link} to="/" aria-label="WWG Campaigner, start page">
            <Group gap="xs">
              <BrandMark size={32} />
              <Text fw={700} size="lg">
                WWG Campaigner
              </Text>
            </Group>
          </UnstyledButton>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar p="sm" aria-label="Main">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            component={Link}
            to={item.to}
            label={item.label}
            leftSection={<item.icon size={20} aria-hidden />}
            active={isActive(item.to)}
            aria-current={isActive(item.to) ? "page" : undefined}
          />
        ))}
      </AppShell.Navbar>

      <AppShell.Main>
        <InstallHint />
        <Outlet />
      </AppShell.Main>

      <AppShell.Footer hiddenFrom="sm" component="nav" aria-label="Main tabs">
        <Group grow h="100%" gap={0}>
          {navItems.map((item) => (
            <UnstyledButton
              key={item.to}
              component={Link}
              to={item.to}
              aria-current={isActive(item.to) ? "page" : undefined}
              // The anchor colour meets AA in both schemes; the filled primary is too dark on dark.
              c={isActive(item.to) ? "var(--mantine-color-anchor)" : "dimmed"}
              h="100%"
              style={{
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                justifyContent: "center",
              }}
            >
              <item.icon size={22} aria-hidden />
              <Text size="xs" c="inherit">
                {item.label}
              </Text>
            </UnstyledButton>
          ))}
        </Group>
      </AppShell.Footer>

      <UpdatePrompt />
    </AppShell>
  );
}
