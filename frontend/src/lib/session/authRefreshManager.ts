import axios, { type AxiosError } from 'axios';
import { useAuthStore } from '../../store/authStore';
import { getAccessToken, setAccessToken } from './authTokenMemory';
import { readEnvelopePayload } from '../../modules/lib/apiEnvelope';
import { normalizeAuthResponse } from '../../modules/auth/normalizeAuthResponse';
import type { ApiResponse } from '../../types/api';

const viteApiBase = (import.meta.env.VITE_API_URL as string | undefined)?.trim() ?? '';

export const AUTH_REFRESH_LOCK = 'erp-refresh';
export const AUTH_BROADCAST_CHANNEL = 'erp.auth';

export type AuthBroadcastEvent =
  | { type: 'refresh_started'; tabId: string }
  | { type: 'refresh_success'; tabId: string; accessToken: string }
  | { type: 'refresh_failed'; tabId: string; retryable?: boolean }
  | { type: 'logout' };

type RefreshPayload = ApiResponse<Record<string, unknown>>;

const TAB_ID =
  typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `tab-${Date.now()}`;

const LOCK_TIMEOUT_MS = 15_000;
const BOOTSTRAP_RETRY_MIN_MS = 100;
const BOOTSTRAP_RETRY_MAX_MS = 300;

let channel: BroadcastChannel | null = null;
let inFlightRefresh: Promise<string> | null = null;
let remoteRefreshWaiters: Array<{
  resolve: (token: string) => void;
  reject: (err: unknown) => void;
}> = [];
let remoteRefreshPending = false;

function getChannel(): BroadcastChannel | null {
  if (typeof BroadcastChannel === 'undefined') return null;
  if (!channel) {
    channel = new BroadcastChannel(AUTH_BROADCAST_CHANNEL);
    channel.onmessage = (ev: MessageEvent<AuthBroadcastEvent>) => {
      handleBroadcast(ev.data);
    };
  }
  return channel;
}

function broadcast(event: AuthBroadcastEvent): void {
  try {
    getChannel()?.postMessage(event);
  } catch {
    /* channel closed / unavailable */
  }
}

function handleBroadcast(event: AuthBroadcastEvent): void {
  if (event.type === 'refresh_started' && event.tabId !== TAB_ID) {
    remoteRefreshPending = true;
    return;
  }

  if (event.type === 'refresh_success' && event.tabId !== TAB_ID) {
    remoteRefreshPending = false;
    setAccessToken(event.accessToken);
    useAuthStore.getState().updateTokens(event.accessToken, null);
    const waiters = remoteRefreshWaiters.splice(0);
    waiters.forEach((w) => w.resolve(event.accessToken));
    return;
  }

  if (event.type === 'refresh_failed' && event.tabId !== TAB_ID) {
    remoteRefreshPending = false;
    if (!event.retryable) {
      const waiters = remoteRefreshWaiters.splice(0);
      const err = new Error('Refresh remoto falló');
      waiters.forEach((w) => w.reject(err));
    }
    return;
  }

  if (event.type === 'logout') {
    remoteRefreshPending = false;
    remoteRefreshWaiters.splice(0).forEach((w) => w.reject(new Error('logout')));
    import('./fullLogout').then(({ fullLogout }) => {
      fullLogout({ resetStores: true, broadcast: false });
      if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/login')) {
        window.location.href = '/login';
      }
    });
  }
}

function waitForRemoteRefresh(timeoutMs = LOCK_TIMEOUT_MS): Promise<string> {
  const existing = getAccessToken();
  if (existing) return Promise.resolve(existing);

  return new Promise<string>((resolve, reject) => {
    const timer = window.setTimeout(() => {
      remoteRefreshWaiters = remoteRefreshWaiters.filter(
        (w) => w.resolve !== wrappedResolve,
      );
      reject(new Error('Timeout esperando refresh de otra pestaña'));
    }, timeoutMs);

    const wrappedResolve = (token: string) => {
      window.clearTimeout(timer);
      resolve(token);
    };
    const wrappedReject = (err: unknown) => {
      window.clearTimeout(timer);
      reject(err);
    };

    remoteRefreshWaiters.push({ resolve: wrappedResolve, reject: wrappedReject });
  });
}

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

function isRetryableRefreshError(err: unknown): boolean {
  const ax = err as AxiosError<{ message?: string; Message?: string }>;
  if (ax.response?.status !== 401) return false;
  const msg = (ax.response.data?.message ?? ax.response.data?.Message ?? '').toLowerCase();
  return msg.includes('ya utilizado') || msg.includes('rotado') || msg.includes('utilizado');
}

async function postRefresh(): Promise<string> {
  const res = await axios.post<RefreshPayload>(
    `${viteApiBase}/api/auth/refresh`,
    {},
    { withCredentials: true },
  );
  const session = normalizeAuthResponse(readEnvelopePayload<Record<string, unknown> | null>(res.data));
  if (!session.token) {
    throw new Error('La renovación de sesión no devolvió token.');
  }
  setAccessToken(session.token);
  useAuthStore.getState().login(session);
  return session.token;
}

async function executeRefreshOnce(allowBootstrapRetry: boolean): Promise<string> {
  try {
    return await postRefresh();
  } catch (err) {
    if (allowBootstrapRetry && isRetryableRefreshError(err)) {
      const delay =
        BOOTSTRAP_RETRY_MIN_MS +
        Math.floor(Math.random() * (BOOTSTRAP_RETRY_MAX_MS - BOOTSTRAP_RETRY_MIN_MS));
      await sleep(delay);
      return postRefresh();
    }
    throw err;
  }
}

async function withRefreshLock<T>(fn: () => Promise<T>): Promise<T> {
  if (typeof navigator !== 'undefined' && navigator.locks?.request) {
    const ac = new AbortController();
    const timer = window.setTimeout(() => ac.abort(), LOCK_TIMEOUT_MS);
    try {
      return await navigator.locks.request(
        AUTH_REFRESH_LOCK,
        { signal: ac.signal },
        async () => fn(),
      );
    } catch (err) {
      if (ac.signal.aborted) {
        return fn();
      }
      throw err;
    } finally {
      window.clearTimeout(timer);
    }
  }
  return fn();
}

async function runRefresh(options: { bootstrapRetry?: boolean } = {}): Promise<string> {
  const existing = getAccessToken();
  if (existing) return existing;

  if (remoteRefreshPending) {
    try {
      return await waitForRemoteRefresh();
    } catch {
      /* continuar con refresh local */
    }
  }

  broadcast({ type: 'refresh_started', tabId: TAB_ID });

  try {
    const access = await withRefreshLock(() =>
      executeRefreshOnce(options.bootstrapRetry ?? false),
    );
    broadcast({ type: 'refresh_success', tabId: TAB_ID, accessToken: access });
    return access;
  } catch (err) {
    broadcast({
      type: 'refresh_failed',
      tabId: TAB_ID,
      retryable: isRetryableRefreshError(err),
    });
    throw err;
  }
}

export type RefreshSessionOptions = {
  /** Reintento único tras 401 benigno (cookie actualizada por otra pestaña). */
  bootstrapRetry?: boolean;
};

/**
 * Gestor único de refresh: Web Locks + BroadcastChannel + single-flight por pestaña.
 */
export function refreshSessionToken(options: RefreshSessionOptions = {}): Promise<string> {
  const existing = getAccessToken();
  if (existing) return Promise.resolve(existing);

  if (inFlightRefresh) return inFlightRefresh;

  inFlightRefresh = runRefresh(options).finally(() => {
    inFlightRefresh = null;
  });

  return inFlightRefresh;
}

export function resetRefreshSessionFlight(): void {
  inFlightRefresh = null;
  remoteRefreshPending = false;
  remoteRefreshWaiters.splice(0).forEach((w) =>
    w.reject(new Error('Refresh cancelado')),
  );
}

export function broadcastAuthLogout(): void {
  broadcast({ type: 'logout' });
}

export function closeAuthBroadcastChannel(): void {
  try {
    channel?.close();
  } catch {
    /* */
  }
  channel = null;
}

/** Solo tests: simular evento remoto. */
export function __testHandleAuthBroadcast(event: AuthBroadcastEvent): void {
  handleBroadcast(event);
}

export function __testResetAuthRefreshManager(): void {
  resetRefreshSessionFlight();
  closeAuthBroadcastChannel();
}
