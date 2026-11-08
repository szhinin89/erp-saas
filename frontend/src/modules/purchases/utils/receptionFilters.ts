import type {
  ProcessingStatus,
  PurchaseReceptionItem,
} from "../api/purchaseReceptionService";
import { normalizeSearchText } from "../../../components/zh/inputs/searchSelectFilter";
import { receptionErpRecordView, receptionSummaryBucket } from "./receptionErpRecord";
import type { ReceptionDocumentBucket } from "./receptionNormalize";

/**
 * PURCHASES-RECEPTION-FILTERS-01 — criterios de filtro de `/purchases/reception`, funciones puras
 * sobre las filas ya importadas (el TXT llega completo; no hay filtrado de servidor). La situación
 * ERP sale SIEMPRE de `receptionErpRecordView` / `receptionSummaryBucket`: no existe una segunda
 * definición de "registrado".
 */

/** Estado de la consulta XML que solo existe en esta sesión del navegador (no se persiste). */
export type ReceptionXmlSessionState = "loading" | "error";
export type ReceptionXmlSessionMap = Readonly<Record<string, ReceptionXmlSessionState>>;

/**
 * XML del comprobante según los datos disponibles: `stored` = el backend ya guardó el XML
 * autorizado (el documento salió de IMPORTED). `loading`/`error` son solo de esta sesión: el
 * backend no registra intentos fallidos, así que tras recargar vuelven a verse como `pending`.
 */
export type ReceptionXmlState = "stored" | "pending" | "loading" | "error";

export type ReceptionSupplierState = "registered" | "inactive" | "notRegistered";

/**
 * Situación ERP — ÚNICA fuente del estado de las tarjetas y del selector "Situación ERP", así nunca
 * conviven dos situaciones incompatibles. registered / toProcess / pendingSupplier = exactamente la
 * partición de las tarjetas (por comprobante); unregistered = Por procesar + Pendientes de proveedor;
 * cancelledHistory = sin registrar y con historial anulado; review = "En revisión" (documento
 * repetido con datos distintos, fuera de las tarjetas hasta resolverlo).
 */
export type ReceptionErpSituation =
  | "all"
  | "registered"
  | "toProcess"
  | "pendingSupplier"
  | "unregistered"
  | "cancelledHistory"
  | "review";

/** Situaciones que corresponden 1:1 a una tarjeta de resumen. */
export const RECEPTION_SUMMARY_SITUATIONS = ["registered", "toProcess", "pendingSupplier"] as const;

/** Estados de recepción que el backend emite hoy (CANCELLED está modelado pero nunca se produce). */
export const RECEPTION_DOCUMENT_STATUSES = ["IMPORTED", "VERIFIED", "PROCESSED"] as const;
export const RECEPTION_PROCESSING_STATUSES = [
  "PENDING",
  "PROCESSED",
  "PROCESSED_WITH_WARNINGS",
  "FAILED",
] as const satisfies readonly ProcessingStatus[];
export const RECEPTION_DOC_TYPES = ["INVOICE", "CREDIT_NOTE"] as const;

export interface ReceptionFilters {
  /** RUC, proveedor, número de comprobante o clave de acceso. */
  search: string;
  /** Selector de proveedores existente (RUC tal como viene en la fila). */
  supplierRucs: string[];
  erpSituation: ReceptionErpSituation;
  supplierState: "all" | ReceptionSupplierState;
  xmlState: "all" | ReceptionXmlState;
  documentStatus: "all" | (typeof RECEPTION_DOCUMENT_STATUSES)[number];
  processingStatus: "all" | ProcessingStatus;
  docType: "all" | (typeof RECEPTION_DOC_TYPES)[number];
  /** Fechas de negocio "YYYY-MM-DD" (ADR-034); "" = sin límite. */
  issueDateFrom: string;
  issueDateTo: string;
  /** Texto del input de moneda (punto decimal); "" = sin límite. */
  totalMin: string;
  totalMax: string;
  attentionOnly: boolean;
}

export const EMPTY_RECEPTION_FILTERS: ReceptionFilters = {
  search: "",
  supplierRucs: [],
  erpSituation: "all",
  supplierState: "all",
  xmlState: "all",
  documentStatus: "all",
  processingStatus: "all",
  docType: "all",
  issueDateFrom: "",
  issueDateTo: "",
  totalMin: "",
  totalMax: "",
  attentionOnly: false,
};

/** Criterios de la zona plegable (para el contador del botón "Más filtros"). */
const ADVANCED_KEYS = [
  "erpSituation",
  "supplierState",
  "xmlState",
  "documentStatus",
  "processingStatus",
  "docType",
  "issueDateFrom",
  "issueDateTo",
  "totalMin",
  "totalMax",
] as const satisfies readonly (keyof ReceptionFilters)[];

export function countActiveAdvancedFilters(filters: ReceptionFilters): number {
  return ADVANCED_KEYS.filter((key) => filters[key] !== EMPTY_RECEPTION_FILTERS[key]).length;
}

/** Mismo criterio que la columna Proveedor: en Factura además debe existir el Id resuelto. */
export function receptionSupplierState(
  row: Pick<PurchaseReceptionItem, "sourceDocType" | "supplierExists" | "supplierId" | "supplierIsActive">,
): ReceptionSupplierState {
  const registered =
    row.sourceDocType === "INVOICE" ? row.supplierExists && !!row.supplierId : row.supplierExists;
  if (!registered) return "notRegistered";
  return row.supplierIsActive === false ? "inactive" : "registered";
}

export function receptionXmlState(
  row: Pick<PurchaseReceptionItem, "documentStatus">,
  session: ReceptionXmlSessionState | undefined,
): ReceptionXmlState {
  if (row.documentStatus !== "IMPORTED") return "stored";
  return session ?? "pending";
}

/**
 * "Requieren atención": sin registro ERP activo y con algo que resolver antes de registrarlo —
 * proveedor pendiente o inactivo, XML no almacenado (pendiente, consultándose o con error en esta
 * sesión) o lectura FAILED / PROCESSED_WITH_WARNINGS. Un comprobante listo para registrar (proveedor
 * activo, XML almacenado y leído sin advertencias) NO requiere atención aunque siga sin registrar.
 */
export function receptionRequiresAttention(
  row: PurchaseReceptionItem,
  session: ReceptionXmlSessionState | undefined,
): boolean {
  if (receptionErpRecordView(row).active !== null) return false;
  return (
    receptionSupplierState(row) !== "registered" ||
    receptionXmlState(row, session) !== "stored" ||
    row.processingStatus === "FAILED" ||
    row.processingStatus === "PROCESSED_WITH_WARNINGS"
  );
}

/** Monto del filtro: "" o texto no numérico = sin límite. */
export function parseReceptionFilterAmount(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed === "") return null;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}

function matchesErpSituation(
  row: PurchaseReceptionItem,
  situation: ReceptionErpSituation,
  bucketOf: (row: PurchaseReceptionItem) => ReceptionDocumentBucket,
): boolean {
  if (situation === "all") return true;
  const bucket = bucketOf(row);
  if (situation === "unregistered" || situation === "cancelledHistory") {
    if (bucket !== "toProcess" && bucket !== "pendingSupplier") return false;
    return situation === "unregistered" || receptionErpRecordView(row).history;
  }
  return bucket === situation;
}

function matchesSearch(row: PurchaseReceptionItem, needle: string): boolean {
  if (needle === "") return true;
  return [row.supplierRuc, row.supplierName, row.invoiceNumber, row.accessKey].some((value) =>
    normalizeSearchText(value ?? "").includes(needle),
  );
}

/**
 * Prepara los filtros una sola vez (texto normalizado, montos, RUC) y devuelve el predicado que se
 * aplica a cada fila. Todos los criterios se combinan con Y lógico. `bucketOf` = tarjeta resuelta
 * por comprobante (ver `resolveReceptionDocumentBuckets`), para que filtrar una tarjeta muestre
 * exactamente lo que cuenta.
 */
export function buildReceptionFilterPredicate(
  filters: ReceptionFilters,
  session: ReceptionXmlSessionMap,
  bucketOf: (row: PurchaseReceptionItem) => ReceptionDocumentBucket = receptionSummaryBucket,
): (row: PurchaseReceptionItem) => boolean {
  const needle = normalizeSearchText(filters.search);
  const rucs = new Set(filters.supplierRucs);
  const totalMin = parseReceptionFilterAmount(filters.totalMin);
  const totalMax = parseReceptionFilterAmount(filters.totalMax);

  return (row) => {
    const sessionState = session[row.documentId];
    if (rucs.size > 0 && !rucs.has(row.supplierRuc)) return false;
    if (!matchesSearch(row, needle)) return false;
    if (!matchesErpSituation(row, filters.erpSituation, bucketOf)) return false;
    if (filters.supplierState !== "all" && receptionSupplierState(row) !== filters.supplierState)
      return false;
    if (filters.xmlState !== "all" && receptionXmlState(row, sessionState) !== filters.xmlState)
      return false;
    if (filters.documentStatus !== "all" && row.documentStatus !== filters.documentStatus)
      return false;
    if (filters.processingStatus !== "all" && row.processingStatus !== filters.processingStatus)
      return false;
    if (filters.docType !== "all" && row.sourceDocType !== filters.docType) return false;
    if (filters.issueDateFrom && row.issueDate < filters.issueDateFrom) return false;
    if (filters.issueDateTo && row.issueDate > filters.issueDateTo) return false;
    if (totalMin !== null && row.total < totalMin) return false;
    if (totalMax !== null && row.total > totalMax) return false;
    if (filters.attentionOnly && !receptionRequiresAttention(row, sessionState)) return false;
    return true;
  };
}
