import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Group, Modal, Stack, Text, Textarea } from "@mantine/core";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import type { ArmySummary, UnitResponse } from "@/api/generated/model";
import { SendBackTurnBody } from "@/api/generated/zod/turns/turns.zod";
import { applyServerErrors } from "@/lib/form-errors";
import { useOnline } from "@/lib/use-online";

const unitNote = SendBackTurnBody.shape.unitNotes.unwrap().element.shape;

// Blank notes are left out when sending, so the form allows them.
const ReviewForm = z.object({
  note: SendBackTurnBody.shape.note.unwrap(),
  unitNotes: z.array(z.object({ unitId: unitNote.unitId, text: unitNote.text.or(z.literal("")) })),
});

type ReviewValues = z.infer<typeof ReviewForm>;

/** A unit note's field name (the lint rule wants the index as a string; the form, a number). */
const noteField = (index: number) =>
  `unitNotes.${String(index)}.text` as `unitNotes.${number}.text`;

interface ReviewModalProps {
  kind: "send-back" | "revert";
  army: ArmySummary;
  turn: number;
  /** The army's units, for notes on their orders. */
  units: readonly UnitResponse[];
  onSubmit: (values: {
    note: string | null;
    unitNotes: { unitId: string; text: string }[] | null;
  }) => Promise<void>;
  onClose: () => void;
}

/**
 * Sending back a Submitted turn, or reopening an approved one, with the Umpire's note on the turn
 * and on units' orders (DESIGN.md §3.13). Both optional. Mount it only while open.
 */
export function ReviewModal({ kind, army, turn, units, onSubmit, onClose }: ReviewModalProps) {
  const online = useOnline();
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<ReviewValues>({
    resolver: zodResolver(ReviewForm),
    defaultValues: { note: "", unitNotes: units.map((u) => ({ unitId: u.id, text: "" })) },
  });
  const { errors, isSubmitting } = form.formState;
  const sendBack = kind === "send-back";

  const submit = form.handleSubmit(async (values) => {
    setFormError(null);
    const unitNotes = values.unitNotes.filter((n) => n.text !== "");
    try {
      await onSubmit({
        note: values.note === "" ? null : values.note,
        unitNotes: unitNotes.length > 0 ? unitNotes : null,
      });
      onClose();
    } catch (error) {
      setFormError(applyServerErrors(error, form.setError, ["note"]));
    }
  });

  return (
    <Modal
      opened
      onClose={onClose}
      title={`${sendBack ? "Send back" : "Reopen"} ${army.name}'s turn ${String(turn)}`}
      centered
    >
      <form onSubmit={(event) => void submit(event)} noValidate>
        <Stack>
          {formError && (
            <Alert color="red" role="alert">
              {formError}
            </Alert>
          )}
          <Text size="sm">
            {sendBack
              ? "It goes back to its commander as a draft, to change and submit again."
              : "Its approval is undone: it goes back to its commander as a draft, to change and submit again."}
          </Text>
          <Textarea
            label="Note"
            description="Why, for the commander. Optional."
            autosize
            minRows={2}
            data-autofocus
            error={errors.note?.message}
            {...form.register("note", { setValueAs: (value: string) => value.trim() })}
          />
          {units.length > 0 && (
            <Stack gap="xs">
              <Text size="sm" fw={500}>
                Notes on units&apos; orders (optional)
              </Text>
              {units.map((unit, index) => (
                <Textarea
                  key={unit.id}
                  label={unit.name}
                  autosize
                  minRows={1}
                  error={errors.unitNotes?.[index]?.text?.message}
                  {...form.register(noteField(index), {
                    setValueAs: (value: string) => value.trim(),
                  })}
                />
              ))}
            </Stack>
          )}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={isSubmitting} disabled={!online}>
              {sendBack ? "Send back" : "Reopen"}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
