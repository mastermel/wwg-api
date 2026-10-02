import {
  AppShell,
  Box,
  Button,
  Divider,
  Group,
  Menu,
  NavLink,
  Text,
  UnstyledButton,
} from "@mantine/core";
import {
  IconBooks,
  IconChevronDown,
  IconInfoCircle,
  IconLogout,
  IconMasksTheater,
  IconMap,
  IconSwords,
  IconUser,
  IconUsers,
  type Icon,
} from "@tabler/icons-react";
import { Link, Outlet, useMatchRoute } from "@tanstack/react-router";
import { Fragment } from "react";
import classes from "@/components/AppLayout.module.css";
import { BrandMark } from "@/components/BrandMark";
import { InstallHint } from "@/components/InstallHint";
import { OfflineBanner } from "@/components/OfflineBanner";
import { ThemeMenuItems } from "@/components/ThemeMenuItems";
import { useMasqueradeSession } from "@/features/admin/use-masquerade-session";
import { ConfirmEmailReminder } from "@/features/auth/ConfirmEmailReminder";
import { useSession, useSessionStore } from "@/features/auth/session-context";
import { formatDateTime } from "@/lib/format";

interface NavItem {
  to: "/campaigns" | "/library" | "/admin/campaigns" | "/admin/users" | "/about";
  label: string;
  icon: Icon;
}

const campaigns: NavItem = { to: "/campaigns", label: "Campaigns", icon: IconSwords };
const library: NavItem = { to: "/library", label: "Library", icon: IconBooks };
const allCampaigns: NavItem = { to: "/admin/campaigns", label: "All campaigns", icon: IconMap };
const users: NavItem = { to: "/admin/users", label: "Users", icon: IconUsers };
const about: NavItem = { to: "/about", label: "About", icon: IconInfoCircle };

const memberNavItems = [campaigns, library, about];
// Admins also get the admin screens. (The API enforces access regardless.)
const adminNavItems = [campaigns, library, allCampaigns, users, about];
const adminOnly = new Set<NavItem>([allCampaigns, users]);

/**
 * The app frame: a header, a sidebar on desktop, and a bottom tab bar on phones (below Mantine's
 * `sm` breakpoint). Both navigations use the same items; CSS shows one or the other, and they
 * have distinct labels so they're never two identical landmarks.
 */
export function AppLayout() {
  const matchRoute = useMatchRoute();
  const session = useSessionStore();
  const { user } = useSession();
  const masquerade = useMasqueradeSession();
  const navItems = user?.isAdmin ? adminNavItems : memberNavItems;
  const isActive = (to: NavItem["to"]) => Boolean(matchRoute({ to, fuzzy: true }));

  return (
    <AppShell
      header={{ height: 60 }}
      navbar={{ width: 240, breakpoint: "sm", collapsed: { mobile: true } }}
      footer={{ height: { base: 64, sm: 0 } }}
      padding="lg"
    >
      <AppShell.Header px="lg" className={classes.header}>
        <Group h="100%" justify="space-between" wrap="nowrap">
          <UnstyledButton
            component={Link}
            to="/"
            aria-label="Wasatch Wargamers, start page"
            className={classes.brand}
          >
            <Group gap="xs">
              <BrandMark size={40} />
              <Text fw={700} size="lg">
                Wasatch Wargamers
              </Text>
            </Group>
          </UnstyledButton>
          {user && (
            <Menu position="bottom-end">
              <Menu.Target>
                <Button
                  variant="subtle"
                  className={classes.userButton}
                  data-masquerade={user.masquerade ? true : undefined}
                  leftSection={user.masquerade && <IconMasksTheater size={16} aria-hidden />}
                  rightSection={<IconChevronDown size={16} aria-hidden />}
                >
                  {user.masquerade ? `${user.firstName} (masquerade)` : user.firstName}
                </Button>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Label>
                  {user.firstName} {user.lastName}
                </Menu.Label>
                {user.masquerade && (
                  <>
                    <Text size="xs" c="dimmed" px="sm" pb={6} maw={260}>
                      {user.masquerade.adminName} is masquerading as {user.firstName}, until{" "}
                      {formatDateTime(user.masquerade.endsAt)}.
                    </Text>
                    <Menu.Item
                      leftSection={<IconMasksTheater size={16} aria-hidden />}
                      onClick={() => void masquerade.end()}
                    >
                      End masquerade
                    </Menu.Item>
                    <Menu.Divider />
                  </>
                )}
                <Menu.Item
                  leftSection={<IconUser size={16} aria-hidden />}
                  renderRoot={(props) => <Link to="/account" {...props} />}
                >
                  Account
                </Menu.Item>
                <Menu.Divider />
                <ThemeMenuItems />
                <Menu.Divider />
                <Menu.Item
                  leftSection={<IconLogout size={16} aria-hidden />}
                  onClick={() => void session.signOut()}
                >
                  Sign out
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
          )}
        </Group>
      </AppShell.Header>

      {/* Hidden below sm, not just slid away: otherwise its links stay in the tab order and the
          accessibility tree on phones, off-screen, next to the tab bar's. */}
      <AppShell.Navbar p="sm" aria-label="Main" visibleFrom="sm" className={classes.navbar}>
        {navItems.map((item, index) => (
          <Fragment key={item.to}>
            {/* Admin screens under their own label (a visual grouping, not a heading). */}
            {adminOnly.has(item) && !adminOnly.has(navItems[index - 1] ?? item) && (
              <Text className={classes.navLabel} aria-hidden>
                Admin
              </Text>
            )}
            {!adminOnly.has(item) && adminOnly.has(navItems[index - 1] ?? item) && (
              <Divider my="sm" />
            )}
            <NavLink
              component={Link}
              to={item.to}
              label={item.label}
              leftSection={<item.icon size={20} aria-hidden />}
              active={isActive(item.to)}
              aria-current={isActive(item.to) ? "page" : undefined}
              className={classes.navLink}
            />
          </Fragment>
        ))}
      </AppShell.Navbar>

      <AppShell.Main className={classes.main}>
        <Box className={classes.content}>
          <OfflineBanner />
          <ConfirmEmailReminder />
          <InstallHint />
          <Outlet />
        </Box>
      </AppShell.Main>

      <AppShell.Footer
        hiddenFrom="sm"
        component="nav"
        aria-label="Main tabs"
        className={classes.footer}
      >
        <Group grow h="100%" gap={0}>
          {navItems.map((item) => (
            <UnstyledButton
              key={item.to}
              component={Link}
              to={item.to}
              aria-current={isActive(item.to) ? "page" : undefined}
              // The anchor colour meets AA in both schemes; the filled primary is too dark on dark.
              className={classes.tab}
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
