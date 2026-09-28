import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Anchor, Button, Group, PasswordInput, Stack, Text, TextInput } from "@mantine/core";
import { Link, useRouter } from "@tanstack/react-router";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { useRegister } from "@/api/generated/endpoints/auth/auth";
import { RegisterBody } from "@/api/generated/zod/auth/auth.zod";
import { Page } from "@/components/Page";
import { useSessionStore } from "@/features/auth/session-context";
import { ApiError } from "@/lib/api-fetch";
import { applyServerErrors } from "@/lib/form-errors";
import { safeRedirect } from "@/lib/safe-redirect";
import { useOnline } from "@/lib/use-online";

type RegisterValues = z.infer<typeof RegisterBody>;

const fields = ["email", "password", "firstName", "lastName"] as const;
const trim = (value: string) => value.trim();

export function RegisterPage({ redirect }: { redirect?: string | undefined }) {
  const router = useRouter();
  const session = useSessionStore();
  const online = useOnline();
  const register = useRegister();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<RegisterValues>({
    resolver: zodResolver(RegisterBody),
    defaultValues: { email: "", password: "", firstName: "", lastName: "" },
  });
  const { errors, isSubmitting } = form.formState;

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await session.signIn(await register.mutateAsync({ data: values }));
      await router.navigate({ href: safeRedirect(redirect) });
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        form.setError("email", { message: "An account with this email already exists." });
        return;
      }
      setFormError(applyServerErrors(error, form.setError, fields));
    }
  });

  return (
    <Page title="Create an account">
      {formError && (
        <Alert color="red" role="alert">
          {formError}
        </Alert>
      )}
      <form onSubmit={(event) => void onSubmit(event)} noValidate>
        <Stack>
          <Group grow align="flex-start">
            <TextInput
              label="First name"
              autoComplete="given-name"
              required
              error={errors.firstName?.message}
              {...form.register("firstName", { setValueAs: trim })}
            />
            <TextInput
              label="Last name"
              autoComplete="family-name"
              required
              error={errors.lastName?.message}
              {...form.register("lastName", { setValueAs: trim })}
            />
          </Group>
          <TextInput
            label="Email"
            type="email"
            autoComplete="email"
            required
            error={errors.email?.message}
            {...form.register("email", { setValueAs: trim })}
          />
          <PasswordInput
            label="Password"
            description="At least 8 characters."
            autoComplete="new-password"
            required
            error={errors.password?.message}
            {...form.register("password")}
          />
          <Button type="submit" loading={isSubmitting} disabled={!online}>
            Create account
          </Button>
          {!online && (
            <Text size="sm" c="dimmed">
              Creating an account needs a connection.
            </Text>
          )}
        </Stack>
      </form>
      <Text size="sm">
        Already have an account?{" "}
        {/* Underlined: a link inside a sentence can't differ by colour alone (WCAG 1.4.1). */}
        <Anchor
          underline="always"
          renderRoot={(props) => <Link to="/sign-in" search={{ redirect }} {...props} />}
        >
          Sign in
        </Anchor>
      </Text>
    </Page>
  );
}
