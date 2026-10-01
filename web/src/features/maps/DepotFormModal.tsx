import {
  Button,
  Group,
  Modal,
  SegmentedControl,
  Select,
  Stack,
  Text,
  TextInput,
} from "@mantine/core";
import { useState } from "react";
import type { ArmySummary, DepotKind } from "@/api/generated/model";

export interface DepotDraft {
  armyId: string;
  kind: DepotKind;
  name: string;
}

interface DepotFormModalProps {
  title: string;
  submitLabel: string;
  armies: readonly ArmySummary[];
  initial: DepotDraft;
  /** Whether the army can be chosen (a new depot), or is its own (an existing one). */
  chooseArmy: boolean;
  onSubmit: (draft: DepotDraft) => void;
  onClose: () => void;
}

/** A depot's army, kind and name (step 48a). Mount it only while open. */
export function DepotFormModal({
  title,
  submitLabel,
  armies,
  initial,
  chooseArmy,
  onSubmit,
  onClose,
}: DepotFormModalProps) {
  const [draft, setDraft] = useState(initial);
  return (
    <Modal opened onClose={onClose} title={title} centered>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit({ ...draft, name: draft.name.trim() });
        }}
      >
        <Stack gap="md">
          {chooseArmy && (
            <Select
              label="Army"
              description="Only its own units draw supply from it."
              data={armies.map((a) => ({ value: a.id, label: a.name }))}
              value={draft.armyId}
              allowDeselect={false}
              onChange={(value) => {
                if (value) setDraft({ ...draft, armyId: value });
              }}
              comboboxProps={{ withinPortal: false }}
            />
          )}
          <Stack gap={4}>
            <Text size="sm" fw={500} id="depot-kind">
              Kind
            </Text>
            <SegmentedControl
              aria-labelledby="depot-kind"
              data={[
                { value: "Main", label: "Main" },
                { value: "Intermediate", label: "Intermediate" },
              ]}
              value={draft.kind}
              onChange={(value) => {
                setDraft({ ...draft, kind: value });
              }}
              fullWidth
            />
            <Text size="xs" c="dimmed">
              {draft.kind === "Main"
                ? "Where supply comes from: it's never out of supply itself."
                : "Supplied by its own route from a main depot; stocked for 15 turns once cut off."}
            </Text>
          </Stack>
          <TextInput
            label="Name"
            description="Optional, e.g. the town it's in."
            maxLength={100}
            value={draft.name}
            onChange={(event) => {
              setDraft({ ...draft, name: event.currentTarget.value });
            }}
          />
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit">{submitLabel}</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
