import type { FieldValues, Path, UseFormSetError } from "react-hook-form";
import { ApiError } from "@/lib/api-fetch";
import { errorMessage } from "@/lib/errors";

/**
 * Puts an API error on the form: validation errors (camelCase keys, as the API sends them) on
 * their fields, and anything else returned as a message for the whole form.
 */
export function applyServerErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fields: readonly Path<T>[],
): string | null {
  if (!(error instanceof ApiError)) {
    return errorMessage(error, "");
  }

  let unplaced = false;
  for (const [key, messages] of Object.entries(error.problem?.errors ?? {})) {
    const field = fields.find((f) => f === key);
    if (field) {
      setError(field, { message: messages.join(" ") });
    } else {
      unplaced = true;
    }
  }

  const hasFieldErrors = Object.keys(error.problem?.errors ?? {}).length > 0;
  if (hasFieldErrors && !unplaced) {
    return null;
  }
  return errorMessage(error, error.problem?.title ?? "Something went wrong. Try again.");
}
