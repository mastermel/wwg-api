import { AppShell, Group, NavLink, Text, UnstyledButton } from "@mantine/core";
import { IconSwords, type Icon } from "@tabler/icons-react";
import { Link, Outlet, useMatchRoute } from "@tanstack/react-router";

interface NavItem {
  to: "/campaigns";
  label: string;
  icon: Icon;
}

const navItems: NavItem[] = [{ to: "/campaigns", label: "Campaigns", icon: IconSwords }];

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
          <Text component={Link} to="/" fw={700} size="lg" c="inherit" td="none">
            WWG Campaigner
          </Text>
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
              c={isActive(item.to) ? "var(--mantine-primary-color-filled)" : "dimmed"}
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
    </AppShell>
  );
}
