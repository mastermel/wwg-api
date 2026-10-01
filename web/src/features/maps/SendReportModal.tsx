import { Alert, Button, Checkbox, Group, Modal, Select, Stack, Textarea } from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import {
  getListReportsQueryKey,
  useSendReport,
} from "@/api/generated/endpoints/intelligence/intelligence";
import type { ArmySummary } from "@/api/generated/model";
import { errorMessage } from "@/lib/errors";
import { useOnline } from "@/lib/use-online";

const noteLimit = 1000;

interface SendReportModalProps {
  campaignId: string;
  /** The armies the viewer sends from: a commander's own (the Umpire's, any). */
  from: readonly ArmySummary[];
  armies: readonly ArmySummary[];
  onClose: () => void;
}

/**
 * Sending an ally a report (step 49d, decision 0020): the army's units, its sightings so far, a
 * note; a courier carries it. Mount it only while open.
 */
export function SendReportModal({ campaignId, from, armies, onClose }: SendReportModalProps) {
  const online = useOnline();
  const queryClient = useQueryClient();
  const send = useSendReport();
  const [fromId, setFromId] = useState(from[0]?.id ?? "");
  const sender = armies.find((a) => a.id === fromId);
  const allies = armies.filter((a) => a.side.id === sender?.side.id && a.id !== sender.id);
  const [toId, setToId] = useState<string | null>(allies[0]?.id ?? null);
  const [snapshot, setSnapshot] = useState(true);
  const [sightings, setSightings] = useState(true);
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const empty = !snapshot && !sightings && note.trim() === "";

  const submit = async () => {
    if (!toId) return;
    setError(null);
    try {
      await send.mutateAsync({
        id: fromId,
        data: {
          toArmyId: toId,
          includesSnapshot: snapshot,
          includesSightings: sightings,
          note: note.trim() || null,
        },
      });
      notifications.show({
        color: "green",
        message: `Sent a report to ${armies.find((a) => a.id === toId)?.name ?? "the ally"} by courier.`,
      });
      await queryClient.invalidateQueries({ queryKey: getListReportsQueryKey(campaignId) });
      onClose();
    } catch (failed) {
      setError(errorMessage(failed, "The report couldn't be sent. Try again."));
    }
  };

  return (
    <Modal opened onClose={onClose} title="Send a report" centered>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
      >
        <Stack gap="md">
          {error && (
            <Alert color="red" role="alert">
              {error}
            </Alert>
          )}
          {from.length > 1 && (
            <Select
              label="From"
              data={from.map((a) => ({ value: a.id, label: a.name }))}
              value={fromId}
              allowDeselect={false}
              onChange={(value) => {
                if (!value) return;
                setFromId(value);
                setToId(null);
              }}
              comboboxProps={{ withinPortal: false }}
            />
          )}
          {allies.length === 0 ? (
            <Alert role="status" color="gray">
              Your side has no other army to send to.
            </Alert>
          ) : (
            <Select
              label="To"
              description="A courier rides it there: it takes a turn or more."
              data={allies.map((a) => ({ value: a.id, label: a.name }))}
              value={toId}
              allowDeselect={false}
              onChange={setToId}
              comboboxProps={{ withinPortal: false }}
            />
          )}
          <Checkbox
            label="Our units"
            description="Where each is now, and its points."
            checked={snapshot}
            onChange={(event) => {
              setSnapshot(event.currentTarget.checked);
            }}
          />
          <Checkbox
            label="Our sightings"
            description="Every sighting of the enemy received so far, on its turn."
            checked={sightings}
            onChange={(event) => {
              setSightings(event.currentTarget.checked);
            }}
          />
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
            <Button type="submit" loading={send.isPending} disabled={!online || !toId || empty}>
              Send by courier
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
