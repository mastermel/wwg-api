import { getAccessToken, refreshAccessToken } from "@/lib/access-token";

/**
 * The one fetch function every generated API call goes through (Orval's "mutator"). It adds the
 * access token, and after a 401 refreshes once (shared with any other callers) and retries.
 */

/** RFC 9457 Problem Details, as every API error returns (validation errors add `errors`). */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** Validation errors by camelCase field name. */
  errors?: Record<string, string[]>;
}

/** A non-2xx API response. `body` is the parsed response body, if there was one. */
export class ApiError<TBody = unknown> extends Error {
  readonly status: number;
  readonly body: TBody | undefined;
  /** The Problem Details, when the response was `application/problem+json`. */
  readonly problem: ProblemDetails | undefined;

  constructor(status: number, body: TBody | undefined, problem: ProblemDetails | undefined) {
    super(problem?.title ?? `The request failed with status ${String(status)}.`);
    this.name = "ApiError";
    this.status = status;
    this.body = body;
    this.problem = problem;
  }
}

/** Orval types each hook's error with this, from the error bodies the endpoint declares. */
export type ErrorType<TBody> = ApiError<TBody>;

export async function apiFetch<T>(url: string, init: RequestInit): Promise<T> {
  let response = await send(url, init);

  // The auth endpoints answer 401 for their own reasons (wrong password, session over).
  if (response.status === 401 && !url.startsWith("/api/auth/")) {
    if ((await refreshAccessToken()) === "refreshed") {
      response = await send(url, init);
    }
  }

  const body = await readBody(response);

  if (!response.ok) {
    const isProblem = response.headers.get("content-type")?.includes("application/problem+json");
    throw new ApiError(response.status, body, isProblem ? (body as ProblemDetails) : undefined);
  }

  // The generated caller supplies T from the OpenAPI document; the body is trusted to match.
  return body as T;
}

function send(url: string, init: RequestInit) {
  const headers = new Headers(init.headers);
  const token = getAccessToken();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  // Always same-origin. Resolving against the page's origin also makes relative URLs work in
  // tests, where fetch doesn't know the page's address.
  return fetch(new URL(url, window.location.origin), { ...init, headers });
}

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (text === "") {
    return undefined;
  }

  const isJson = response.headers.get("content-type")?.includes("json") ?? false;
  return isJson ? (JSON.parse(text) as unknown) : text;
}
