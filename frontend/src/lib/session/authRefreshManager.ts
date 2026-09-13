import axios, { type AxiosError } from "axios";
import { useAuthStore } from "../../store/authStore";
import { getAccessToken, setAccessToken } from "./authTokenMemory";
import { readEnvelopePayload } from "../../modules/lib/apiEnvelope";
import { logDevSessionContext } from "./devSessionLog";
import { normalizeAuthResponse } from "../../modules/auth/normalizeAuthResponse";
import type { ApiResponse } from "../../types/api";

const viteApiBase =
  (import.meta.env.VITE_API_URL as string | undefined)?.trim() ?? "";

export const AUTH_REFRESH_LOCK = "erp-refresh";
export const AUTH_BROADCAST_CHANNEL = "erp.auth";
export const AUTH_LOGOUT_STORAGE_KEY = "erp.auth.logout";

export type AuthBroadcastEvent =
  | { type: "refresh_started"; tabId: string }
  | { type: "refresh_success"; tabId: string; accessToken: string }
  | { type: "refresh_failed"; tabId: string; retryable?: boolean }
  | { type: "logout" };

type RefreshPayload = ApiResponse<Record<string, unknown>>;

const TAB_ID =
  typeof crypto !== "undefined" && "randomUUID" in crypto
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
let storageLogoutListenerInstalled = false;

function getChannel(): BroadcastChannel | null {
  if (typeof BroadcastChannel === "undefined") return null;
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

function handleRemoteLogout(): void {
  remoteRefreshPending = false;
  remoteRefreshWaiters
    .splice(0)
    .forEach((w) => w.reject(new Error("logout")));
  import("./fullLogout").then(({ fullLogout }) => {
    fullLogout({ resetStores: true, broadcast: false });
    if (
      typeof window !== "undefined" &&
      !window.location.pathname.startsWith("/login")
    ) {
      window.location.href = "/login";
    }
  });
}

function handleBroadcast(event: AuthBroadcastEvent): void {
  if (event.type === "refresh_started" && event.tabId !== TAB_ID) {
    remoteRefreshPending = true;
    return;
  }

  if (event.type === "refresh_success" && event.tabId !== TAB_ID) {
    remoteRefreshPending = false;
    setAccessToken(event.accessToken);
    useAuthStore.getState().updateTokens(event.accessToken, null);
    const waiters = remoteRefreshWaiters.splice(0);
    waiters.forEach((w) => w.resolve(event.accessToken));
    return;
  }

  if (event.type === "refresh_failed" && event.tabId !== TAB_ID) {
    remoteRefreshPending = false;
    if (!event.retryable) {
      const waiters = remoteRefreshWaiters.splice(0);
      const err = new Error("Refresh remoto falló");
      waiters.forEach((w) => w.reject(err));
    }
    return;
  }

  if (event.type === "logout") {
    handleRemoteLogout();
  }
}

function waitForRemoteRefresh(
  timeoutMs = LOCK_TIMEOUT_MS,
  force = false,
): Promise<string> {
  if (!force) {
    const existing = getAccessToken();
    if (existing) return Promise.resolve(existing);
  }

  return new Promise<string>((resolve, reject) => {
    const timer = window.setTimeout(() => {
      remoteRefreshWaiters = remoteRefreshWaiters.filter(
        (w) => w.resolve !== wrappedResolve,
      );
      reject(new Error("Timeout esperando refresh de otra pestaña"));
    }, timeoutMs);

    const wrappedResolve = (token: string) => {
      window.clearTimeout(timer);
      resolve(token);
    };
    const wrappedReject = (err: unknown) => {
      window.clearTimeout(timer);
      reject(err);
    };

    remoteRefreshWaiters.push({
      resolve: wrappedResolve,
      reject: wrappedReject,
    });
  });
}

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

function isRetryableRefreshError(err: unknown): boolean {
  const ax = err as AxiosError<{ message?: string; Message?: string }>;
  if (ax.response?.status !== 401) return false;
  const msg = (
    ax.response.data?.message ??
    ax.response.data?.Message ??
    ""
  ).toLowerCase();
  return (
    msg.includes("ya utilizado") ||
    msg.includes("rotado") ||
    msg.includes("utilizado")
  );
}

async function postRefresh(): Promise<string> {
  const res = await axios.post<RefreshPayload>(
    `${viteApiBase}/api/v1/auth/refresh`,
    {},
    { withCredentials: true },
  );
  const session = normalizeAuthResponse(
    readEnvelopePayload<Record<string, unknown> | null>(res.data),
  );
  if (!session.token) {
    throw new Error("La renovación de sesión no devolvió token.");
  }
  setAccessToken(session.token);
  useAuthStore.getState().login(session);
  logDevSessionContext("refresh");
  return session.token;
}

async function executeRefreshOnce(
  allowBootstrapRetry: boolean,
): Promise<string> {
  try {
    return await postRefresh();
  } catch (err) {
    if (allowBootstrapRetry && isRetryableRefreshError(err)) {
      const delay =
        BOOTSTRAP_RETRY_MIN_MS +
        Math.floor(
          Math.random() * (BOOTSTRAP_RETRY_MAX_MS - BOOTSTRAP_RETRY_MIN_MS),
        );
      await sleep(delay);
      return postRefresh();
    }
    throw err;
  }
}

async function withRefreshLock<T>(fn: () => Promise<T>): Promise<T> {
  if (typeof navigator !== "undefined" && navigator.locks?.request) {
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

async function runRefresh(
  options: { bootstrapRetry?: boolean; force?: boolean } = {},
): Promise<string> {
  if (!options.force) {
    const existing = getAccessToken();
    if (existing) return existing;
  }

  if (remoteRefreshPending) {
    try {
      return await waitForRemoteRefresh(LOCK_TIMEOUT_MS, options.force);
    } catch {
      /* continuar con refresh local */
    }
  }

  broadcast({ type: "refresh_started", tabId: TAB_ID });

  try {
    const access = await withRefreshLock(() =>
      executeRefreshOnce(options.bootstrapRetry ?? false),
    );
    broadcast({ type: "refresh_success", tabId: TAB_ID, accessToken: access });
    return access;
  } catch (err) {
    broadcast({
      type: "refresh_failed",
      tabId: TAB_ID,
      retryable: isRetryableRefreshError(err),
    });
    throw err;
  }
}

export type RefreshSessionOptions = {
  /** Reintento único tras 401 benigno (cookie actualizada por otra pestaña). */
  bootstrapRetry?: boolean;
  /**
   * Fuerza una llamada real a /auth/refresh aunque ya exista un access token en memoria.
   * Necesario para quien pide el refresh reaccionando a un 401 (interceptor de `api.ts`): el
   * token en memoria en ese momento ES el mismo que el backend acaba de rechazar por vencido,
   * así que "ya existe uno" no significa "sigue siendo válido". Sin este flag,
   * refreshSessionToken() devolvía ese mismo token vencido sin tocar la red, el reintento
   * fallaba con el mismo 401, y como el request ya había agotado su único reintento
   * (`_retry`), el error se propagaba sin haber refrescado nada
   * (SALES-SAVE-401-AFTER-IDLE-SESSION-01). El caso normal (bootstrap en
   * restoreSessionFromCookie, cache entre requests concurrentes) sigue sin `force` — ahí un
   * token en memoria sí es válido, no hay ninguna respuesta 401 que lo haya invalidado. */
  force?: boolean;
};

/**
 * Gestor único de refresh: Web Locks + BroadcastChannel + single-flight por pestaña.
 */
export function refreshSessionToken(
  options: RefreshSessionOptions = {},
): Promise<string> {
  if (!options.force) {
    const existing = getAccessToken();
    if (existing) return Promise.resolve(existing);
  }

  if (inFlightRefresh) return inFlightRefresh;

  inFlightRefresh = runRefresh(options).finally(() => {
    inFlightRefresh = null;
  });

  return inFlightRefresh;
}

export function resetRefreshSessionFlight(): void {
  inFlightRefresh = null;
  remoteRefreshPending = false;
  remoteRefreshWaiters
    .splice(0)
    .forEach((w) => w.reject(new Error("Refresh cancelado")));
}

export function broadcastAuthLogout(): void {
  broadcast({ type: "logout" });
  try {
    // Fallback para pestañas que no exponen o pierden BroadcastChannel.
    localStorage.setItem(
      AUTH_LOGOUT_STORAGE_KEY,
      `${Date.now()}-${Math.random()}`,
    );
  } catch {
    /* storage disabled */
  }
}

/**
 * Registra el listener compartido al arrancar cada pestaña. El canal antes se
 * abría sólo durante un refresh o al emitir un evento, por lo que una pestaña
 * ya autenticada podía no escuchar el logout remoto.
 */
export function initializeAuthBroadcastListener(): void {
  getChannel();
  if (
    !storageLogoutListenerInstalled &&
    typeof window !== "undefined"
  ) {
    window.addEventListener("storage", (event) => {
      if (event.key === AUTH_LOGOUT_STORAGE_KEY && event.newValue) {
        handleRemoteLogout();
      }
    });
    storageLogoutListenerInstalled = true;
  }
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
