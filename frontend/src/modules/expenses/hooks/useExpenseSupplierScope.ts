import { useEffect, useMemo, useRef, useState } from "react";
import { useCompanyScopedAsync } from "../../../hooks/useCompanyScopedAsync";
import { useI18n } from "../../../i18n/i18n";
import { useAuthStore } from "../../../store/authStore";
import { supplierExpenseSubcategoryLookupFacade } from "../../masterData/facades/supplierExpenseSubcategoryLookupFacade";
import type { ExpenseDraftLineState } from "../components/ExpenseDocumentLinesEditor";
import {
  lineScopeIssue,
  reconcileLinesWithScope,
  scopeFromDto,
  type ExpenseSupplierScope,
} from "../utils/expenseSupplierScopeModel";

export interface ExpenseSupplierScopeState {
  scope: ExpenseSupplierScope;
  /** Aviso (no bloqueante) tras limpiar selecciones nuevas incompatibles al cambiar de proveedor. */
  notice: string | null;
  supplierChanged: boolean;
  /** Alguna línea elegida no está permitida: bloquea guardar y confirmar (D3). */
  blocked: boolean;
  /** Lectura en curso o fallida: bloquea guardar, pero no confirmar un borrador ya guardado (D7). */
  pending: boolean;
}

/**
 * EXPENSE-SUPPLIER-SUBCATEGORIES-01 (fase 3) — consulta la configuración de subcategorías del proveedor
 * seleccionado (manual o desde XML) con el `GET` existente y la aplica a las líneas. Se vuelve a leer al
 * cambiar de proveedor o de empresa. Si la lectura falla NO cae al catálogo completo: bloquea.
 */
export function useExpenseSupplierScope(params: {
  supplierId: string;
  enabled: boolean;
  supplierChanged: boolean;
  lines: ExpenseDraftLineState[];
  setLines: (lines: ExpenseDraftLineState[]) => void;
}): ExpenseSupplierScopeState {
  const { supplierId, enabled, supplierChanged, lines, setLines } = params;
  const { t } = useI18n();
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);
  const active = enabled && !!supplierId;
  const read = useCompanyScopedAsync(
    async () => ({
      supplierId,
      companySessionVersion,
      dto: await supplierExpenseSubcategoryLookupFacade.get(supplierId),
    }),
    active,
    [supplierId],
  );

  const scope = useMemo<ExpenseSupplierScope>(() => {
    if (!active) return { status: "none" };
    // Los datos solo valen para el proveedor con que se leyeron.
    if (read.error && !read.loading)
      return { status: "error", message: read.error };
    if (read.data && read.data.supplierId === supplierId &&
        read.data.companySessionVersion === companySessionVersion && !read.loading)
      return scopeFromDto(supplierId, read.data.dto);
    return { status: "loading" };
  }, [active, read.data, read.error, read.loading, supplierId, companySessionVersion]);

  const linesRef = useRef(lines);
  linesRef.current = lines;
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    if (scope.status === "loading") setNotice(null);
    if (scope.status !== "ready") return;
    const result = reconcileLinesWithScope(linesRef.current, scope);
    if (result.lines !== linesRef.current) setLines(result.lines);
    setNotice(
      result.cleared > 0
        ? t("expenses.form.supplierScope.cleared", { count: result.cleared })
        : null,
    );
    // Solo al conocerse/cambiar el alcance: las ediciones del usuario no se reescriben.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scope]);

  const pending = scope.status === "loading" || scope.status === "error";
  const blocked = lines.some(
    (line) => lineScopeIssue(line, scope, supplierChanged) === "blocked",
  );
  return { scope, notice, supplierChanged, blocked, pending };
}
