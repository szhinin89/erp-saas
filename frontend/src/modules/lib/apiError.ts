import axios from "axios";

function pickString(v: unknown): string | null {
  if (typeof v === "string" && v.trim()) return v.trim();
  return null;
}

function pickStringArray(value: unknown): string[] {
  if (!Array.isArray(value)) return [];
  return value.flatMap((item) => {
    const message = pickString(item);
    return message ? [message] : [];
  });
}

function pickErrorMapStrings(value: unknown): string[] {
  if (!value || typeof value !== "object" || Array.isArray(value)) return [];
  return Object.values(value as Record<string, unknown>).flatMap((entry) =>
    pickStringArray(entry),
  );
}

function pickErrors(value: unknown): string[] {
  const fromArray = pickStringArray(value);
  if (fromArray.length > 0) return fromArray;
  return pickErrorMapStrings(value);
}

function pickNestedString(
  o: Record<string, unknown>,
  container: string,
  field: string,
): string | null {
  const nested = o[container];
  if (nested && typeof nested === "object" && !Array.isArray(nested)) {
    return pickString((nested as Record<string, unknown>)[field]);
  }
  return null;
}

function pickNestedErrorStrings(
  o: Record<string, unknown>,
  container: string,
  field: string,
): string[] {
  const nested = o[container];
  if (nested && typeof nested === "object" && !Array.isArray(nested)) {
    return pickErrors((nested as Record<string, unknown>)[field]);
  }
  return [];
}

function messagesFromRecord(o: Record<string, unknown>): string[] {
  // 1. data.errors — array plano o mapa campo→[msgs] (formato 422 estructurado).
  const dataErrors = [
    ...pickNestedErrorStrings(o, "data", "errors"),
    ...pickNestedErrorStrings(o, "Data", "Errors"),
  ];
  if (dataErrors.length > 0) return dataErrors;

  // 2. message.user (o Message.User) — mensaje genérico del catálogo por code.
  const catalogMessage =
    pickNestedString(o, "message", "user") ??
    pickNestedString(o, "Message", "User");
  if (catalogMessage) return [catalogMessage];

  // 3. Fallbacks para respuestas que no pasan por ResponseFactory.
  const direct =
    pickString(o.message) ??
    pickString(o.Message) ??
    pickString(o.title) ??
    pickString(o.detail) ??
    pickString(o.error);
  if (direct) return [direct];

  // 4. Compatibilidad con respuestas ModelState de ASP.NET (errors: { field: [msgs] })
  const modelStateErrors = pickErrorMapStrings(o.errors);
  return modelStateErrors;
}

function messageFromRecord(o: Record<string, unknown>): string | null {
  return messagesFromRecord(o)[0] ?? null;
}

/**
 * Extrae un mensaje legible del cuerpo de error de la API (ASP.NET, middleware, ProblemDetails, etc.).
 * Devuelve null si no hay información útil.
 */
export function readApiErrorMessage(err: unknown): string | null {
  if (!axios.isAxiosError(err) || !err.response) return null;

  const data = err.response.data;
  if (data == null) return null;

  if (typeof data === "string") {
    const s = data.trim();
    if (!s || s.startsWith("<")) return null;
    try {
      const j = JSON.parse(s) as unknown;
      if (j && typeof j === "object" && !Array.isArray(j)) {
        return messageFromRecord(j as Record<string, unknown>);
      }
    } catch {
      return s.length < 400 ? s : null;
    }
    return null;
  }

  if (typeof data === "object" && !Array.isArray(data)) {
    return messageFromRecord(data as Record<string, unknown>);
  }

  return null;
}

/**
 * Extrae todos los mensajes legibles del cuerpo de error de la API.
 * Prioriza data.errors sobre message.user para no ocultar validaciones específicas
 * detrás del mensaje genérico del catálogo.
 */
export function readApiErrorMessages(err: unknown): string[] {
  if (!axios.isAxiosError(err) || !err.response) return [];

  const data = err.response.data;
  if (data == null) return [];

  if (typeof data === "string") {
    const s = data.trim();
    if (!s || s.startsWith("<")) return [];
    try {
      const j = JSON.parse(s) as unknown;
      if (j && typeof j === "object" && !Array.isArray(j)) {
        return messagesFromRecord(j as Record<string, unknown>);
      }
    } catch {
      return s.length < 400 ? [s] : [];
    }
    return [];
  }

  if (typeof data === "object" && !Array.isArray(data)) {
    return messagesFromRecord(data as Record<string, unknown>);
  }

  return [];
}

/**
 * Extrae el mapa campo→mensajes de un error 422 del backend.
 * Formato esperado en data.errors: { fieldName: string[] }
 * Retorna null si el error no es 422 o no tiene ese formato.
 */
export function parseValidationErrors(
  err: unknown,
): Record<string, string[]> | null {
  if (!axios.isAxiosError(err) || err.response?.status !== 422) return null;
  const responseData = err.response.data as Record<string, unknown> | null;
  if (!responseData) return null;
  const inner = responseData["data"];
  if (!inner || typeof inner !== "object" || Array.isArray(inner)) return null;
  const errors = (inner as Record<string, unknown>)["errors"];
  if (Array.isArray(errors)) {
    const messages = pickStringArray(errors);
    return messages.length > 0 ? { _: messages } : null;
  }
  if (!errors || typeof errors !== "object") return null;
  return errors as Record<string, string[]>;
}

function messageFromProblemDetails(o: Record<string, unknown>): string | null {
  const errors = o.errors;
  if (errors && typeof errors === "object" && !Array.isArray(errors)) {
    for (const key of Object.keys(errors as Record<string, unknown>)) {
      const arr = (errors as Record<string, unknown>)[key];
      if (Array.isArray(arr) && arr.length > 0) {
        const m = pickString(arr[0]);
        if (m) return m;
      }
    }
  }
  return null;
}

/**
 * Registra `message.dev` (o `Message.Dev`) en consola para diagnóstico — nunca se muestra al usuario.
 * No-op si la respuesta no trae ese campo.
 */
export function logApiDevError(err: unknown): void {
  if (!axios.isAxiosError(err) || !err.response) return;
  const data = err.response.data;
  let record: Record<string, unknown> | null = null;
  if (data && typeof data === "object" && !Array.isArray(data)) {
    record = data as Record<string, unknown>;
  } else if (typeof data === "string") {
    try {
      const parsed = JSON.parse(data) as unknown;
      if (parsed && typeof parsed === "object" && !Array.isArray(parsed))
        record = parsed as Record<string, unknown>;
    } catch {
      return;
    }
  }
  if (!record) return;
  const dev =
    pickNestedString(record, "message", "dev") ??
    pickNestedString(record, "Message", "Dev");
  if (dev) console.error("[API]", dev);
}

/** Mensaje para mostrar en UI: cuerpo de error, red sin respuesta, o texto genérico. */
export function formatApiRequestError(
  err: unknown,
  labels: { offline?: string; generic: string },
): string {
  logApiDevError(err);
  const fromApi = readApiErrorMessage(err);
  if (fromApi) return fromApi;

  if (axios.isAxiosError(err)) {
    if (!err.response) {
      return labels.offline ?? labels.generic;
    }
    if (err.response.status === 401) {
      return "Sesión expirada o no autorizada. Vuelve a iniciar sesión.";
    }
    const data = err.response.data;
    if (data && typeof data === "object" && !Array.isArray(data)) {
      const fromProblem = messageFromProblemDetails(
        data as Record<string, unknown>,
      );
      if (fromProblem) return fromProblem;
    }
  }

  if (err instanceof Error && err.message.trim()) {
    // Axios errors with a server response must never expose the raw HTTP status
    // message ("Request failed with status code 4xx") — that's useless to the user.
    // Use the caller-supplied generic label instead.
    if (axios.isAxiosError(err) && err.response) {
      return labels.generic;
    }
    return err.message.trim();
  }

  return labels.generic;
}
