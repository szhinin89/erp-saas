import { api } from './api';
import type { ApiResponse } from '../../types/api';

type EnvelopeBody = ApiResponse<unknown> | Record<string, unknown>;

type ApiRequestConfig = {
  params?: Record<string, string | number | boolean | undefined>;
};

/** Extrae `responseObject` / `ResponseObject` del envelope estándar de la API. */
export function readEnvelopePayload<T>(body: unknown): T {
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if ('responseObject' in o && o.responseObject !== undefined) return o.responseObject as T;
    if ('ResponseObject' in o && o.ResponseObject !== undefined) return o.ResponseObject as T;
  }
  return body as T;
}

export function apiGet<T>(url: string, config?: ApiRequestConfig): Promise<T> {
  return api.get<EnvelopeBody>(url, config).then((r) => readEnvelopePayload<T>(r.data));
}

export function apiPost<T>(url: string, body: unknown, config?: ApiRequestConfig): Promise<T> {
  return api.post<EnvelopeBody>(url, body, config).then((r) => readEnvelopePayload<T>(r.data));
}

export function apiPut<T>(url: string, body: unknown, config?: ApiRequestConfig): Promise<T> {
  return api.put<EnvelopeBody>(url, body, config).then((r) => readEnvelopePayload<T>(r.data));
}

export function apiPatch<T>(url: string, body: unknown = {}, config?: ApiRequestConfig): Promise<T> {
  return api.patch<EnvelopeBody>(url, body, config).then((r) => readEnvelopePayload<T>(r.data));
}

export function apiDelete<T>(url: string, config?: ApiRequestConfig): Promise<T> {
  return api.delete<EnvelopeBody>(url, config).then((r) => readEnvelopePayload<T>(r.data));
}
