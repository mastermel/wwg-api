import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Button,
  Checkbox,
  Divider,
  Group,
  MultiSelect,
  Select,
  SimpleGrid,
  Stack,
  Switch,
  Text,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { IconDice5, IconTrash } from "@tabler/icons-react";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import type { z } from "zod";
import { useListArmies } from "@/api/generated/endpoints/armies/armies";
import {
  getListHexDetailsQueryKey,
  useDeleteHexDetail,
  useRollHexDetail,
  useUpdateHexDetail,
} from "@/api/generated/endpoints/maps/maps";
import type { HexCellResponse, HexDetailResponse, HexFeatures } from "@/api/generated/model";
import { UpdateHexDetailBody } from "@/api/generated/zod/maps/maps.zod";
import { ConfirmModal } from "@/components/ConfirmModal";
import { Section } from "@/components/Section";
import type { Hex } from "@/features/maps/hex-grid";
import {
  describeDetail,
  dominantLabels,
  favorabilityLabels,
  featureLabels,
  reliefLabels,
} from "@/features/maps/hex-detail";
import { errorMessage } from "@/lib/errors";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

const live = { query: { meta: { persist: false } } } as const;

const options = <T extends string>(labels: Record<T, string>) =>
  (Object.keys(labels) as T[]).map((value) => ({ value, label: labels[value] }));

interface HexDetailSectionProps {
  campaignId: string;
  hex: Hex;
  /** The hex's map terrain, for the red die's modifier. */
  cell: HexCellResponse | undefined;
  detail: HexDetailResponse | undefined;
}

/**
 * A hex's actual terrain (decision 0016; the rules, p. 57): the Umpire shakes the three dice for
 * it when a player asks, changes what they find, and shows it to armies or to everyone.
 */
export function HexDetailSection({ campaignId, hex, cell, detail }: HexDetailSectionProps) {
  const armies = useListArmies(campaignId, live);
  const armyOptions = (armies.data ?? []).map((army) => ({ value: army.id, label: army.name }));

  return (
    <Section
      title="Actual terrain"
      description="What's really in the hex, for a battle: found by the dice when a player asks. Movement goes by the map's terrain."
    >
      <Stack gap="md">
        <RollForm
          campaignId={campaignId}
          hex={hex}
          cell={cell}
          detail={detail}
          armies={armyOptions}
        />
        {detail && (
          <>
            <Divider />
            <DetailForm
              // A new form for each result, so it shows what the dice found.
              key={JSON.stringify(detail)}
              campaignId={campaignId}
              hex={hex}
              detail={detail}
              armies={armyOptions}
            />
          </>
        )}
      </Stack>
    </Section>
  );
}

type ArmyOptions = { value: string; label: string }[];

function RollForm({
  campaignId,
  hex,
  cell,
  detail,
  armies,
}: HexDetailSectionProps & { armies: ArmyOptions }) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const roll = useRollHexDetail();
  const [forArmyId, setForArmyId] = useState<string | null>(detail?.forArmyId ?? null);
  const [favorability, setFavorability] = useState(false);
  const [minusOne, setMinusOne] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  // The red die's modifier comes from the map terrain; only a flat hex can take one off.
  const flat = (cell?.terrain ?? "Flat") === "Flat" && !cell?.forest;

  const shake = async () => {
    setFailure(null);
    try {
      const found = await roll.mutateAsync({
        id: campaignId,
        q: hex.q,
        r: hex.r,
        data: { forArmyId, favorability, flatMinusOne: flat && minusOne },
      });
      notifications.show({ color: "green", message: `The dice found: ${describeDetail(found)}` });
      await queryClient.invalidateQueries({ queryKey: getListHexDetailsQueryKey(campaignId) });
    } catch (error) {
      setFailure(errorMessage(error, "The dice couldn't be rolled. Try again."));
    }
  };

  return (
    <Stack gap="sm">
      {failure && (
        <Alert color="red" role="alert">
          {failure}
        </Alert>
      )}
      <Select
        label="Asked by"
        placeholder="No army"
        data={armies}
        value={forArmyId}
        onChange={setForArmyId}
        clearable
        comboboxProps={{ withinPortal: false }}
      />
      <Switch
        label="Roll favourability"
        checked={favorability}
        onChange={(event) => {
          setFavorability(event.currentTarget.checked);
        }}
      />
      {flat && (
        <Switch
          label="One off the red die"
          checked={minusOne}
          onChange={(event) => {
            setMinusOne(event.currentTarget.checked);
          }}
        />
      )}
      <Text size="xs" c="dimmed">
        Favourability is only for when both sides come onto the field together. The red die gets +1
        for low hills or forest, +2 for high hills or mountains.
      </Text>
      <Group>
        <Button
          variant="light"
          leftSection={<IconDice5 size={16} aria-hidden />}
          onClick={() => void shake()}
          loading={roll.isPending}
          disabled={!online}
        >
          {detail ? "Roll again" : "Roll the dice"}
        </Button>
      </Group>
    </Stack>
  );
}

type DetailValues = z.infer<typeof UpdateHexDetailBody>;

function DetailForm({
  campaignId,
  hex,
  detail,
  armies,
}: {
  campaignId: string;
  hex: Hex;
  detail: HexDetailResponse;
  armies: ArmyOptions;
}) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const update = useUpdateHexDetail();
  const remove = useDeleteHexDetail();
  const [forgetting, forget] = useDisclosure(false);
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<DetailValues>({
    resolver: zodResolver(UpdateHexDetailBody),
    defaultValues: {
      relief: detail.relief,
      features: detail.features,
      dominant: detail.dominant,
      favorability: detail.favorability,
      forArmyId: detail.forArmyId ?? null,
      shownToArmyIds: [...detail.shownToArmyIds],
      shownToAll: detail.shownToAll,
    },
  });
  const { isSubmitting } = form.formState;
  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: getListHexDetailsQueryKey(campaignId) });

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await update.mutateAsync({ id: campaignId, q: hex.q, r: hex.r, data: values });
      notifications.show({ color: "green", message: "Saved the actual terrain." });
      await refresh();
    } catch (error) {
      setFormError(
        applyServerErrors(error, form.setError, ["relief", "features", "shownToArmyIds"]),
      );
    }
  });

  const confirmForget = async () => {
    try {
      await remove.mutateAsync({ id: campaignId, q: hex.q, r: hex.r });
      notifications.show({ color: "green", message: "Forgot the actual terrain." });
      await refresh();
    } catch (error) {
      notifications.show({
        color: "red",
        message: errorMessage(error, "It couldn't be forgotten. Try again."),
      });
    }
    forget.close();
  };

  const featureKeys = Object.keys(featureLabels) as (keyof HexFeatures)[];

  return (
    <form onSubmit={(event) => void submit(event)} noValidate>
      <Stack gap="sm">
        {formError && (
          <Alert color="red" role="alert">
            {formError}
          </Alert>
        )}
        <Text size="sm" fw={500}>
          {describeDetail(detail)}
        </Text>
        <Text size="xs" c="dimmed">
          {detail.dice
            ? `Red ${String(detail.dice.red)}, white ${String(detail.dice.white)}${typeof detail.dice.green === "number" ? `, green ${String(detail.dice.green)}` : ""}.`
            : "Set by you, without dice."}
        </Text>
        <Controller
          control={form.control}
          name="relief"
          render={({ field }) => (
            <Select
              label="Lie of the land"
              data={options(reliefLabels)}
              value={field.value}
              onChange={(value) => {
                if (value) field.onChange(value);
              }}
              allowDeselect={false}
              comboboxProps={{ withinPortal: false }}
            />
          )}
        />
        <Controller
          control={form.control}
          name="features"
          render={({ field }) => (
            <Checkbox.Group
              label="What's there"
              value={featureKeys.filter((key) => field.value[key])}
              onChange={(chosen) => {
                field.onChange(
                  Object.fromEntries(featureKeys.map((key) => [key, chosen.includes(key)])),
                );
              }}
            >
              <SimpleGrid cols={2} spacing={6} mt={6}>
                {featureKeys.map((key) => (
                  <Checkbox
                    key={key}
                    value={key}
                    label={featureLabels[key].replace(/^./, (c) => c.toUpperCase())}
                  />
                ))}
              </SimpleGrid>
            </Checkbox.Group>
          )}
        />
        <Controller
          control={form.control}
          name="dominant"
          render={({ field }) => (
            <Select
              label="Dominant feature"
              data={options(dominantLabels)}
              value={field.value}
              onChange={(value) => {
                if (value) field.onChange(value);
              }}
              allowDeselect={false}
              comboboxProps={{ withinPortal: false }}
            />
          )}
        />
        <Controller
          control={form.control}
          name="favorability"
          render={({ field }) => (
            <Select
              label="Favourability"
              data={options(favorabilityLabels)}
              value={field.value}
              onChange={(value) => {
                if (value) field.onChange(value);
              }}
              allowDeselect={false}
              comboboxProps={{ withinPortal: false }}
            />
          )}
        />
        <Controller
          control={form.control}
          name="forArmyId"
          render={({ field }) => (
            <Select
              label="Asked by"
              placeholder="No army"
              data={armies}
              value={field.value ?? null}
              onChange={field.onChange}
              clearable
              comboboxProps={{ withinPortal: false }}
            />
          )}
        />
        <Controller
          control={form.control}
          name="shownToArmyIds"
          render={({ field }) => (
            <MultiSelect
              label="Shown to"
              description="Their commanders see it on the map."
              placeholder={field.value.length ? undefined : "No army yet"}
              data={armies}
              value={field.value}
              onChange={field.onChange}
              comboboxProps={{ withinPortal: false }}
            />
          )}
        />
        <Controller
          control={form.control}
          name="shownToAll"
          render={({ field }) => (
            <Switch
              label="Show to everyone"
              checked={field.value}
              onChange={(event) => {
                field.onChange(event.currentTarget.checked);
              }}
            />
          )}
        />
        <Group justify="space-between">
          <Button
            variant="subtle"
            color="red"
            leftSection={<IconTrash size={16} aria-hidden />}
            onClick={forget.open}
            disabled={!online}
          >
            Forget
          </Button>
          <Button type="submit" loading={isSubmitting} disabled={!online}>
            Save actual terrain
          </Button>
        </Group>
      </Stack>
      <ConfirmModal
        opened={forgetting}
        onClose={forget.close}
        title="Forget the actual terrain?"
        confirmLabel="Forget"
        onConfirm={() => void confirmForget()}
        loading={remove.isPending}
      >
        What the dice found here is forgotten, and no one sees it any more.
      </ConfirmModal>
    </form>
  );
}
