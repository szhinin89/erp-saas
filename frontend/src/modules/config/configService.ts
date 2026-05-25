import { api } from '../lib/api';
import type { ApiResponse } from '../../types/api';
import type { ConfigDeleteInput, ConfigEntry, ConfigScope, ConfigUpsertInput } from './types';
import { PLATFORM_API } from '../platform/api/platformApiPaths';

const platformConfigBase = (subscriberId: string) =>
  `${PLATFORM_API.config}/${encodeURIComponent(subscriberId)}`;

function normalizeEntry(raw: Partial<ConfigEntry>): ConfigEntry | null {
  const scope = (raw.scope ?? '').toString().trim().toLowerCase() as ConfigScope;
  if (!raw.id || !raw.subscriberId || !raw.key || !raw.value || !scope) return null;
  if (scope !== 'global' && scope !== 'module' && scope !== 'feature') return null;
  return {
    id: raw.id,
    subscriberId: raw.subscriberId,
    scope,
    module: raw.module ?? null,
    feature: raw.feature ?? null,
    key: raw.key,
    value: raw.value,
    dataType: raw.dataType ?? 'string',
    updatedAt: raw.updatedAt ?? null,
    updatedBy: raw.updatedBy ?? null,
  };
}

function normalizeList(list: unknown): ConfigEntry[] {
  if (!Array.isArray(list)) return [];
  return list.map((x) => normalizeEntry(x as Partial<ConfigEntry>)).filter((x): x is ConfigEntry => Boolean(x));
}

async function safeGet<T>(url: string, params?: Record<string, string | undefined>): Promise<T | null> {
  try {
    const res = await api.get<ApiResponse<T>>(url, params ? { params } : undefined);
    return res.data.responseObject ?? null;
  } catch {
    return null;
  }
}

export const configService = {
  /** Config global del suscriptor activo (JWT). Admin u operador platform en runtime suscriptor. */
  async loadCurrentSubscriberConfig(): Promise<ConfigEntry[]> {
    const rows = await safeGet<unknown[]>('/api/settings/config/global');
    return normalizeList(rows ?? []);
  },

  /** Config de un suscriptor concreto vía panel platform (JWT global). */
  async loadSubscriberConfig(subscriberId: string): Promise<ConfigEntry[]> {
    const fallbackGlobal = await safeGet<unknown[]>(
      `${platformConfigBase(subscriberId)}/global`,
    );
    return normalizeList(fallbackGlobal ?? []);
  },

  async upsertConfig(subscriberId: string, input: ConfigUpsertInput): Promise<ConfigEntry> {
    const key = input.key.trim();
    const payload = { key, value: input.value, dataType: input.dataType };

    if (input.scope === 'global') {
      const res = await api.put<ApiResponse<ConfigEntry>>(
        `${platformConfigBase(subscriberId)}/global`,
        payload,
      );
      const normalized = normalizeEntry(res.data.responseObject ?? undefined);
      if (!normalized) throw new Error('invalid config response');
      return normalized;
    }
    if (input.scope === 'module') {
      const moduleName = (input.module ?? '').trim();
      const res = await api.put<ApiResponse<ConfigEntry>>(
        `${platformConfigBase(subscriberId)}/module/${encodeURIComponent(moduleName)}`,
        payload,
      );
      const normalized = normalizeEntry(res.data.responseObject ?? undefined);
      if (!normalized) throw new Error('invalid config response');
      return normalized;
    }
    const featureName = (input.feature ?? '').trim();
    const res = await api.put<ApiResponse<ConfigEntry>>(
      `${platformConfigBase(subscriberId)}/feature/${encodeURIComponent(featureName)}`,
      payload,
    );
    const normalized = normalizeEntry(res.data.responseObject ?? undefined);
    if (!normalized) throw new Error('invalid config response');
    return normalized;
  },

  async deleteConfig(subscriberId: string, input: ConfigDeleteInput): Promise<void> {
    const key = input.key.trim();
    if (input.scope === 'global') {
      await api.delete(`${platformConfigBase(subscriberId)}/global/${encodeURIComponent(key)}`);
      return;
    }
    if (input.scope === 'module') {
      const moduleName = (input.module ?? '').trim();
      await api.delete(
        `${platformConfigBase(subscriberId)}/module/${encodeURIComponent(moduleName)}/${encodeURIComponent(key)}`,
      );
      return;
    }
    const featureName = (input.feature ?? '').trim();
    await api.delete(
      `${platformConfigBase(subscriberId)}/feature/${encodeURIComponent(featureName)}/${encodeURIComponent(key)}`,
    );
  },
};

