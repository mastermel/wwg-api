import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Anchor, Button, PasswordInput, Stack, Text } from "@mantine/core";
import { Link, useRouter } from "@tanstack/react-router";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useResetPassword } from "@/api/generated/endpoints/auth/auth";
import { ResetPasswordBody } from "@/api/generated/zod/auth/auth.zod";
import { Page } from "@/components/Page";
import { ApiError } from "@/lib/api-fetch";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

const ResetForm = z
  .object({
    newPassword: ResetPasswordBody.shape.newPassword,
    confirmPassword: z.string(),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: "The passwords don't match.",
    path: ["confirmPassword"],
  });

type ResetValues = z.infer<typeof ResetForm>;

function RequestNewLink() {
  return (
    <Anchor renderRoot={(props) => <Link to="/forgot-password" {...props} />}>
      Ask for a new link
    </Anchor>
  );
}

export function ResetPasswordPage({
  email,
  code,
}: {
  email?: string | undefined;
  code?: string | undefined;
}) {
  const router = useRouter();
  const online = useOnline();
  const reset = useResetPassword();
  const [linkInvalid, setLinkInvalid] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<ResetValues>({
    resolver: zodResolver(ResetForm),
    defaultValues: { newPassword: "", confirmPassword: "" },
  });
  const { errors, isSubmitting } = form.formState;

  if (!email || !code) {
    return (
      <Page title="Choose a new password">
        <Alert color="yellow" title="This link is incomplete">
          Open the link from the email again, or <RequestNewLink />.
        </Alert>
      </Page>
    );
  }

  const onSubmit = form.handleSubmit(async ({ newPassword }) => {
    setFormError(null);
    try {
      await reset.mutateAsync({ data: { email, code, newPassword } });
      await router.navigate({ to: "/sign-in", search: { reset: true } });
    } catch (error) {
      if (error instanceof ApiError && error.problem?.errors?.code) {
        setLinkInvalid(true);
        return;
      }
      setFormError(applyServerErrors(error, form.setError, ["newPassword"]));
    }
  });

  return (
    <Page title="Choose a new password">
      <Text size="sm">For {email}.</Text>
      {linkInvalid && (
        <Alert color="red" role="alert" title="This link has expired or was already used">
          <RequestNewLink />.
        </Alert>
      )}
      {formError && (
        <Alert color="red" role="alert">
          {formError}
        </Alert>
      )}
      <form onSubmit={(event) => void onSubmit(event)} noValidate>
        <Stack>
          {/* Lets password managers save the new password against the right account. */}
          <input type="email" autoComplete="username" value={email} readOnly hidden />
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
          <Button type="submit" loading={isSubmitting} disabled={!online || linkInvalid}>
            Change password
          </Button>
        </Stack>
      </form>
    </Page>
  );
}
