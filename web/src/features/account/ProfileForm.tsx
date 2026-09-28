import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Group, TextInput } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { useUpdateMe } from "@/api/generated/endpoints/account/account";
import type { MeResponse } from "@/api/generated/model";
import { UpdateMeBody } from "@/api/generated/zod/account/account.zod";
import { AccountSection } from "@/features/account/AccountSection";
import { useSessionStore } from "@/features/auth/session-context";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

type ProfileValues = z.infer<typeof UpdateMeBody>;
const trim = (value: string) => value.trim();

export function ProfileForm({ user }: { user: MeResponse }) {
  const session = useSessionStore();
  const online = useOnline();
  const update = useUpdateMe();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<ProfileValues>({
    resolver: zodResolver(UpdateMeBody),
    defaultValues: { firstName: user.firstName, lastName: user.lastName },
  });
  const { errors, isSubmitting, isDirty } = form.formState;

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      const updated = await update.mutateAsync({ data: values });
      session.setUser(updated);
      form.reset({ firstName: updated.firstName, lastName: updated.lastName });
      notifications.show({ color: "green", message: "Your name was saved." });
    } catch (error) {
      setFormError(applyServerErrors(error, form.setError, ["firstName", "lastName"]));
    }
  });

  return (
    <AccountSection title="Your name">
      {formError && (
        <Alert color="red" role="alert">
          {formError}
        </Alert>
      )}
      <form onSubmit={(event) => void onSubmit(event)} noValidate>
        <Group align="flex-end" grow>
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
        <Group mt="md">
          <Button type="submit" loading={isSubmitting} disabled={!online || !isDirty}>
            Save name
          </Button>
        </Group>
      </form>
    </AccountSection>
  );
}
