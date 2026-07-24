/**
 * Minimal HTTP helper (Node fetch — no production dependencies).
 * @param {string} method
 * @param {string} url
 * @param {{ headers?: Record<string,string>; body?: unknown; token?: string }} [opts]
 */
export async function request(method, url, opts = {}) {
  const headers = { ...(opts.headers ?? {}) };
  if (opts.token) headers.Authorization = `Bearer ${opts.token}`;
  if (opts.body !== undefined && !headers['Content-Type']) {
    headers['Content-Type'] = 'application/json';
  }

  const res = await fetch(url, {
    method,
    headers,
    body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
  });

  const text = await res.text();
  return { status: res.status, ok: res.ok, text, headers: res.headers };
}

/** @param {string} bodyText */
export function unwrapEnvelope(bodyText) {
  if (!bodyText) return null;
  try {
    const body = JSON.parse(bodyText);
    return body.data ?? body;
  } catch {
    return null;
  }
}
