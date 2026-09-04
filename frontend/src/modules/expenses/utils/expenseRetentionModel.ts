import { parseExpenseNumber } from "./expenseDocumentDraftModel";
import type {
  RetentionEligibilityResult,
  RetentionIntentLineRequest,
  RetentionIntentRequest,
  RetentionTaxType,
} from "../api/expenseDocumentService";

/**
 * RETENTIONS-UI-EXPENSES-01F — estado de captura de `RetentionIntent` en el formulario de
 * Gastos. Espejo de string del contrato de request (`RetentionIntentLineRequest`/
 * `RetentionIntentRequest`), mismo criterio ya usado por `ExpenseDraftLineState` frente a
 * `ExpenseDraftLineRequest`: los montos viven como string mientras se editan (compatibles con
 * `ZhDecimalInput`) y se parsean solo al construir el payload real.
 */
export interface RetentionIntentLineFormState {
  key: string;
  taxType: RetentionTaxType;
  retentionCode: string;
  baseAmount: string;
  retentionRate: string;
  retainedAmount: string;
  description: string;
}

export interface RetentionIntentFormState {
  appliesRetention: boolean;
  emissionPointId: string;
  retentionNumber: string;
  issueDate: string;
  lines: RetentionIntentLineFormState[];
}

export function newRetentionIntentLine(
  taxType: RetentionTaxType = "Vat",
): RetentionIntentLineFormState {
  return {
    key: globalThis.crypto?.randomUUID?.() ?? `ret-line-${Date.now()}-${Math.random()}`,
    taxType,
    retentionCode: "",
    baseAmount: "0.00",
    retentionRate: "0.00",
    retainedAmount: "0.00",
    description: "",
  };
}

export function emptyRetentionIntentState(): RetentionIntentFormState {
  return {
    appliesRetention: false,
    emissionPointId: "",
    retentionNumber: "",
    issueDate: "",
    lines: [],
  };
}

/**
 * Validación cliente mínima antes de enviar — el backend sigue siendo la fuente final de
 * verdad (revalida elegibilidad y cada línea igual). Esto solo evita mandar una solicitud que
 * la propia UI ya sabe incompleta.
 */
export function isRetentionIntentComplete(state: RetentionIntentFormState): boolean {
  if (!state.appliesRetention) return true;
  if (!state.emissionPointId) return false;
  if (!state.retentionNumber.trim()) return false;
  if (!state.issueDate) return false;
  if (state.lines.length === 0) return false;
  return state.lines.every((line) => {
    if (!line.retentionCode.trim()) return false;
    if (parseExpenseNumber(line.baseAmount) <= 0) return false;
    if (parseExpenseNumber(line.retentionRate) < 0) return false;
    if (parseExpenseNumber(line.retainedAmount) <= 0) return false;
    return true;
  });
}

/** `undefined` cuando no aplica — preserva el comportamiento actual de confirmar sin retención. */
export function buildRetentionIntentRequest(
  state: RetentionIntentFormState,
): RetentionIntentRequest | undefined {
  if (!state.appliesRetention) return undefined;
  const lines: RetentionIntentLineRequest[] = state.lines.map((line) => ({
    taxType: line.taxType,
    retentionCode: line.retentionCode.trim(),
    baseAmount: parseExpenseNumber(line.baseAmount),
    retentionRate: parseExpenseNumber(line.retentionRate),
    retainedAmount: parseExpenseNumber(line.retainedAmount),
    description: line.description.trim() || null,
  }));
  return {
    appliesRetention: true,
    emissionPointId: state.emissionPointId || null,
    retentionNumber: state.retentionNumber.trim() || null,
    issueDate: state.issueDate || null,
    lines,
  };
}

/**
 * True si, con la última elegibilidad conocida en el cliente, el intento del usuario debe
 * bloquearse ANTES de llamar al backend (mismo criterio de "fail closed" del resto del ERP: si
 * no hay elegibilidad conocida todavía, o el backend ya indicó que no aplica, no se envía).
 */
export function isRetentionIntentBlockedByEligibility(
  state: RetentionIntentFormState,
  eligibility: RetentionEligibilityResult | null,
): boolean {
  if (!state.appliesRetention) return false;
  return !eligibility?.isEligible;
}
