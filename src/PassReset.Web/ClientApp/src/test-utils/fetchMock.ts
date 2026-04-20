import { vi } from 'vitest';

interface MockInit {
  status?: number;
  ok?: boolean;
  contentType?: string;
}

function buildResponse(body: unknown, init: MockInit): Response {
  const status = init.status ?? 200;
  const ok = init.ok ?? (status >= 200 && status < 300);
  const contentType = init.contentType ?? 'application/json';
  return {
    ok,
    status,
    headers: {
      get: (name: string) => (name.toLowerCase() === 'content-type' ? contentType : null),
    },
    json: async () => body,
    text: async () => (typeof body === 'string' ? body : JSON.stringify(body)),
  } as unknown as Response;
}

/**
 * Stubs global fetch to resolve once with the given JSON body.
 * Subsequent calls reject (flushing to any second unmocked call loudly).
 */
export function mockFetchOnce(body: unknown, init: MockInit = {}) {
  const fn = vi.fn().mockResolvedValueOnce(buildResponse(body, init));
  vi.stubGlobal('fetch', fn);
  return fn;
}

/**
 * Stubs global fetch to reject with an error once.
 */
export function mockFetchReject(error: unknown = new Error('network')) {
  const fn = vi.fn().mockRejectedValueOnce(error);
  vi.stubGlobal('fetch', fn);
  return fn;
}

/**
 * Stubs global fetch to resolve with the same response for every call.
 */
export function mockFetchAlways(body: unknown, init: MockInit = {}) {
  const fn = vi.fn().mockResolvedValue(buildResponse(body, init));
  vi.stubGlobal('fetch', fn);
  return fn;
}

/**
 * URL-routing fetch mock. Routes requests by URL substring match.
 * Falls back to the `default` response (or a safe empty-HIBP response) for
 * unmatched URLs so that incidental fetches (e.g. debounced HIBP breach
 * check via useHibpCheck) do not consume the primary test mock under CI-
 * specific timing (see commit history: 2026-04-20 CI race).
 */
export function mockFetchByUrl(
  routes: Record<string, { body: unknown; init?: MockInit }>,
  options: { default?: { body: unknown; init?: MockInit } } = {},
) {
  const fallbackBody = options.default?.body ?? '';
  const fallbackInit = options.default?.init ?? { status: 200 };
  const fn = vi.fn(async (input: RequestInfo | URL, _init?: RequestInit) => {
    const url =
      typeof input === 'string'
        ? input
        : input instanceof URL
          ? input.toString()
          : (input as Request).url;
    for (const [needle, response] of Object.entries(routes)) {
      if (url.includes(needle)) {
        return buildResponse(response.body, response.init ?? {});
      }
    }
    return buildResponse(fallbackBody, fallbackInit);
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}
