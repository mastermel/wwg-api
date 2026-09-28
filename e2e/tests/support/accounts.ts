/**
 * The Admin account ./stack.sh creates (and Admin:Emails in compose.yaml names). Shared by every
 * run: never sign in to it with a wrong password, or Identity's lockout breaks the tests that use it.
 */
export const admin = { email: "admin@e2e.test", password: "e2e admin password" };

/** Every other user's password. */
export const password = "correct horse battery";

let counter = 0;

/**
 * An email address no other test (or run, or browser project) uses, so tests can run in parallel
 * against one database.
 */
export function uniqueEmail(name: string): string {
  counter += 1;
  const stamp = `${String(Date.now())}-${String(process.pid)}-${String(counter)}`;
  return `${name.toLowerCase()}-${stamp}@e2e.test`;
}
