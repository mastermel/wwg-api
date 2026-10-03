import type { ReactNode } from "react";
import { Button, Group, Modal, Stack, Text, Textarea } from "@mantine/core";

const noteLimit = 1000;

interface SendReportModalProps {
  children: ReactNode;
  note: string;
  setNote: (note: string) => void;
  opened: boolean;
  loading: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

/**
 * Submitting the draft turn to the umpire, possibly including a note. This action is not
 * reversable by the player themselves. However the umpire could send the turn back or modify
 * it themselves..
 */
export function SubmitTurnModal({
  children,
  loading,
  opened,
  note,
  setNote,
  onClose,
}: SendReportModalProps) {
  return (
    <Modal opened={opened} onClose={onClose} title="Submit this turn?" centered>
      <Stack gap="md">
        <Text size="sm">{children}</Text>
        <Textarea
          label="Note"
          autosize
          minRows={3}
          maxLength={noteLimit}
          description={`${String(note.length)} of ${String(noteLimit)} characters.`}
          value={note}
          onChange={(event) => {
            setNote(event.currentTarget.value);
          }}
        />
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" loading={loading}>
            Submit
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
