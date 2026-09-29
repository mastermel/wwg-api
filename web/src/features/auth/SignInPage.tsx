import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Anchor, Button, PasswordInput, Stack, Text, TextInput } from "@mantine/core";
import { Link, useRouter } from "@tanstack/react-router";
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

export function SignInPage({
  redirect,
  passwordReset = false,
}: {
  redirect?: string | undefined;
  passwordReset?: boolean;
}) {
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
      {passwordReset && !formError && (
        <Alert role="status" color="green" title="Password changed">
          Sign in with your new password. You&apos;ve been signed out everywhere else.
        </Alert>
      )}
      {ended && !passwordReset && !formError && (
        <Alert role="status" color="yellow" title="You've been signed out">
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
          <Anchor
            size="sm"
            ta="center"
            renderRoot={(props) => <Link to="/forgot-password" {...props} />}
          >
            Forgot your password?
          </Anchor>
          {!online && (
            <Text size="sm" c="dimmed">
              Signing in needs a connection.
            </Text>
          )}
        </Stack>
      </form>
      <Text size="sm">
        New here?{" "}
        {/* Underlined: a link inside a sentence can't differ by colour alone (WCAG 1.4.1). */}
        <Anchor
          underline="always"
          renderRoot={(props) => <Link to="/register" search={{ redirect }} {...props} />}
        >
          Create an account
        </Anchor>
      </Text>
    </Page>
  );
}
