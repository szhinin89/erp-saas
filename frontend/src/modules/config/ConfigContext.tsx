import { createContext, useCallback, useContext, useEffect, useMemo, useReducer, useRef } from 'react';
import { useAuthStore } from '../../store/authStore';
import { isAdminRole } from '../../access/permissionUi';

import { getAccessToken } from '../../lib/session/authTokenMemory';
import { configService } from './configService';
import type {
  ConfigDeleteInput,
  ConfigEntry,
  ConfigLoadStatus,
  ConfigResolvedValue,
  ConfigScope,
  ConfigUpsertInput,
} from './types';

type ConfigState = {
  tenantId: string | null;
  status: ConfigLoadStatus;
  error: string | null;
  entries: ConfigEntry[];
  globalMap: Record<string, ConfigEntry>;
  moduleMap: Record<string, ConfigEntry>;
  featureMap: Record<string, ConfigEntry>;
};

type ConfigAction =
  | { type: 'load_start'; tenantId: string }
  | { type: 'load_success'; tenantId: string; entries: ConfigEntry[] }
  | { type: 'load_error'; tenantId: string; error: string }
  | { type: 'upsert_entry'; entry: ConfigEntry }
  | { type: 'delete_entry'; scope: ConfigScope; key: string; module?: string | null; feature?: string | null }
  | { type: 'clear' };

const initialState: ConfigState = {
  tenantId: null,
  status: 'idle',
  error: null,
  entries: [],
  globalMap: {},
  moduleMap: {},
  featureMap: {},
};

function normalizeKey(value: string | null | undefined): string {
  return (value ?? '').trim().toLowerCase();
}

function moduleKey(module: string | null | undefined, key: string): string {
  return `${normalizeKey(module)}::${normalizeKey(key)}`;
}

function featureKey(feature: string | null | undefined, key: string): string {
  return `${normalizeKey(feature)}::${normalizeKey(key)}`;
}

function indexEntries(entries: ConfigEntry[]) {
  const globalMap: Record<string, ConfigEntry> = {};
  const moduleMap: Record<string, ConfigEntry> = {};
  const featureMap: Record<string, ConfigEntry> = {};

  for (const entry of entries) {
    const normalizedEntry: ConfigEntry = {
      ...entry,
      key: normalizeKey(entry.key),
      module: entry.module ? normalizeKey(entry.module) : null,
      feature: entry.feature ? normalizeKey(entry.feature) : null,
    };
    if (normalizedEntry.scope === 'global') {
      globalMap[normalizedEntry.key] = normalizedEntry;
      continue;
    }
    if (normalizedEntry.scope === 'module') {
      moduleMap[moduleKey(normalizedEntry.module, normalizedEntry.key)] = normalizedEntry;
      continue;
    }
    featureMap[featureKey(normalizedEntry.feature, normalizedEntry.key)] = normalizedEntry;
  }

  return { globalMap, moduleMap, featureMap };
}

function configReducer(state: ConfigState, action: ConfigAction): ConfigState {
  switch (action.type) {
    case 'load_start':
      return { ...state, tenantId: action.tenantId, status: 'loading', error: null };
    case 'load_success': {
      const entries = action.entries;
      const indexed = indexEntries(entries);
      return {
        tenantId: action.tenantId,
        status: 'ready',
        error: null,
        entries,
        ...indexed,
      };
    }
    case 'load_error':
      return { ...state, tenantId: action.tenantId, status: 'error', error: action.error };
    case 'upsert_entry': {
      const nextEntries = [...state.entries];
      const keyNorm = normalizeKey(action.entry.key);
      const scope = action.entry.scope;
      const moduleNorm = action.entry.module ? normalizeKey(action.entry.module) : null;
      const featureNorm = action.entry.feature ? normalizeKey(action.entry.feature) : null;
      const idx = nextEntries.findIndex(
        (x) =>
          x.scope === scope &&
          normalizeKey(x.key) === keyNorm &&
          normalizeKey(x.module) === normalizeKey(moduleNorm) &&
          normalizeKey(x.feature) === normalizeKey(featureNorm),
      );
      const normalizedEntry: ConfigEntry = {
        ...action.entry,
        key: keyNorm,
        module: moduleNorm,
        feature: featureNorm,
      };
      if (idx >= 0) nextEntries[idx] = normalizedEntry;
      else nextEntries.push(normalizedEntry);
      const indexed = indexEntries(nextEntries);
      return { ...state, entries: nextEntries, ...indexed };
    }
    case 'delete_entry': {
      const keyNorm = normalizeKey(action.key);
      const moduleNorm = normalizeKey(action.module);
      const featureNorm = normalizeKey(action.feature);
      const nextEntries = state.entries.filter((x) => {
        if (x.scope !== action.scope) return true;
        if (normalizeKey(x.key) !== keyNorm) return true;
        if (action.scope === 'module' && normalizeKey(x.module) !== moduleNorm) return true;
        if (action.scope === 'feature' && normalizeKey(x.feature) !== featureNorm) return true;
        return false;
      });
      const indexed = indexEntries(nextEntries);
      return { ...state, entries: nextEntries, ...indexed };
    }
    case 'clear':
      return initialState;
    default:
      return state;
  }
}

function parseByDataType(rawValue: string, dataType: string): unknown {
  const dt = normalizeKey(dataType);
  if (dt === 'boolean' || dt === 'bool') {
    if (normalizeKey(rawValue) === 'true') return true;
    if (normalizeKey(rawValue) === 'false') return false;
    return rawValue;
  }
  if (dt === 'number' || dt === 'int' || dt === 'long' || dt === 'decimal' || dt === 'double') {
    const n = Number(rawValue);
    return Number.isFinite(n) ? n : rawValue;
  }
  if (dt === 'json') {
    try {
      return JSON.parse(rawValue);
    } catch {
      return rawValue;
    }
  }
  return rawValue;
}

type ConfigContextValue = {
  status: ConfigLoadStatus;
  error: string | null;
  tenantId: string | null;
  entries: ConfigEntry[];
  refresh: () => Promise<void>;
  getResolved: <T = unknown>(key: string, module?: string | null, feature?: string | null) => ConfigResolvedValue<T> | null;
  getValue: <T = unknown>(key: string, module?: string | null, feature?: string | null, defaultValue?: T) => T;
  upsertConfig: (input: ConfigUpsertInput) => Promise<ConfigEntry>;
  deleteConfig: (input: ConfigDeleteInput) => Promise<void>;
};

const ConfigContext = createContext<ConfigContextValue | null>(null);

export function ConfigProvider({ children }: { children: React.ReactNode }) {
  const user = useAuthStore((s) => s.user);
  const hasHydrated = useAuthStore((s) => s.hasHydrated);
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const [state, dispatch] = useReducer(configReducer, initialState);
  const inFlightRef = useRef<Promise<void> | null>(null);

  const tenantId = (user?.tenantId ?? '').trim();
  const canReadSessionConfig = isAdminRole(user?.role) && !!tenantId;
  const shouldLoad = canReadSessionConfig;
  /** Tras F5 el perfil hidrata antes que el access token (cookie refresh en SessionBootstrap). */
  const sessionReady = hasHydrated && (!isAuthenticated || getAccessToken() !== null);

  const loadTenantConfig = useCallback(async () => {
    if (!sessionReady) return;

    if (!shouldLoad) {
      dispatch({ type: 'clear' });
      return;
    }

    if (inFlightRef.current) {
      await inFlightRef.current;
      return;
    }

    const task = (async () => {
      dispatch({ type: 'load_start', tenantId });
      try {
        const entries = await configService.loadTenantConfig(tenantId);
        dispatch({ type: 'load_success', tenantId, entries });
      } catch (error) {
        const message = error instanceof Error ? error.message : 'No se pudo cargar configuración del tenant.';
        dispatch({ type: 'load_error', tenantId, error: message });
      }
    })();

    inFlightRef.current = task;
    try {
      await task;
    } finally {
      inFlightRef.current = null;
    }
  }, [sessionReady, shouldLoad, tenantId]);

  useEffect(() => {
    void loadTenantConfig();
  }, [loadTenantConfig]);

  const getResolved = useCallback(
    <T = unknown>(key: string, module?: string | null, feature?: string | null): ConfigResolvedValue<T> | null => {
      const normalizedKey = normalizeKey(key);
      if (!normalizedKey) return null;

      const featureEntry = feature
        ? state.featureMap[featureKey(feature, normalizedKey)]
        : undefined;
      if (featureEntry) {
        return {
          value: parseByDataType(featureEntry.value, featureEntry.dataType) as T,
          scope: 'feature',
          dataType: featureEntry.dataType,
        };
      }

      const moduleEntry = module
        ? state.moduleMap[moduleKey(module, normalizedKey)]
        : undefined;
      if (moduleEntry) {
        return {
          value: parseByDataType(moduleEntry.value, moduleEntry.dataType) as T,
          scope: 'module',
          dataType: moduleEntry.dataType,
        };
      }

      const globalEntry = state.globalMap[normalizedKey];
      if (globalEntry) {
        return {
          value: parseByDataType(globalEntry.value, globalEntry.dataType) as T,
          scope: 'global',
          dataType: globalEntry.dataType,
        };
      }

      return null;
    },
    [state.featureMap, state.globalMap, state.moduleMap],
  );

  const getValue = useCallback(
    <T = unknown>(key: string, module?: string | null, feature?: string | null, defaultValue?: T): T => {
      const resolved = getResolved<T>(key, module, feature);
      if (!resolved) return defaultValue as T;
      return resolved.value;
    },
    [getResolved],
  );

  const upsertConfig = useCallback(
    async (input: ConfigUpsertInput) => {
      if (!shouldLoad) throw new Error('No hay tenant activo para guardar configuración.');
      const entry = await configService.upsertConfig(tenantId, input);
      dispatch({ type: 'upsert_entry', entry });
      return entry;
    },
    [shouldLoad, tenantId],
  );

  const deleteConfig = useCallback(
    async (input: ConfigDeleteInput) => {
      if (!shouldLoad) throw new Error('No hay tenant activo para eliminar configuración.');
      await configService.deleteConfig(tenantId, input);
      dispatch({
        type: 'delete_entry',
        scope: input.scope,
        key: input.key,
        module: input.module,
        feature: input.feature,
      });
    },
    [shouldLoad, tenantId],
  );

  const value = useMemo<ConfigContextValue>(
    () => ({
      status: state.status,
      error: state.error,
      tenantId: state.tenantId,
      entries: state.entries,
      refresh: loadTenantConfig,
      getResolved,
      getValue,
      upsertConfig,
      deleteConfig,
    }),
    [state, loadTenantConfig, getResolved, getValue, upsertConfig, deleteConfig],
  );

  return <ConfigContext.Provider value={value}>{children}</ConfigContext.Provider>;
}

export function useConfig(): ConfigContextValue {
  const ctx = useContext(ConfigContext);
  if (!ctx) throw new Error('useConfig debe usarse dentro de <ConfigProvider>.');
  return ctx;
}

