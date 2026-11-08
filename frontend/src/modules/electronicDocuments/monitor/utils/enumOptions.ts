/** Valores reales del enum de dominio `ElectronicDocumentState` — no inventar estados nuevos. */
export const ELECTRONIC_DOCUMENT_STATES = [
  "Draft",
  "XmlGenerated",
  "Signed",
  "Sent",
  "Received",
  "Authorized",
  "Rejected",
  "DeadLetter",
  "Cancelled",
  "Failed",
  // ADR-036 (ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A)
  "Dispatching",
  "Discarded",
  // ZH-RETENTION-SRI-ANNULMENT-01
  "AnnulmentPending",
] as const;

/**
 * Grupos de estados que el Resumen usa como filtro (valor = estados reales separados por coma, tal
 * como acepta `GET /api/v1/electronic-documents`). Espejan las agrupaciones del backend
 * (`GetElectronicDocumentsDashboardQueryHandler`) — no son estados nuevos. `attention` reúne los
 * estados que pueden requerir revisión manual (Rechazado, Reintentos agotados, Falló la generación y
 * Despachando, que solo se resuelve por consulta manual — ADR-036).
 */
export const ELECTRONIC_DOCUMENT_STATE_GROUPS = {
  pending: {
    value: "Draft,XmlGenerated",
    labelKey: "electronicDocuments.monitor.dashboard.pending",
  },
  inProgress: {
    value: "Signed,Sent,Received",
    labelKey: "electronicDocuments.monitor.dashboard.inProgress",
  },
  error: {
    value: "DeadLetter,Failed",
    labelKey: "electronicDocuments.monitor.dashboard.error",
  },
  attention: {
    value: "Rejected,DeadLetter,Failed,Dispatching",
    labelKey: "electronicDocuments.monitor.attention.title",
  },
} as const;

/** Valores reales del enum de dominio `ElectronicDocumentType`. */
export const ELECTRONIC_DOCUMENT_TYPES = [
  "Invoice",
  "CreditNote",
  "DebitNote",
  "Retention",
  "ShippingGuide",
  "PurchaseSettlement",
] as const;

/** Códigos oficiales SRI (Ficha Técnica, Tabla 4 "Ambiente") — mismos que `SriSettings.Environment` ("1"=Pruebas, "2"=Producción). */
export const ELECTRONIC_DOCUMENT_ENVIRONMENTS = ["1", "2"] as const;
