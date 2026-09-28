import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Group, PasswordInput, Stack, TextInput } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { useChangeEmail } from "@/api/generated/endpoints/account/account";
import { ChangeEmailBody } from "@/api/generated/zod/account/account.zod";
import { AccountSection } from "@/features/account/AccountSection";
import { useSessionStore } from "@/features/auth/session-context";
import { ApiError } from "@/lib/api-fetch";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

type EmailValues = z.infer<typeof ChangeEmailBody>;

export function EmailForm({ currentEmail }: { currentEmail: string }) {
  const session = useSessionStore();
  const online = useOnline();
  const change = useChangeEmail();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<EmailValues>({
    resolver: zodResolver(ChangeEmailBody),
    defaultValues: { newEmail: "", currentPassword: "" },
  });
  const { errors, isSubmitting } = form.formState;

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await session.signIn(await change.mutateAsync({ data: values }));
      form.reset();
      notifications.show({
        color: "green",
        message: `You'll sign in with ${values.newEmail} from now on. Other devices were signed out.`,
      });
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        form.setError("newEmail", { message: "An account with this email already exists." });
        return;
      }
      setFormError(applyServerErrors(error, form.setError, ["newEmail", "currentPassword"]));
    }
  });

  return (
    <AccountSection
      title="Email"
      description={`You sign in with ${currentEmail}. Changing it signs out your other devices, and we'll tell your old address.`}
    >
      {formError && (
        <Alert color="red" role="alert">
          {formError}
        </Alert>
      )}
      <form onSubmit={(event) => void onSubmit(event)} noValidate>
        <Stack>
          <TextInput
            label="New email"
            type="email"
            autoComplete="email"
            required
            error={errors.newEmail?.message}
            {...form.register("newEmail", { setValueAs: (value: string) => value.trim() })}
          />
          <PasswordInput
            label="Current password"
            autoComplete="current-password"
            required
            error={errors.currentPassword?.message}
            {...form.register("currentPassword")}
          />
          <Group>
            <Button type="submit" loading={isSubmitting} disabled={!online}>
              Change email
            </Button>
          </Group>
        </Stack>
      </form>
    </AccountSection>
  );
}
