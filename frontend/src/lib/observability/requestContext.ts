const CORRELATION_KEY = 'erp.correlationId';

function newCorrelationId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return `erp-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

/** Id estable por pestaña para trazas (OpenTelemetry / Datadog / Sentry). */
export function getCorrelationId(): string {
  if (typeof sessionStorage === 'undefined') return newCorrelationId();
  let id = sessionStorage.getItem(CORRELATION_KEY);
  if (!id) {
    id = newCorrelationId();
    sessionStorage.setItem(CORRELATION_KEY, id);
  }
  return id;
}

export function rotateCorrelationId(): string {
  const id = newCorrelationId();
  if (typeof sessionStorage !== 'undefined') {
    sessionStorage.setItem(CORRELATION_KEY, id);
  }
  return id;
}
