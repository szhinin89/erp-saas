import type { SupplierExpenseSubcategoriesDto } from "../../masterData/facades/supplierExpenseSubcategoryLookupFacade";
import type { ExpenseCategoryTreeNodeDto } from "../api/expenseCategoryService";
import type { ExpenseDraftLineState } from "../components/ExpenseDocumentLinesEditor";

/**
 * EXPENSE-SUPPLIER-SUBCATEGORIES-01 (fase 3) — lectura en cliente de las subcategorías permitidas del
 * proveedor. Una sola fuente de verdad: el modo y los disponibles los calcula el servidor; aquí solo se
 * aplican a la selección (el backend revalida siempre).
 *
 * - `none`: sin proveedor o documento no editable → sin restricción en cliente.
 * - `ready.allowedIds === null` → `Unconfigured` (catálogo completo); `Set` vacío → `RestrictedEmpty`.
 */
export type ExpenseSupplierScope =
  | { status: "none" }
  | { status: "loading" }
  | { status: "error"; message: string }
  | {
      status: "ready";
      supplierId: string;
      mode: SupplierExpenseSubcategoriesDto["mode"];
      allowedIds: Set<string> | null;
      singleId: string | null;
    };

export type LineScopeIssue = "blocked" | "saved";

export function scopeFromDto(
  supplierId: string,
  dto: SupplierExpenseSubcategoriesDto,
): ExpenseSupplierScope {
  const allowedIds =
    dto.mode === "Unconfigured" ? null : new Set(dto.availableSubcategoryIds);
  return {
    status: "ready",
    supplierId,
    mode: dto.mode,
    allowedIds,
    singleId: allowedIds && allowedIds.size === 1 ? [...allowedIds][0] : null,
  };
}

export function scopeAllows(scope: ExpenseSupplierScope, id: string): boolean {
  return (
    scope.status !== "ready" || !scope.allowedIds || scope.allowedIds.has(id)
  );
}

/** Subcategoría ya guardada en el borrador y todavía sin cambiar en esta sesión. */
export function isPersistedSelection(line: ExpenseDraftLineState): boolean {
  return (
    line.persistedSubcategoryId !== undefined &&
    line.persistedSubcategoryId === line.expenseSubcategoryId
  );
}

/**
 * Incidencia de una línea frente al alcance (D3/D7):
 * - persistida + mismo proveedor → `saved` (se conserva, solo aviso; guardar y confirmar siguen permitidos);
 * - persistida + proveedor cambiado, o selección nueva no permitida → `blocked` (bloquea guardar y confirmar).
 */
export function lineScopeIssue(
  line: ExpenseDraftLineState,
  scope: ExpenseSupplierScope,
  supplierChanged: boolean,
): LineScopeIssue | null {
  if (
    !line.expenseSubcategoryId ||
    scopeAllows(scope, line.expenseSubcategoryId)
  )
    return null;
  return isPersistedSelection(line) && !supplierChanged ? "saved" : "blocked";
}

/**
 * Aplica el alcance a las líneas al conocerse (o cambiar) el proveedor. Nunca toca una selección
 * persistida (abrir un borrador no modifica sus líneas): solo limpia las nuevas incompatibles y
 * asigna la única permitida a las vacías. Devuelve la misma lista si nada cambió.
 */
export function reconcileLinesWithScope(
  lines: ExpenseDraftLineState[],
  scope: ExpenseSupplierScope,
): { lines: ExpenseDraftLineState[]; cleared: number } {
  if (scope.status !== "ready") return { lines, cleared: 0 };
  let cleared = 0;
  let changed = false;
  const next = lines.map((line) => {
    if (isPersistedSelection(line)) return line;
    let id = line.expenseSubcategoryId;
    if (id && !scopeAllows(scope, id)) {
      id = "";
      cleared += 1;
    }
    if (!id && scope.singleId) id = scope.singleId;
    if (id === line.expenseSubcategoryId) return line;
    changed = true;
    return { ...line, expenseSubcategoryId: id };
  });
  return { lines: changed ? next : lines, cleared };
}

/**
 * Árbol visible para una línea: solo Tipo/Categoría/Subcategoría con alguna subcategoría permitida,
 * más la subcategoría actual de la línea (`keepId`) para no ocultar nunca un dato existente.
 */
export function filterExpenseTreeByScope(
  tree: ExpenseCategoryTreeNodeDto[],
  scope: ExpenseSupplierScope,
  keepId: string,
): ExpenseCategoryTreeNodeDto[] {
  if (scope.status !== "ready" || !scope.allowedIds) return tree;
  const allowed = scope.allowedIds;
  const keep = (node: ExpenseCategoryTreeNodeDto) =>
    allowed.has(node.id) || node.id === keepId;
  return tree
    .map((type) => ({
      ...type,
      children: type.children
        .map((category) => ({
          ...category,
          children: category.children.filter(keep),
        }))
        .filter((category) => category.children.length > 0),
    }))
    .filter((type) => type.children.length > 0);
}
