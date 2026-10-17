/**
 * ACCOUNTING-CONTINUITY-06 / ACCOUNTING-07 — etiqueta de cada código del readiness único de
 * completitud contable (reportes y pre-cierre de período). Solo presentación: la decisión de
 * completitud viene íntegra del backend, nunca se recalcula en el frontend.
 */
export const COMPLETENESS_REASON_LABELS: Record<string, string> = {
  POSTINGS_UNRESOLVED: "Contabilizaciones pendientes o fallidas",
  OPENING_BATCHES_UNPOSTED: "Lotes de apertura sin asiento",
  OPENING_ASI_MISSING: "Asiento inicial (ASI) sin publicar",
  OPENING_ASI_UNRESOLVED: "Asiento inicial (ASI) pendiente o fallido",
  SALE_COST_PENDING: "Ventas con costo pendiente",
  ORIGINS_WITHOUT_ACCOUNTING: "Documentos sin contabilización",
  JOURNAL_ENTRIES_NOT_FINAL: "Asientos sin publicar",
  JOURNAL_ENTRIES_WITHOUT_NUMBER: "Asientos sin número",
  REVERSALS_INCOMPLETE: "Reversos incompletos",
  PREVIOUS_PERIOD_OPEN: "Período anterior abierto",
  YEAR_END_CLOSING_MISSING: "Asiento de cierre anual sin contabilizar",
};

export const completenessReasonLabel = (code: string): string =>
  COMPLETENESS_REASON_LABELS[code] ?? code;
