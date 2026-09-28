import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Anchor, Button, Stack, Text, TextInput } from "@mantine/core";
import { Link } from "@tanstack/react-router";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { useForgotPassword } from "@/api/generated/endpoints/auth/auth";
import { ForgotPasswordBody } from "@/api/generated/zod/auth/auth.zod";
import { Page } from "@/components/Page";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

type ForgotValues = z.infer<typeof ForgotPasswordBody>;

export function ForgotPasswordPage() {
  const online = useOnline();
  const forgot = useForgotPassword();
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<ForgotValues>({
    resolver: zodResolver(ForgotPasswordBody),
    defaultValues: { email: "" },
  });
  const { errors, isSubmitting } = form.formState;

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await forgot.mutateAsync({ data: values });
      setSentTo(values.email);
    } catch (error) {
      setFormError(applyServerErrors(error, form.setError, ["email"]));
    }
  });

  return (
    <Page title="Reset your password">
      {sentTo ? (
        // The same message whether or not there's an account, like the API's answer.
        <Alert color="green" title="Check your email">
          If there&apos;s an account for {sentTo}, we&apos;ve sent it a link to choose a new
          password. The link works for 2 hours.
        </Alert>
      ) : (
        <>
          <Text size="sm">Enter your account&apos;s email and we&apos;ll send you a link.</Text>
          {formError && (
            <Alert color="red" role="alert">
              {formError}
            </Alert>
          )}
          <form onSubmit={(event) => void onSubmit(event)} noValidate>
            <Stack>
              <TextInput
                label="Email"
                type="email"
                autoComplete="email"
                required
                error={errors.email?.message}
                {...form.register("email", { setValueAs: (value: string) => value.trim() })}
              />
              <Button type="submit" loading={isSubmitting} disabled={!online}>
                Send reset link
              </Button>
            </Stack>
          </form>
        </>
      )}
      <Text size="sm">
        <Anchor renderRoot={(props) => <Link to="/sign-in" {...props} />}>Back to sign in</Anchor>
      </Text>
    </Page>
  );
}
