import axios from 'axios';

function pickString(v: unknown): string | null {
  if (typeof v === 'string' && v.trim()) return v.trim();
  return null;
}

function messageFromRecord(o: Record<string, unknown>): string | null {
  const direct =
    pickString(o.message) ??
    pickString(o.Message) ??
    pickString(o.title) ??
    pickString(o.detail) ??
    pickString(o.error);
  if (direct) return direct;

  const responseObject = o.responseObject;
  if (responseObject && typeof responseObject === 'object' && !Array.isArray(responseObject)) {
    const roMsg = messageFromRecord(responseObject as Record<string, unknown>);
    if (roMsg) return roMsg;
  }

  const code = pickString(o.code);
  if (code === 'company.ruc_already_exists') {
    return 'El RUC ya está registrado en el sistema.';
  }

  const errors = o.errors;
  if (errors && typeof errors === 'object' && !Array.isArray(errors)) {
    const firstKey = Object.keys(errors as Record<string, unknown>)[0];
    if (firstKey) {
      const arr = (errors as Record<string, unknown>)[firstKey];
      if (Array.isArray(arr) && arr.length > 0) {
        const m = pickString(arr[0]);
        if (m) return m;
      }
    }
  }
  return null;
}

/**
 * Extrae un mensaje legible del cuerpo de error de la API (ASP.NET, middleware, ProblemDetails, etc.).
 * Devuelve null si no hay información útil.
 */
export function readApiErrorMessage(err: unknown): string | null {
  if (!axios.isAxiosError(err) || !err.response) return null;

  const data = err.response.data;
  if (data == null) return null;

  if (typeof data === 'string') {
    const s = data.trim();
    if (!s || s.startsWith('<')) return null;
    try {
      const j = JSON.parse(s) as unknown;
      if (j && typeof j === 'object' && !Array.isArray(j)) {
        return messageFromRecord(j as Record<string, unknown>);
      }
    } catch {
      return s.length < 400 ? s : null;
    }
    return null;
  }

  if (typeof data === 'object' && !Array.isArray(data)) {
    return messageFromRecord(data as Record<string, unknown>);
  }

  return null;
}

function messageFromProblemDetails(o: Record<string, unknown>): string | null {
  const errors = o.errors;
  if (errors && typeof errors === 'object' && !Array.isArray(errors)) {
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

/** Mensaje para mostrar en UI: cuerpo de error, red sin respuesta, o texto genérico. */
export function formatApiRequestError(
  err: unknown,
  labels: { offline?: string; generic: string },
): string {
  const fromApi = readApiErrorMessage(err);
  if (fromApi) return fromApi;

  if (axios.isAxiosError(err)) {
    if (!err.response) {
      return labels.offline ?? labels.generic;
    }
    if (err.response.status === 401) {
      return 'Sesión expirada o no autorizada. Vuelve a iniciar sesión.';
    }
    const data = err.response.data;
    if (data && typeof data === 'object' && !Array.isArray(data)) {
      const fromProblem = messageFromProblemDetails(data as Record<string, unknown>);
      if (fromProblem) return fromProblem;
    }
  }

  if (err instanceof Error && err.message.trim()) {
    return err.message.trim();
  }

  return labels.generic;
}
