import { useAsync } from './useAsync';

/**
 * Alias explícito de `useAsync` — refetch automático al cambiar `companySessionVersion`.
 * Usar en queries operacionales multiempresa para no omitir el aislamiento de sesión.
 */
export function useCompanyScopedAsync<T>(
  fn: () => Promise<T>,
  enabled = true,
  extraDeps: readonly unknown[] = [],
) {
  return useAsync(fn, enabled, extraDeps);
}
