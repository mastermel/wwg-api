import { Box, Paper, Stack, Text, Title } from "@mantine/core";
import { Outlet } from "@tanstack/react-router";
import { BrandMark } from "@/components/BrandMark";
import { OfflineBanner } from "@/components/OfflineBanner";
import { CompactPageContext } from "@/components/page-context";
import classes from "@/components/PublicLayout.module.css";

/** Sign-in, register and join links: the app's mark and name on a navy band, the page in a card. */
export function PublicLayout() {
  return (
    <Box className={classes.root} px="md" py="xl">
      <Stack w="100%" maw={440} mx="auto" align="stretch" gap="lg">
        <Stack align="center" gap={4} className={classes.brand} pt="md">
          <BrandMark size={96} />
          <Title order={2} component="p" c="inherit">
            WWG Campaigner
          </Title>
          <Text size="sm" className={classes.tagline}>
            Campaigns for the Wasatch Wargamers
          </Text>
        </Stack>
        <OfflineBanner />
        <Paper withBorder shadow="md" p="xl" radius="lg">
          <main>
            <CompactPageContext.Provider value={true}>
              <Outlet />
            </CompactPageContext.Provider>
          </main>
        </Paper>
      </Stack>
    </Box>
  );
}
