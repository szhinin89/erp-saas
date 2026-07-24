import { useEffect, useState } from 'react';

const STORAGE_KEY = 'zh-launcher-expanded';

function loadExpandedIds(): Set<string> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return new Set();
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return new Set();
    return new Set(parsed.filter((x): x is string => typeof x === 'string'));
  } catch {
    return new Set();
  }
}

/**
 * Persiste en localStorage qué módulos/categorías del App Launcher están expandidos,
 * combinado con auto-expand de la sección que contiene la ruta activa (entradas en
 * `autoExpandIds` que aún no tengan preferencia explícita del usuario).
 */
export function useLauncherExpansion(autoExpandIds: string[]) {
  const [expandedIds, setExpandedIds] = useState<Set<string>>(() => {
    const stored = loadExpandedIds();
    for (const id of autoExpandIds) stored.add(id);
    return stored;
  });

  // Auto-expande la sección activa al navegar, sin colapsar las que el usuario ya abrió.
  useEffect(() => {
    if (autoExpandIds.length === 0) return;
    setExpandedIds((prev) => {
      let changed = false;
      const next = new Set(prev);
      for (const id of autoExpandIds) {
        if (!next.has(id)) {
          next.add(id);
          changed = true;
        }
      }
      return changed ? next : prev;
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [autoExpandIds.join('|')]);

  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify([...expandedIds]));
    } catch {
      // ignore
    }
  }, [expandedIds]);

  const isExpanded = (id: string) => expandedIds.has(id);

  const toggle = (id: string) => {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  return { isExpanded, toggle };
}
