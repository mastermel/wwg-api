import { Stack } from "@mantine/core";
import { Page } from "@/components/Page";
import { EmailForm } from "@/features/account/EmailForm";
import { EmailSettings } from "@/features/account/EmailSettings";
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
    <Page title="Account" summary={`Signed in as ${user.email}`}>
      <Stack maw={720} gap="xl">
        {/* Keyed so the form's defaults follow the saved name (it can change on another device). */}
        <ProfileForm key={`${user.id} ${user.firstName} ${user.lastName}`} user={user} />
        <EmailForm currentEmail={user.email} />
        <PasswordForm />
        <EmailSettings />
        <SignOutEverywhere />
      </Stack>
    </Page>
  );
}
