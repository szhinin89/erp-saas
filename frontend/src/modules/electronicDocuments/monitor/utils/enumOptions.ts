/** Valores reales del enum de dominio `ElectronicDocumentState` — no inventar estados nuevos. */
export const ELECTRONIC_DOCUMENT_STATES = [
  'Draft', 'XmlGenerated', 'Signed', 'Sent', 'Received',
  'Authorized', 'Rejected', 'DeadLetter', 'Cancelled', 'Failed',
] as const;

/** Valores reales del enum de dominio `ElectronicDocumentType`. */
export const ELECTRONIC_DOCUMENT_TYPES = [
  'Invoice', 'CreditNote', 'DebitNote', 'Retention', 'ShippingGuide', 'PurchaseSettlement',
] as const;

/** Códigos oficiales SRI (Ficha Técnica, Tabla 4 "Ambiente") — mismos que `SriSettings.Environment` ("1"=Pruebas, "2"=Producción). */
export const ELECTRONIC_DOCUMENT_ENVIRONMENTS = ['1', '2'] as const;
