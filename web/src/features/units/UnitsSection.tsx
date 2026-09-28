import { zodResolver } from "@hookform/resolvers/zod";
import { ActionIcon, Button, Group, List, Stack, Text, TextInput, Title } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { IconEdit, IconPlus, IconTrash } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { getGetArmyQueryKey } from "@/api/generated/endpoints/armies/armies";
import { useCreateUnit, useDeleteUnit, useRenameUnit } from "@/api/generated/endpoints/units/units";
import type { ArmyResponse, UnitResponse } from "@/api/generated/model";
import { CreateUnitBody } from "@/api/generated/zod/units/units.zod";
import { ConfirmModal } from "@/components/ConfirmModal";
import { RenameUnitModal } from "@/features/units/RenameUnitModal";
import { applyServerErrors } from "@/lib/form-errors";
import { useConfirmTarget } from "@/lib/use-confirm-target";
import { useOnline } from "@/lib/use-online";

/**
 * The army's units. Only its commander, the Umpire and Admins get this far (the army page is
 * theirs); the Umpire (or an Admin) can add, rename and delete them.
 */
export function UnitsSection({ army, manager }: { army: ArmyResponse; manager: boolean }) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const rename = useRenameUnit();
  const remove = useDeleteUnit();
  const [renaming, setRenaming] = useState<UnitResponse | null>(null);
  const deleting = useConfirmTarget<UnitResponse>();
  const refresh = () => queryClient.invalidateQueries({ queryKey: getGetArmyQueryKey(army.id) });

  const confirmDelete = async (unit: UnitResponse) => {
    try {
      await remove.mutateAsync({ id: unit.id });
      notifications.show({ color: "green", message: `Deleted ${unit.name}.` });
      await refresh();
    } catch {
      notifications.show({ color: "red", message: "The unit couldn't be deleted. Try again." });
    }
    deleting.close();
  };

  return (
    <Stack gap="sm" component="section" aria-labelledby="units-heading">
      <Title order={2} size="h3" id="units-heading">
        Units
      </Title>
      {army.units.length === 0 ? (
        <Text c="dimmed">No units yet.</Text>
      ) : (
        <List listStyleType="none" spacing={4} p={0}>
          {army.units.map((unit) => (
            <List.Item key={unit.id}>
              <Group gap="xs" wrap="nowrap">
                <Text>{unit.name}</Text>
                {manager && (
                  <>
                    <ActionIcon
                      variant="subtle"
                      aria-label={`Rename ${unit.name}`}
                      onClick={() => {
                        setRenaming(unit);
                      }}
                      disabled={!online}
                    >
                      <IconEdit size={16} aria-hidden />
                    </ActionIcon>
                    <ActionIcon
                      variant="subtle"
                      color="red"
                      aria-label={`Delete ${unit.name}`}
                      onClick={() => {
                        deleting.open(unit);
                      }}
                      disabled={!online}
                    >
                      <IconTrash size={16} aria-hidden />
                    </ActionIcon>
                  </>
                )}
              </Group>
            </List.Item>
          ))}
        </List>
      )}
      {manager && <AddUnitForm army={army} onAdded={refresh} />}
      {renaming && (
        <RenameUnitModal
          name={renaming.name}
          onClose={() => {
            setRenaming(null);
          }}
          onSubmit={async ({ name }) => {
            await rename.mutateAsync({ id: renaming.id, data: { name } });
            await refresh();
          }}
        />
      )}
      <ConfirmModal
        opened={deleting.opened}
        onClose={deleting.close}
        title="Delete this unit?"
        confirmLabel="Delete unit"
        onConfirm={() => {
          if (deleting.target) void confirmDelete(deleting.target);
        }}
        loading={remove.isPending}
      >
        {deleting.target?.name} will be removed from {army.name}. This can&apos;t be undone.
      </ConfirmModal>
    </Stack>
  );
}

type NewUnitValues = z.infer<typeof CreateUnitBody>;

function AddUnitForm({ army, onAdded }: { army: ArmyResponse; onAdded: () => Promise<void> }) {
  const online = useOnline();
  const create = useCreateUnit();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<NewUnitValues>({
    resolver: zodResolver(CreateUnitBody),
    defaultValues: { name: "" },
  });
  const { errors, isSubmitting } = form.formState;

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await create.mutateAsync({ id: army.id, data: values });
      form.reset();
      await onAdded();
    } catch (error) {
      setFormError(applyServerErrors(error, form.setError, ["name"]));
    }
  });

  return (
    <form onSubmit={(event) => void submit(event)} noValidate>
      <Group align="flex-start">
        <TextInput
          label="New unit"
          placeholder="e.g. Light Division"
          error={errors.name?.message ?? formError}
          w={{ base: "100%", xs: 280 }}
          {...form.register("name", { setValueAs: (value: string) => value.trim() })}
        />
        <Button
          type="submit"
          variant="default"
          leftSection={<IconPlus size={16} aria-hidden />}
          loading={isSubmitting}
          disabled={!online}
          mt={{ base: 0, xs: 25 }}
        >
          Add unit
        </Button>
      </Group>
    </form>
  );
}
