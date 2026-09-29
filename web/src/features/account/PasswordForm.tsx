import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Group, PasswordInput, Stack } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useChangePassword } from "@/api/generated/endpoints/account/account";
import { ChangePasswordBody } from "@/api/generated/zod/account/account.zod";
import { Section } from "@/components/Section";
import { useSessionStore } from "@/features/auth/session-context";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

const PasswordFormSchema = ChangePasswordBody.extend({ confirmPassword: z.string() }).refine(
  (values) => values.newPassword === values.confirmPassword,
  { message: "The passwords don't match.", path: ["confirmPassword"] },
);

type PasswordValues = z.infer<typeof PasswordFormSchema>;

export function PasswordForm() {
  const session = useSessionStore();
  const online = useOnline();
  const change = useChangePassword();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<PasswordValues>({
    resolver: zodResolver(PasswordFormSchema),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  });
  const { errors, isSubmitting } = form.formState;

  const onSubmit = form.handleSubmit(async ({ currentPassword, newPassword }) => {
    setFormError(null);
    try {
      await session.signIn(await change.mutateAsync({ data: { currentPassword, newPassword } }));
      form.reset();
      notifications.show({
        color: "green",
        message: "Password changed. Your other devices were signed out.",
      });
    } catch (error) {
      setFormError(applyServerErrors(error, form.setError, ["currentPassword", "newPassword"]));
    }
  });

  return (
    <Section title="Password" description="Changing it signs out your other devices.">
      {formError && (
        <Alert color="red" role="alert">
          {formError}
        </Alert>
      )}
      <form onSubmit={(event) => void onSubmit(event)} noValidate>
        <Stack>
          <PasswordInput
            label="Current password"
            autoComplete="current-password"
            required
            error={errors.currentPassword?.message}
            {...form.register("currentPassword")}
          />
          <PasswordInput
            label="New password"
            description="At least 8 characters."
            autoComplete="new-password"
            required
            error={errors.newPassword?.message}
            {...form.register("newPassword")}
          />
          <PasswordInput
            label="Confirm new password"
            autoComplete="new-password"
            required
            error={errors.confirmPassword?.message}
            {...form.register("confirmPassword")}
          />
          <Group>
            <Button type="submit" loading={isSubmitting} disabled={!online}>
              Change password
            </Button>
          </Group>
        </Stack>
      </form>
    </Section>
  );
}
