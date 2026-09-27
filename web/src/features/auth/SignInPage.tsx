import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, PasswordInput, Stack, Text, TextInput } from "@mantine/core";
import { useRouter } from "@tanstack/react-router";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { useLogin } from "@/api/generated/endpoints/auth/auth";
import { LoginBody } from "@/api/generated/zod/auth/auth.zod";
import { Page } from "@/components/Page";
import { useSession, useSessionStore } from "@/features/auth/session-context";
import { applyServerErrors } from "@/lib/form-errors";
import { safeRedirect } from "@/lib/safe-redirect";
import { useOnline } from "@/lib/use-online";

type SignInValues = z.infer<typeof LoginBody>;

const trim = (value: string) => value.trim();

export function SignInPage({ redirect }: { redirect?: string | undefined }) {
  const router = useRouter();
  const session = useSessionStore();
  const { ended } = useSession();
  const online = useOnline();
  const login = useLogin();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<SignInValues>({
    resolver: zodResolver(LoginBody),
    defaultValues: { email: "", password: "" },
  });
  const { errors, isSubmitting } = form.formState;

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await session.signIn(await login.mutateAsync({ data: values }));
      await router.navigate({ href: safeRedirect(redirect) });
    } catch (error) {
      setFormError(applyServerErrors(error, form.setError, ["email", "password"]));
    }
  });

  return (
    <Page title="Sign in">
      {ended && !formError && (
        <Alert color="yellow" title="You've been signed out">
          Your session ended, for example after a password change. Sign in again.
        </Alert>
      )}
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
            autoComplete="username"
            required
            error={errors.email?.message}
            {...form.register("email", { setValueAs: trim })}
          />
          <PasswordInput
            label="Password"
            autoComplete="current-password"
            required
            error={errors.password?.message}
            {...form.register("password")}
          />
          <Button type="submit" loading={isSubmitting} disabled={!online}>
            Sign in
          </Button>
          {!online && (
            <Text size="sm" c="dimmed">
              Signing in needs a connection.
            </Text>
          )}
        </Stack>
      </form>
    </Page>
  );
}
