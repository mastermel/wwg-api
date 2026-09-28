import { Stack } from "@mantine/core";
import { Page } from "@/components/Page";
import { EmailForm } from "@/features/account/EmailForm";
import { PasswordForm } from "@/features/account/PasswordForm";
import { ProfileForm } from "@/features/account/ProfileForm";
import { SignOutEverywhere } from "@/features/account/SignOutEverywhere";
import { useSession } from "@/features/auth/session-context";

export function AccountPage() {
  const { user } = useSession();
  if (!user) {
    return null; // The _app guard only lets signed-in (or offline) users here.
  }

  return (
    <Page title="Account">
      <Stack maw={640} gap="lg">
        {/* Keyed so the form's defaults follow the saved name. */}
        <ProfileForm key={user.id} user={user} />
        <EmailForm currentEmail={user.email} />
        <PasswordForm />
        <SignOutEverywhere />
      </Stack>
    </Page>
  );
}
