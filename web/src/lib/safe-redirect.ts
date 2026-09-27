/**
 * The path to go to after signing in, from the ?redirect= search param. Only same-origin paths
 * are accepted ("/campaigns/1"), never "//evil.example" or a full URL, so a crafted link can't
 * send someone off-site after they sign in.
 */
export function safeRedirect(path: string | undefined): string {
  return path?.startsWith("/") && !path.startsWith("//") && !path.startsWith("/\\") ? path : "/";
}
