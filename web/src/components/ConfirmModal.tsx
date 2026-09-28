import { Button, Group, Modal, Text } from "@mantine/core";
import type { ReactNode } from "react";

interface ConfirmModalProps {
  opened: boolean;
  onClose: () => void;
  title: string;
  /** What will happen, in a sentence or two. */
  children: ReactNode;
  confirmLabel: string;
  onConfirm: () => void;
  loading?: boolean;
  /** red for anything that deletes or removes. */
  color?: string;
}

/** Asks before an action that can't be undone (§6: deletes ask for confirmation). */
export function ConfirmModal({
  opened,
  onClose,
  title,
  children,
  confirmLabel,
  onConfirm,
  loading = false,
  color = "red",
}: ConfirmModalProps) {
  return (
    <Modal opened={opened} onClose={onClose} title={title} centered>
      <Text size="sm">{children}</Text>
      <Group justify="flex-end" mt="lg">
        <Button variant="default" onClick={onClose}>
          Cancel
        </Button>
        <Button color={color} loading={loading} onClick={onConfirm}>
          {confirmLabel}
        </Button>
      </Group>
    </Modal>
  );
}
