const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: "medium" });
const dateTimeFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
});

/** A UTC timestamp from the API, as a date in the user's locale and time zone. */
export const formatDate = (utc: string) => dateFormat.format(new Date(utc));

/**
 * A UTC timestamp from the API (or milliseconds since the epoch), as a date and time in the
 * user's locale and time zone.
 */
export const formatDateTime = (utc: string | number) => dateTimeFormat.format(new Date(utc));
