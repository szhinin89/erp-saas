import type { AccountDto } from "../api/accountingApi";

/**
 * ACCOUNTING-CHART-UX-01 — modelo puro del Plan de Cuentas (sin UI ni API).
 *
 * La jerarquía se construye con `parentAccountId` (fuente real del backend). Una cuenta cuyo padre
 * no está en la lista cargada, apunta a sí misma o forma parte de un ciclo se trata como raíz y
 * queda marcada en `orphanIds`: nunca se pierde del listado. El orden entre hermanos es el orden
 * natural por código contable.
 */

const accountCodeSegments = (code: string) => code.split(".").filter(Boolean);

export function compareAccountCode(leftCode: string, rightCode: string): number {
  const leftSegments = accountCodeSegments(leftCode);
  const rightSegments = accountCodeSegments(rightCode);
  const segmentCount = Math.min(leftSegments.length, rightSegments.length);

  for (let index = 0; index < segmentCount; index += 1) {
    const leftSegment = leftSegments[index];
    const rightSegment = rightSegments[index];
    if (/^\d+$/.test(leftSegment) && /^\d+$/.test(rightSegment)) {
      const numericComparison = Number(leftSegment) - Number(rightSegment);
      if (numericComparison !== 0) return numericComparison;
    }
    const textComparison = leftSegment.localeCompare(rightSegment, undefined, {
      numeric: true,
      sensitivity: "base",
    });
    if (textComparison !== 0) return textComparison;
  }

  return leftSegments.length - rightSegments.length || leftCode.localeCompare(rightCode);
}

/** Quita prefijos de árbol en texto ("L ", "│ ├ ") heredados en nombres importados. */
export const cleanAccountName = (name: string) =>
  name.replace(/^\s*(?:(?:L|[│└├])\s+)+/, "");

/**
 * Código del padre canónico implicado por el código (espejo de
 * `AccountHierarchyRules.GetExpectedParentCode` del backend): todo hasta el último ".".
 * Solo se usa para presentar/preseleccionar el padre; el servidor sigue validándolo.
 */
export function expectedParentCode(code: string): string | null {
  const trimmed = code.trim();
  const lastDot = trimmed.lastIndexOf(".");
  return lastDot <= 0 ? null : trimmed.slice(0, lastDot);
}

export interface AccountTree {
  roots: AccountDto[];
  childrenById: ReadonlyMap<string, AccountDto[]>;
  byId: ReadonlyMap<string, AccountDto>;
  depthById: ReadonlyMap<string, number>;
  /** Cuentas con `parentAccountId` no resoluble (padre ausente o ciclo) mostradas como raíz. */
  orphanIds: ReadonlySet<string>;
}

export function buildAccountTree(accounts: AccountDto[]): AccountTree {
  const sorted = [...accounts].sort((a, b) => compareAccountCode(a.code, b.code));
  const byId = new Map(sorted.map((account) => [account.id, account]));
  const orphanIds = new Set<string>();

  const resolvesToRoot = (account: AccountDto): boolean => {
    const seen = new Set<string>([account.id]);
    let current = account.parentAccountId;
    while (current) {
      if (seen.has(current)) return false;
      const parent = byId.get(current);
      if (!parent) return true;
      seen.add(current);
      current = parent.parentAccountId;
    }
    return true;
  };

  const effectiveParent = new Map<string, string | null>();
  for (const account of sorted) {
    const parentId = account.parentAccountId;
    const usable =
      parentId !== null && parentId !== account.id && byId.has(parentId) && resolvesToRoot(account);
    if (parentId !== null && !usable) orphanIds.add(account.id);
    effectiveParent.set(account.id, usable ? parentId : null);
  }

  const roots: AccountDto[] = [];
  const childrenById = new Map<string, AccountDto[]>();
  for (const account of sorted) {
    const parentId = effectiveParent.get(account.id) ?? null;
    if (parentId === null) {
      roots.push(account);
      continue;
    }
    const siblings = childrenById.get(parentId);
    if (siblings) siblings.push(account);
    else childrenById.set(parentId, [account]);
  }

  const depthById = new Map<string, number>();
  const assignDepth = (nodes: AccountDto[], depth: number) => {
    for (const node of nodes) {
      depthById.set(node.id, depth);
      assignDepth(childrenById.get(node.id) ?? [], depth + 1);
    }
  };
  assignDepth(roots, 0);

  return { roots, childrenById, byId, depthById, orphanIds };
}

export interface AccountTreeRow {
  account: AccountDto;
  depth: number;
  childCount: number;
  /** Ancestro mostrado solo para dar contexto a una coincidencia: no cuenta como resultado. */
  isContext: boolean;
}

const childCountOf = (tree: AccountTree, id: string) => tree.childrenById.get(id)?.length ?? 0;

/** Cuentas con hijas (las únicas que se pueden expandir/contraer). */
export function containerIds(tree: AccountTree): string[] {
  return [...tree.childrenById.keys()];
}

/** Estado inicial: niveles 0 y 1 visibles — se contraen las cuentas con hijas desde el nivel 1. */
export function initialCollapsedIds(tree: AccountTree): Set<string> {
  return new Set(containerIds(tree).filter((id) => (tree.depthById.get(id) ?? 0) >= 1));
}

/** Ancestros de `id`, de la raíz hacia abajo (sin incluir la propia cuenta). */
export function ancestorIds(tree: AccountTree, id: string): string[] {
  const result: string[] = [];
  let current = tree.byId.get(id);
  while (current) {
    const parentId: string | null = tree.orphanIds.has(current.id) ? null : current.parentAccountId;
    if (!parentId) break;
    result.unshift(parentId);
    current = tree.byId.get(parentId);
  }
  return result;
}

/** Vista de árbol sin filtros: recorre en profundidad omitiendo hijas de cuentas contraídas. */
export function visibleTreeRows(tree: AccountTree, collapsed: ReadonlySet<string>): AccountTreeRow[] {
  const rows: AccountTreeRow[] = [];
  const walk = (nodes: AccountDto[], depth: number) => {
    for (const account of nodes) {
      rows.push({ account, depth, childCount: childCountOf(tree, account.id), isContext: false });
      if (!collapsed.has(account.id)) walk(tree.childrenById.get(account.id) ?? [], depth + 1);
    }
  };
  walk(tree.roots, 0);
  return rows;
}

/**
 * Vista filtrada: coincidencias más sus ancestros de contexto, todo expandido (el estado de
 * contracción no puede ocultar un resultado).
 */
export function filteredTreeRows(tree: AccountTree, matches: ReadonlySet<string>): AccountTreeRow[] {
  const rows: AccountTreeRow[] = [];
  const walk = (nodes: AccountDto[], depth: number): boolean => {
    let any = false;
    for (const account of nodes) {
      const position = rows.length;
      const descendantMatched = walk(tree.childrenById.get(account.id) ?? [], depth + 1);
      const matched = matches.has(account.id);
      if (matched || descendantMatched) {
        rows.splice(position, 0, {
          account,
          depth,
          childCount: childCountOf(tree, account.id),
          isContext: !matched,
        });
        any = true;
      }
    }
    return any;
  };
  walk(tree.roots, 0);
  return rows;
}

/** Vista plana (filtro por nivel): solo coincidencias, en orden de código, sin sangría. */
export function flatRows(tree: AccountTree, matches: ReadonlySet<string>): AccountTreeRow[] {
  return [...tree.byId.values()]
    .filter((account) => matches.has(account.id))
    .map((account) => ({ account, depth: 0, childCount: 0, isContext: false }));
}
