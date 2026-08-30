import type { BadgeVariant } from "../../../../components/PageShell";
import type {
  AccountingPostingMode,
  AuthorizationMode,
  CancellationMode,
  ConfirmationMode,
  CreationMode,
  InventoryImpactMode,
  NotificationMode,
  PayableGenerationMode,
  PendingDocumentMode,
} from "../api/documentFlowPolicyService";

/**
 * DOCUMENT-FLOW-POLICY-UX-01 — SSOT frontend de textos funcionales para
 * DocumentFlowPolicy. Ningún componente de esta pantalla debe interpolar un valor de
 * enum técnico directamente en el DOM — siempre a través de este mapa. Los valores del
 * enum (`CreationMode`, `ConfirmationMode`, etc.) son el contrato con el backend
 * (`PUT /api/v1/settings/document-flows/{id}` sigue recibiendo/enviando estos strings
 * técnicos tal cual) — este archivo solo traduce cómo se muestran, nunca qué se envía.
 */
export interface ModeOption<T extends string> {
  value: T;
  /** Texto de la columna de tabla / badge / opción de select. */
  label: string;
  /** Frase corta para el resumen funcional de la fila ("Nace como borrador · ..."). */
  summary: string;
  /** Texto de ayuda mostrado bajo el select en el editor (ZHField hint). */
  help: string;
  badgeVariant: BadgeVariant;
}

export const CREATION_MODE_OPTIONS: ModeOption<CreationMode>[] = [
  {
    value: "DraftRequired",
    label: "Borrador obligatorio",
    summary: "Nace como borrador",
    help: "El documento nace como borrador y luego debe confirmarse.",
    badgeVariant: "info",
  },
  {
    value: "DirectCreation",
    label: "Creación directa",
    summary: "Creación directa",
    help: "El documento se crea directamente en su siguiente estado permitido.",
    badgeVariant: "neutral",
  },
];

export const CONFIRMATION_MODE_OPTIONS: ModeOption<ConfirmationMode>[] = [
  {
    value: "ManualConfirmation",
    label: "Confirmación manual",
    summary: "Confirmación manual",
    help: "El usuario debe confirmar el documento después de crearlo.",
    badgeVariant: "neutral",
  },
  {
    value: "AutoConfirmOnCreate",
    label: "Confirmación automática",
    summary: "Confirmación automática",
    help: "El documento queda confirmado al momento de crearse.",
    badgeVariant: "info",
  },
  {
    value: "RequiresAuthorization",
    label: "Requiere autorización",
    summary: "Requiere autorización",
    help: "El documento debe aprobarse antes de confirmarse.",
    badgeVariant: "warning",
  },
];

export const AUTHORIZATION_MODE_OPTIONS: ModeOption<AuthorizationMode>[] = [
  {
    value: "None",
    label: "Sin autorización",
    summary: "Sin autorización",
    help: "El documento no requiere ninguna aprobación.",
    badgeVariant: "neutral",
  },
  {
    value: "SingleStep",
    label: "Aprobación simple",
    summary: "Aprobación simple",
    help: "Requiere un solo responsable que apruebe el documento.",
    badgeVariant: "info",
  },
  {
    value: "MultiStep",
    label: "Aprobación múltiple",
    summary: "Aprobación múltiple",
    help: "Requiere varios responsables aprobando en secuencia.",
    badgeVariant: "warning",
  },
];

export const PENDING_DOCUMENT_MODE_OPTIONS: ModeOption<PendingDocumentMode>[] = [
  {
    value: "None",
    label: "No genera pendiente",
    summary: "Sin documento pendiente",
    help: "No se genera ningún documento intermedio en espera de autorización.",
    badgeVariant: "neutral",
  },
  {
    value: "GenerateOnCreate",
    label: "Genera pendiente al crear",
    summary: "Pendiente al crear",
    help: "Se genera un documento pendiente apenas se crea, antes de cualquier otra acción.",
    badgeVariant: "info",
  },
  {
    value: "GenerateBeforeConfirmation",
    label: "Genera pendiente antes de confirmar",
    summary: "Pendiente antes de confirmar",
    help: "Se genera un documento pendiente justo antes de poder confirmarse.",
    badgeVariant: "info",
  },
];

export const CANCELLATION_MODE_OPTIONS: ModeOption<CancellationMode>[] = [
  {
    value: "NotAllowed",
    label: "No permite anulación",
    summary: "Sin anulación",
    help: "Este documento no se puede anular desde el flujo.",
    badgeVariant: "neutral",
  },
  {
    value: "AllowedBeforeConfirmation",
    label: "Anulación antes de confirmar",
    summary: "Anulación antes de confirmar",
    help: "Solo se puede anular mientras no esté confirmado.",
    badgeVariant: "info",
  },
  {
    value: "AllowedAfterConfirmationWithReversal",
    label: "Anulación con reverso",
    summary: "Anulación con reverso",
    help: "Permite anular después de confirmado y reversa sus efectos (CxP/asiento/inventario).",
    badgeVariant: "warning",
  },
];

export const PAYABLE_GENERATION_MODE_OPTIONS: ModeOption<PayableGenerationMode>[] = [
  {
    value: "None",
    label: "No genera CxP",
    summary: "No genera CxP",
    help: "Este documento nunca genera una cuenta por pagar.",
    badgeVariant: "neutral",
  },
  {
    value: "OnConfirmation",
    label: "Genera CxP al confirmar",
    summary: "CxP al confirmar",
    help: "La cuenta por pagar se crea en el momento de confirmar el documento.",
    badgeVariant: "info",
  },
  {
    value: "OnAuthorization",
    label: "Genera CxP al autorizar",
    summary: "CxP al autorizar",
    help: "La cuenta por pagar se crea en el momento de autorizar el documento.",
    badgeVariant: "info",
  },
];

export const ACCOUNTING_POSTING_MODE_OPTIONS: ModeOption<AccountingPostingMode>[] = [
  {
    value: "None",
    label: "No genera asiento",
    summary: "No genera asiento",
    help: "Este documento nunca genera un asiento contable.",
    badgeVariant: "neutral",
  },
  {
    value: "OnConfirmation",
    label: "Genera asiento al confirmar",
    summary: "Asiento al confirmar",
    help: "El asiento contable se genera en el momento de confirmar el documento.",
    badgeVariant: "info",
  },
  {
    value: "OnAuthorization",
    label: "Genera asiento al autorizar",
    summary: "Asiento al autorizar",
    help: "El asiento contable se genera en el momento de autorizar el documento.",
    badgeVariant: "info",
  },
];

export const INVENTORY_IMPACT_MODE_OPTIONS: ModeOption<InventoryImpactMode>[] = [
  {
    value: "None",
    label: "No afecta inventario",
    summary: "No afecta inventario",
    help: "Este documento nunca mueve existencias.",
    badgeVariant: "neutral",
  },
  {
    value: "OnConfirmation",
    label: "Afecta inventario al confirmar",
    summary: "Inventario al confirmar",
    help: "El inventario se actualiza en el momento de confirmar el documento.",
    badgeVariant: "info",
  },
  {
    value: "OnAuthorization",
    label: "Afecta inventario al autorizar",
    summary: "Inventario al autorizar",
    help: "El inventario se actualiza en el momento de autorizar el documento.",
    badgeVariant: "info",
  },
];

export const NOTIFICATION_MODE_OPTIONS: ModeOption<NotificationMode>[] = [
  {
    value: "None",
    label: "No notifica",
    summary: "Sin notificaciones",
    help: "No se envía ninguna notificación para este documento.",
    badgeVariant: "neutral",
  },
  {
    value: "OnPendingAuthorization",
    label: "Notifica al quedar pendiente",
    summary: "Notifica al quedar pendiente",
    help: "Se notifica cuando el documento queda a la espera de autorización.",
    badgeVariant: "info",
  },
  {
    value: "OnConfirmation",
    label: "Notifica al confirmar",
    summary: "Notifica al confirmar",
    help: "Se notifica cuando el documento se confirma.",
    badgeVariant: "info",
  },
  {
    value: "OnCancellation",
    label: "Notifica al anular",
    summary: "Notifica al anular",
    help: "Se notifica cuando el documento se anula.",
    badgeVariant: "info",
  },
];

function findOption<T extends string>(options: ModeOption<T>[], value: T): ModeOption<T> {
  return options.find((o) => o.value === value) ?? options[0];
}

export const creationModeOption = (value: CreationMode) =>
  findOption(CREATION_MODE_OPTIONS, value);
export const confirmationModeOption = (value: ConfirmationMode) =>
  findOption(CONFIRMATION_MODE_OPTIONS, value);
export const authorizationModeOption = (value: AuthorizationMode) =>
  findOption(AUTHORIZATION_MODE_OPTIONS, value);
export const pendingDocumentModeOption = (value: PendingDocumentMode) =>
  findOption(PENDING_DOCUMENT_MODE_OPTIONS, value);
export const cancellationModeOption = (value: CancellationMode) =>
  findOption(CANCELLATION_MODE_OPTIONS, value);
export const payableGenerationModeOption = (value: PayableGenerationMode) =>
  findOption(PAYABLE_GENERATION_MODE_OPTIONS, value);
export const accountingPostingModeOption = (value: AccountingPostingMode) =>
  findOption(ACCOUNTING_POSTING_MODE_OPTIONS, value);
export const inventoryImpactModeOption = (value: InventoryImpactMode) =>
  findOption(INVENTORY_IMPACT_MODE_OPTIONS, value);
export const notificationModeOption = (value: NotificationMode) =>
  findOption(NOTIFICATION_MODE_OPTIONS, value);

// ── Flags booleanos ────────────────────────────────────────────────────────
// Nunca "Puede X" / "Permite X" — eso es lenguaje de permiso, no de flujo documental.

export interface BooleanFlagLabel {
  onLabel: string;
  offLabel: string;
  description: string;
}

export const IS_ACTIVE_FLAG: BooleanFlagLabel = {
  onLabel: "Activo",
  offLabel: "Inactivo",
  description: "Si está inactivo, este tipo de documento no puede usarse.",
};

export const REQUIRES_CANCELLATION_REASON_FLAG: BooleanFlagLabel = {
  onLabel: "Motivo de anulación obligatorio",
  offLabel: "Motivo no obligatorio",
  description: "Exige registrar un motivo al anular el documento.",
};

export const REQUIRES_ATTACHMENT_FLAG: BooleanFlagLabel = {
  onLabel: "Adjunto obligatorio",
  offLabel: "Adjunto opcional",
  description: "Exige un archivo adjunto para el documento.",
};

export const REQUIRES_SUPPLIER_FLAG: BooleanFlagLabel = {
  onLabel: "Proveedor obligatorio",
  offLabel: "Proveedor opcional",
  description: "Exige asociar un proveedor al documento.",
};

export const REQUIRES_DUE_DATE_FLAG: BooleanFlagLabel = {
  onLabel: "Vencimiento obligatorio",
  offLabel: "Vencimiento opcional",
  description: "Exige registrar una fecha de vencimiento.",
};

// ── Nombre de documento (fallback frontend-only) ────────────────────────────
// El backend (DocType.Name) ya trae nombres en español ("Documento de Gasto", etc.) —
// este mapa es solo un respaldo si algún día llega un documentTypeName técnico.

const DOCUMENT_TYPE_DISPLAY_NAMES: Record<string, string> = {
  ExpenseDocument: "Documento de Gasto",
  SalesInvoice: "Factura de Venta",
  PurchaseInvoice: "Factura de Compra",
  PurchaseCreditNote: "Nota de Crédito de Compra",
  SalesCreditNote: "Nota de Crédito de Venta",
  ManualJournalEntry: "Asiento Contable Manual",
  InventoryAdjustment: "Ajuste de Inventario",
  CustomerPayment: "Cobro a Cliente",
  CustomerCollection: "Cobro a Cliente",
  SupplierPayment: "Pago a Proveedor",
  ExpenseRetention: "Retención en Gasto",
  ExpenseWithholding: "Retención en Gasto",
};

/** `backendName` (DocType.Name) manda si ya viene en español; el mapa es solo respaldo. */
export function documentTypeDisplayName(documentTypeCode: string, backendName: string): string {
  if (backendName && !/^[A-Za-z]+$/.test(backendName)) return backendName;
  if (backendName && backendName.trim().length > 0 && backendName !== documentTypeCode)
    return backendName;
  return DOCUMENT_TYPE_DISPLAY_NAMES[documentTypeCode] ?? backendName ?? documentTypeCode;
}

// ── Agrupación visual por categoría (inferida de forma segura desde el código
// fijo de DocType — ver ERP.Domain.Modules.DocTypes.Constants.DocTypeCodes en backend.
// Nunca inventada: si un código nuevo no está aquí, cae a "Otros" sin romper nada) ────

export type DocumentCategory =
  | "Ventas"
  | "Compras"
  | "Gastos"
  | "Inventario"
  | "Contabilidad"
  | "Tesorería"
  | "Otros";

const DOCUMENT_CATEGORY_BY_CODE: Record<string, DocumentCategory> = {
  FACVEN: "Ventas",
  NCVDEV: "Ventas",
  FACCOM: "Compras",
  NCCDEV: "Compras",
  GASDOC: "Gastos",
  RETGAS: "Gastos",
  AJUINV: "Inventario",
  ASI: "Contabilidad",
  PAGPRO: "Tesorería",
  COBCLI: "Tesorería",
};

export function documentCategory(documentTypeCode: string): DocumentCategory {
  return DOCUMENT_CATEGORY_BY_CODE[documentTypeCode] ?? "Otros";
}

const CATEGORY_SORT_ORDER: DocumentCategory[] = [
  "Ventas",
  "Compras",
  "Gastos",
  "Inventario",
  "Contabilidad",
  "Tesorería",
  "Otros",
];

export function compareDocumentCategory(a: DocumentCategory, b: DocumentCategory): number {
  return CATEGORY_SORT_ORDER.indexOf(a) - CATEGORY_SORT_ORDER.indexOf(b);
}
