import { Center, Paper, Stack, Title } from "@mantine/core";
import { Outlet } from "@tanstack/react-router";
import { BrandMark } from "@/components/BrandMark";
import { OfflineBanner } from "@/components/OfflineBanner";

/** Sign-in and register: the app's mark and name above a card, centred. */
export function PublicLayout() {
  return (
    <Center mih="100dvh" p="md">
      <Stack w="100%" maw={420} align="stretch" gap="lg">
        <Stack align="center" gap="xs" c="var(--mantine-color-anchor)">
          <BrandMark size={120} />
          <Title order={2} component="p" c="var(--mantine-color-text)">
            WWG Campaigner
          </Title>
        </Stack>
        <OfflineBanner />
        <Paper withBorder p="lg" radius="md">
          <main>
            <Outlet />
          </main>
        </Paper>
      </Stack>
    </Center>
  );
}
