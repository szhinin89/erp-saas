import { request } from './http.js';

/**
 * Poll until predicate passes with exponential backoff.
 * @param {() => Promise<boolean>} probe
 * @param {{ label: string; timeoutMs?: number; initialDelayMs?: number; maxDelayMs?: number }} opts
 */
export async function pollUntilReady(probe, opts) {
  const timeoutMs = opts.timeoutMs ?? 90_000;
  const initialDelayMs = opts.initialDelayMs ?? 500;
  const maxDelayMs = opts.maxDelayMs ?? 5_000;
  const deadline = Date.now() + timeoutMs;
  let delay = initialDelayMs;
  let attempt = 0;

  while (Date.now() < deadline) {
    attempt += 1;
    try {
      if (await probe()) return { attempts: attempt, ready: true };
    } catch {
      // retry
    }

    await sleep(delay);
    delay = Math.min(Math.floor(delay * 1.5), maxDelayMs);
  }

  throw new Error(`${opts.label} not ready after ${timeoutMs}ms (${attempt} attempts)`);
}

/** @param {number} ms */
function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/**
 * @param {string} api
 * @param {{ timeoutMs?: number }} [opts]
 */
export async function waitForApiReady(api, opts = {}) {
  return pollUntilReady(
    async () => {
      const live = await request('GET', `${api}/health/live`);
      if (!live.ok) return false;
      const ready = await request('GET', `${api}/health/ready`);
      return ready.ok;
    },
    { label: 'API', timeoutMs: opts.timeoutMs ?? 90_000 },
  );
}

/**
 * @param {string} frontendUrl
 * @param {{ timeoutMs?: number }} [opts]
 */
export async function waitForFrontendReady(frontendUrl, opts = {}) {
  return pollUntilReady(
    async () => {
      const r = await request('GET', `${frontendUrl}/`);
      return r.ok;
    },
    { label: 'Frontend', timeoutMs: opts.timeoutMs ?? 60_000 },
  );
}
