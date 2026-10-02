import { expect, request } from "@playwright/test";

const mailpit = "http://localhost:8025";

interface MessageSummary {
  ID: string;
  Subject: string;
}

const isWelcome = (message: MessageSummary) => message.Subject.startsWith("Welcome to");

/**
 * The text of the latest email to `to`, waiting for it to arrive: the app sends emails from a
 * background queue. Leaves out the welcome every new account gets (decision 0023), unless
 * `welcome` asks for it.
 */
export async function latestEmailText(to: string, { welcome = false } = {}): Promise<string> {
  const api = await request.newContext({ baseURL: mailpit });
  try {
    let id: string | undefined;
    await expect(async () => {
      const response = await api.get("/api/v1/search", { params: { query: `to:"${to}"` } });
      const body = (await response.json()) as { messages: MessageSummary[] };
      id = body.messages.find((m) => isWelcome(m) === welcome)?.ID;
      expect(id, `an email to ${to}`).toBeDefined();
    }).toPass({ timeout: 15_000 });
    const message = await api.get(`/api/v1/message/${id ?? ""}`);
    return ((await message.json()) as { Text: string }).Text;
  } finally {
    await api.dispose();
  }
}

/** The first link in `text` that starts with `prefix`. */
export function linkIn(text: string, prefix: string): string {
  const link = text.match(/https?:\/\/\S+/g)?.find((url) => url.startsWith(prefix));
  if (!link) throw new Error(`No link starting with ${prefix} in:\n${text}`);
  return link;
}
