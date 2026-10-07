import { collectSalesLineIssues, mapSalesServerLineIssues, focusSalesLineIssue, salesLineCorrectionSummary, type SalesLineIssue } from "../utils/salesLineIssues";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import axios from "axios";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import type {
  SalesInvoiceDto,
  SalesListItemDto,
  CardDetailInput,
  TransferDetailInput,
  ChequeDetailInput,
} from "../api/salesService";
import { salesService } from "../api/salesService";
import {
  paymentMethodService,
  type PaymentMethodDto,
  type PaymentMethodDetailType,
} from "../api/paymentMethodService";
import { warehouseLookupFacade } from "../../inventory/facades/warehouseLookupFacade";
import type { WarehouseDto } from "../../inventory/facades/warehouseLookupFacade";
import { stockLookupFacade } from "../../inventory/facades/stockLookupFacade";
import type { ItemWarehouseAvailabilityDto } from "../../inventory/facades/stockLookupFacade";
import { electronicDocumentAccessFacade } from "../../electronicDocuments/facades/electronicDocumentAccessFacade";
import type { ElectronicDocumentXmlVariant } from "../../electronicDocuments/facades/electronicDocumentAccessFacade";
import { downloadTextFile } from "../../../lib/download";
import type { InvoiceItemSearchResultDto } from "../api/invoiceItemSearchService";
import {
  customerLookupFacade,
  type CustomerPickerRow,
} from "../../masterData/facades/customerLookupFacade";
import { businessPartnerLookupFacade } from "../../masterData/facades/businessPartnerLookupFacade";
import {
  businessPartnerRegistrationFacade,
  RoleTypeEnum,
  type LocationTypeValue,
  type ContactRoleValue,
} from "../../masterData/facades/businessPartnerRegistrationFacade";
import { paymentTermLookupFacade } from "../../masterData/facades/paymentTermLookupFacade";
import type { PaymentTermDto } from "../../masterData/facades/paymentTermLookupFacade";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import { salesDefaultsService } from "../api/salesDefaultsService";
import type { SalesInvoiceDefaultsDto } from "../api/salesDefaultsService";
import { salesRuntimeContextService } from "../api/salesRuntimeContextService";
import type { SalesRuntimeContextDto } from "../api/salesRuntimeContextService";
import { salesItemPricingService } from "../api/salesItemPricingService";
import {
  useSalesCustomerRepricing,
  applyRepricingPlanToLines,
  mapResolvedPricingToLineFields,
} from "./useSalesCustomerRepricing";
import { bankAccountLookupFacade } from "../../finance/facades/bankAccountLookupFacade";
import type { CompanyBankAccountDto } from "../../finance/facades/bankAccountLookupFacade";
import { bankLookupFacade } from "../../settings/banks/facades/bankLookupFacade";
import type { BankDto } from "../../settings/banks/facades/bankLookupFacade";
import {
  loadPrecisionPolicy,
  getPrecisionPolicy,
} from "../../../lib/config/precisionPolicy.config";
import {
  todayIso,
  addDaysIso,
} from "../../../lib/formatters/dateFormatters";
import { normalizeOptionalCode } from "../../../lib/sanitizers";
import { isEditableTarget } from "../../../lib/inputUtils";
import { mapInvoiceLinesToFormValues } from "../utils/salesInvoiceHydration";
import { useCustomerPriceListContext } from "./useCustomerPriceListContext";
import { resolvePriceListHeader } from "../utils/pricingTraceability";
import {
  readApiErrorMessage,
  readApiErrorMessages,
  logApiDevError,
} from "../../lib/apiError";
import {
  calcSummary,
  resolveInvoicedUnitPriceEdit,
  formatVatLabel,
  lineExceedsStock,
  findMergeableLineIndex,
  resolveDefaultLinePresentation,
  resolveLinePresentationChange,
  type TaxBreakdownEntry,
} from "../utils/salesCalc";
import { cajaSessionLookupFacade } from "../../caja/facades/cajaSessionLookupFacade";
import type { CashSessionDto } from "../../caja/facades/cajaSessionLookupFacade";
import { useManualCashMovementFlow } from "../../caja/facades/manualCashMovementFacade";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { useElectronicInvoicingStatusStore } from "../../../store/electronicInvoicingStatusStore";
import type { ElectronicInvoicingStatusDto } from "../../configuracion/facades/electronicInvoicingLookupFacade";
import { message } from "../../../lib/messages";
import {
  salesInvoiceSchema,
  emptySalesInvoiceForm,
  type SalesInvoiceFormValues,
  type SalesLineFormValues,
  type SalesPaymentFormValues,
} from "../schemas/salesInvoiceSchema";
import {
  basePaymentsForAdditionalMethod,
  computeSalesCollectionStatus,
  parseCashReceivedInput,
  syncCashOnlyAppliedAmount,
} from "../utils/salesCollectionStatus";
import { resolveSalesEmissionType } from "../utils/salesEmissionType";
import { tenderedForPayment } from "../utils/salesCashTendered";
import { computeSalesConfigStatus } from "../utils/salesEmissionConfigStatus";

// ── Helpers ────────────────────────────────────────────────────────────

/**
 * Los detalles de pago (tarjeta/transferencia/cheque) llegan del backend con campos
 * `string | null`, pero el formulario (RHF + Zod) los modela como `string | undefined`
 * (campos opcionales sin registrar, no "explícitamente vacíos"). Normaliza null→undefined
 * campo a campo al precargar un pago existente en el formulario.
 */
function nullFieldsToUndefined<T extends object>(
  obj: T,
): { [K in keyof T]: Exclude<T[K], null> | undefined } {
  const out = {} as { [K in keyof T]: Exclude<T[K], null> | undefined };
  for (const key of Object.keys(obj) as (keyof T)[]) {
    const value = obj[key];
    out[key] = (value === null ? undefined : value) as
      Exclude<T[typeof key], null> | undefined;
  }
  return out;
}

/**
 * Motivo por el que GET /cash-sessions/my no pudo confirmarse — nunca se confunde con "no hay
 * caja abierta" (esa es una respuesta 200 exitosa con `null`). Distingue los tres casos reales
 * de falla para que la UI no muestre "no tiene caja" cuando en realidad no se pudo verificar:
 * - "permission": 403 sin código de scope — falta el permiso `caja.view` en el rol del usuario.
 * - "context": 403 con `BRANCH_SCOPE_FORBIDDEN`/`COMPANY_SCOPE_FORBIDDEN` — falta contexto de
 *   empresa/sucursal activa (`BranchScopeBehavior`/`CompanyScopeBehavior`, backend).
 * - "server": cualquier otro caso (500, sin respuesta/red, etc.).
 */
export type CashSessionCheckErrorReason = "permission" | "context" | "server";

const CASH_SESSION_SCOPE_ERROR_CODES = new Set([
  "BRANCH_SCOPE_FORBIDDEN",
  "COMPANY_SCOPE_FORBIDDEN",
]);

function classifyCashSessionCheckError(err: unknown): CashSessionCheckErrorReason {
  if (!axios.isAxiosError(err) || !err.response) return "server";
  if (err.response.status === 403) {
    const body = err.response.data as { code?: string } | undefined;
    if (body?.code && CASH_SESSION_SCOPE_ERROR_CODES.has(body.code))
      return "context";
    return "permission";
  }
  return "server";
}

// ── Types ──────────────────────────────────────────────────────────────

export type Tab = "listado" | "nuevo";

export type CustomerProfile = {
  name: string;
  taxId: string;
  identificationType: string;
  email: string | null;
  phone: string | null;
  address: string | null;
  paymentTermId: string | null;
};

export type CreditRow = { number: number; dueDate: string; amount: number };

// ── Flujo de emisión ─────────────────────────────────────────────────────
// Único modal de "Nueva Venta → Emitir Factura": recorre sus fases dentro de
// la misma instancia (nunca abre un segundo modal distinto). 'error' solo se
// alcanza por fallas de infraestructura (interno/comunicación) — un error de
// validación (cliente o servidor) nunca llega aquí: vuelve a 'idle' y se
// muestra inline en el formulario (F-V3/F-V4 — estándar de validación del
// repo), porque ahí es donde el usuario puede corregirlo.
export type IssuePhase =
  "idle" | "confirm" | "processing" | "success" | "error";
export type IssueErrorKind = "internal" | "communication";
export type IssueErrorInfo = { kind: IssueErrorKind; message: string };

/**
 * POS-F8-CASH-INPUT-01: atributo de opt-in para el atajo de emisión. Un control editable que lo
 * declara (hoy solo "Efectivo recibido" del cobro inline) permite F8 aunque tenga el foco: es el
 * último dato que escribe el cajero y obligarlo a salir del campo antes de emitir era una regla
 * implícita imposible de adivinar. El resto de controles editables (buscador de productos,
 * modales como "Crear Cliente") siguen bloqueando F8 — ver SALES-QUICK-CUSTOMER-MODAL-INPUT-FIX-07.
 */
export const POS_EMIT_SHORTCUT_ATTR = "data-pos-emit-shortcut";

/**
 * SALES-QUICK-CUSTOMER-MODAL-FIX-07A: decisión pura del atajo global F8 ("Emitir Factura"),
 * extraída del listener de teclado para poder testearla sin montar todo useSalesPage. No
 * dispara mientras el foco está en un control editable (p. ej. un modal abierto sobre la
 * página) — ver SALES-QUICK-CUSTOMER-MODAL-INPUT-FIX-07 — salvo los controles que declaran
 * explícitamente `POS_EMIT_SHORTCUT_ATTR` (POS-F8-CASH-INPUT-01).
 */
export function shouldTriggerF8Emit(
  e: Pick<KeyboardEvent, "key" | "target">,
  ctx: { tab: Tab; issuePhase: IssuePhase; canEmit: boolean },
): boolean {
  if (e.key !== "F8") return false;
  const optedIn =
    e.target instanceof HTMLElement && e.target.getAttribute(POS_EMIT_SHORTCUT_ATTR) === "true";
  if (isEditableTarget(e.target) && !optedIn) return false;
  if (ctx.tab !== "nuevo" || ctx.issuePhase !== "idle" || !ctx.canEmit) return false;
  return true;
}

/** Motivo bloqueante de emisión — `source` dice qué zona de la pantalla ya muestra el detalle. */
export type EmitBlockerSource =
  | "customer"
  | "lines"
  | "stock"
  | "cashSession"
  | "config"
  | "consumerFinal"
  | "payment"
  | "document";
export type EmitBlocker = { source: EmitBlockerSource; message: string };

/** Aviso de error accionable para el formulario de ventas: título contextual (qué acción
 * falló, p. ej. "No se puede emitir la factura.") + detalle. El detalle prioriza los
 * `data.errors` específicos del backend (todos, no solo el primero) sobre el mensaje genérico
 * `message.user` del catálogo — ver `readApiErrorMessages` (single source of truth compartida,
 * ya usada en el resto del ERP). `message.dev` nunca llega al usuario: solo se registra en
 * consola vía `logApiDevError`. */
export type SalesErrorNotice = { title: string; detail: string };

/** Todos los `data.errors` del backend unidos en un solo texto legible cuando existen (nunca
 * solo el primero); si no hay ninguno, cae a `message.user` y luego al fallback local. */
export function extractErrorText(err: unknown, fallback: string): string {
  logApiDevError(err);
  const details = readApiErrorMessages(err);
  if (details.length > 0) return details.join(" • ");
  return readApiErrorMessage(err) ?? fallback;
}

export function buildSalesErrorNotice(
  err: unknown,
  title: string,
  fallback: string,
): SalesErrorNotice {
  return { title, detail: extractErrorText(err, fallback) };
}

function issueErrorStatus(e: unknown): number | undefined {
  return (e as { response?: { status?: number } })?.response?.status;
}

/** Mensaje mostrado al cajero cuando el SRI no responde justo antes de emitir. No bloquea la
 * emisión (ver `SRI_UNAVAILABLE_ISSUE_WARNING` / uso en `confirmIssue`): el backend
 * (`AuthorizeSalesInvoiceHandler`) ya autoriza la venta y reintenta el envío al SRI de forma
 * tolerante, así que este aviso es informativo, no un error que revierta el flujo. */
export const SRI_UNAVAILABLE_ISSUE_WARNING =
  "No se pudo conectar con el SRI. Intente nuevamente o revise la configuración.";

/** ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01: true cuando el último check de
 * conectividad SRI (ya resuelto por `electronicInvoicingStatusStore.refreshConnectivity`, nunca
 * recalculado acá) indica que el servicio no respondió. Pura — no decide bloquear ni recalcula
 * nada, solo lee el campo ya calculado por el backend. */
export function shouldWarnSriUnavailable(
  status: ElectronicInvoicingStatusDto | null,
): boolean {
  return status?.sriAvailability === "Unavailable";
}

/** Identificación SRI estándar (tipo 07) del "Consumidor Final" — mismo literal que
 * `TaxIdentification.ConsumidorFinalNumber` en el backend (dominio), sembrado en cada
 * tenant por `SalesBootstrapStep`. No es un dato de negocio tenant-específico: es un
 * valor regulatorio fijo del SRI, usado aquí solo como clave de búsqueda. */
const CONSUMIDOR_FINAL_IDENTIFICATION_NUMBER = "9999999999999";

/** Fallback universal de cliente para venta mostrador: resuelve el Consumidor Final ya
 * sembrado del tenant vía el buscador de clientes existente (no crea nada, no inventa
 * datos). Devuelve null si el tenant no lo tiene sembrado — el llamador debe manejarlo
 * dejando el campo cliente vacío para selección manual, nunca fallando la pantalla. */
async function resolveConsumidorFinal(): Promise<CustomerPickerRow | null> {
  try {
    const rows = await customerLookupFacade.searchCustomers(
      CONSUMIDOR_FINAL_IDENTIFICATION_NUMBER,
    );
    return (
      rows.find(
        (r) => r.identificationNumber === CONSUMIDOR_FINAL_IDENTIFICATION_NUMBER,
      ) ?? null
    );
  } catch {
    return null;
  }
}

/** POS-CASH-TENDERED-01: forma de cobro de efectivo físico = PaymentMethod.affectsPhysicalCash —
 * el MISMO criterio con el que el backend acepta TenderedAmount y mueve el cajón de Caja
 * (antes se comparaba el código "EFECTIVO", que el tenant puede editar). Habilita "Efectivo
 * recibido / Vuelto"; nunca decide el importe aplicado. */
function isCashPaymentMethod(pm: PaymentMethodDto | undefined): boolean {
  return !!pm && pm.affectsPhysicalCash;
}

// SALES-TRANSFER-BANK-ACCOUNT-01: etiquetas legibles de BankAccountType para el selector de
// cuenta bancaria destino en Transferencia.
const BANK_ACCOUNT_TYPE_LABELS: Record<string, string> = {
  Checking: "Corriente",
  Savings: "Ahorros",
  Other: "Otro",
};

// ── Hook ───────────────────────────────────────────────────────────────

export function useSalesPage() {
  // ── Page state ─────────────────────────────────────────────────────
  const [tab, setTab] = useState<Tab>("nuevo");
  const [listItems, setListItems] = useState<SalesListItemDto[]>([]);
  const [listLoading, setListLoading] = useState(false);
  const [listSearch, setListSearch] = useState("");

  const [saving, setSaving] = useState(false);
  const [serverLineIssues, setServerLineIssues] = useState<SalesLineIssue[]>([]);
  const [pendingLineFocus, setPendingLineFocus] = useState<SalesLineIssue | null>(null);
  const [saveError, setSaveError] = useState<SalesErrorNotice | null>(null);
  const [editing, setEditing] = useState<SalesInvoiceDto | null>(null);
  // undefined = todavía cargando (o no se pudo verificar, ver cashSessionCheckError); null =
  // confirmado por el backend (200 OK) que no hay caja abierta.
  const [myCashSession, setMyCashSession] = useState<
    CashSessionDto | null | undefined
  >(undefined);
  const hasCashSession =
    myCashSession === undefined ? null : myCashSession !== null;
  // Motivo si GET /cash-sessions/my falló (permiso/contexto/servidor) — `null` mientras no haya
  // habido un error, o tras una verificación exitosa. Nunca implica "no hay caja": eso solo lo
  // dice `hasCashSession === false` (200 OK real).
  const [cashSessionCheckError, setCashSessionCheckError] =
    useState<CashSessionCheckErrorReason | null>(null);

  const checkCashSession = useCallback(async (): Promise<CashSessionDto | null> => {
    try {
      const s = await cajaSessionLookupFacade.getMy();
      setMyCashSession(s);
      setCashSessionCheckError(null);
      return s;
    } catch (err) {
      // No se pudo confirmar — se deja en "sin verificar" (undefined) en vez de `null`, que
      // significaría "confirmado que no hay caja" y dispararía el aviso equivocado.
      setMyCashSession(undefined);
      setCashSessionCheckError(classifyCashSessionCheckError(err));
      return null;
    }
  }, []);

  const refreshCashSession = useCallback(() => {
    setMyCashSession(undefined);
    setCashSessionCheckError(null);
    void checkCashSession();
  }, [checkCashSession]);

  const branchName = useActiveBranchStore((s) => s.branch)?.name ?? null;

  // ── SALES-MANUAL-CASH-MOVEMENT-INTEGRATION-08 — reutiliza tal cual el flujo de movimiento
  // manual ya construido para Caja (mismo hook, mismo endpoint, mismo permiso `caja.record`,
  // misma configuración de empresa AllowManualInOutMovements, mismo catálogo CashMovementReason).
  // "Turno abierto" aquí es exactamente `myCashSession` (GET /cash-sessions/my ya resuelto arriba
  // vía cajaSessionLookupFacade): un valor no nulo YA significa "hay una sesión abierta para este
  // usuario" (ver comentario de `hasCashSession` arriba) — nunca se acepta un CashSessionId
  // elegido por la UI. Al registrar con éxito no se recarga nada de Ventas (el documento en curso
  // no cambia); Sales tampoco muestra hoy ningún indicador de saldo de caja que deba refrescarse.
  const manualCashMovement = useManualCashMovementFlow({
    cashSessionId: myCashSession?.id ?? null,
    isSessionOpen: myCashSession?.status === "Open",
  });

  // ── Customer state ─────────────────────────────────────────────────
  const [customerProfile, setCustomerProfile] =
    useState<CustomerProfile | null>(null);

  // ── Reference data ─────────────────────────────────────────────────
  const [paymentTermsList, setPaymentTermsList] = useState<PaymentTermDto[]>(
    [],
  );
  const [paymentMethods, setPaymentMethods] = useState<PaymentMethodDto[]>([]);
  // SALES-TRANSFER-BANK-ACCOUNT-01: cuentas bancarias activas de la empresa, para el selector de
  // Transferencia en PaymentDetailModal — reemplaza el texto libre "Banco".
  const [bankAccounts, setBankAccounts] = useState<CompanyBankAccountDto[]>([]);
  const [banks, setBanks] = useState<BankDto[]>([]);
  // SALES-TRANSFER-BANK-ACCOUNT-01: "Banco + alias + tipo/número enmascarado" — el número de
  // cuenta nunca se muestra completo en el selector de Transferencia (solo los últimos 4 dígitos).
  const bankAccountOptions = useMemo(
    () =>
      bankAccounts.map((a) => {
        const bankName = banks.find((b) => b.id === a.bankId)?.name ?? a.bankId;
        const type = BANK_ACCOUNT_TYPE_LABELS[a.accountType] ?? a.accountType;
        const last4 = a.accountNumber.slice(-4);
        const masked = a.accountNumber.length > 4 ? `****${last4}` : a.accountNumber;
        return {
          id: a.id,
          label: `${bankName} — ${a.displayName} — ${type} ${masked}`,
        };
      }),
    [bankAccounts, banks],
  );
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [selectedWarehouseId, setSelectedWarehouseId] = useState("");
  const [vatRatesMap, setVatRatesMap] = useState<Record<string, number>>({});
  const [sriDocTypes, setSriDocTypes] = useState<
    { code: string; name: string }[]
  >([]);
  const [sriPaymentMethods, setSriPaymentMethods] = useState<
    { code: string; name: string }[]
  >([]);
  const [sriIdTypes, setSriIdTypes] = useState<
    { code: string; name: string }[]
  >([]);
  const [tenantDefaults, setTenantDefaults] =
    useState<SalesInvoiceDefaultsDto | null>(null);
  const [iceRatesMap, setIceRatesMap] = useState<Record<string, number>>({});
  // Política fiscal de Consumidor Final + defaults, resuelta por el backend (autoridad única).
  // No reemplaza la validación del backend al emitir — solo previene en UI.
  const [runtimeContext, setRuntimeContext] =
    useState<SalesRuntimeContextDto | null>(null);

  // ── Modal state ────────────────────────────────────────────────────
  const [modalCancelReason, setModalCancelReason] = useState(false);
  const [modalNewCustomer, setModalNewCustomer] = useState(false);
  const [modalDetail, setModalDetail] = useState(false);
  const [modalCredit, setModalCredit] = useState(false);

  // ── Issue flow state (Nueva Venta → Emitir Factura) ────────────────
  const issueInFlightRef = useRef(false);
  const lineLoadVersionRef = useRef(0);
  const warehouseLoadVersionRef = useRef(0);
  const lineWarehouseRequestsRef = useRef(new Map<number, object>());
  useEffect(() => () => { ++lineLoadVersionRef.current; ++warehouseLoadVersionRef.current; }, []);
  const [issuePhase, setIssuePhase] = useState<IssuePhase>("idle");
  const [issueStepIndex, setIssueStepIndex] = useState(0);
  const [issueResult, setIssueResult] = useState<SalesInvoiceDto | null>(null);
  const [issueError, setIssueError] = useState<IssueErrorInfo | null>(null);
  const [xmlDownloading, setXmlDownloading] = useState(false);
  const [productSearchFocusKey, setProductSearchFocusKey] = useState(0);

  // ── Quick customer modal state ─────────────────────────────────────
  const [newCustId, setNewCustId] = useState("");
  const [newCustName, setNewCustName] = useState("");
  const [newCustIdType, setNewCustIdType] = useState("05");
  const [newCustAddress, setNewCustAddress] = useState("");
  const [newCustEmail, setNewCustEmail] = useState("");
  const [newCustPhone, setNewCustPhone] = useState("");
  const [newCustIsEdit, setNewCustIsEdit] = useState(false);
  const [newCustSaving, setNewCustSaving] = useState(false);
  const [newCustError, setNewCustError] = useState("");

  // ── Payment detail modal state ─────────────────────────────────────
  type DetailRow = {
    _k: number;
    amount: number;
    card?: CardDetailInput;
    transfer?: TransferDetailInput;
    cheque?: ChequeDetailInput;
  };
  const [detailMethodId, setDetailMethodId] = useState("");
  const [detailMethodType, setDetailMethodType] =
    useState<PaymentMethodDetailType>("None");
  const [detailMethodName, setDetailMethodName] = useState("");
  const [detailRows, setDetailRows] = useState<DetailRow[]>([]);
  const [detailKey, setDetailKey] = useState(1);

  // ── Credit modal state ─────────────────────────────────────────────
  const [creditAmount, setCreditAmount] = useState(0);
  const [creditRows, setCreditRows] = useState<CreditRow[]>([]);
  // ADR-033, Fase 4: cronograma explícito confirmado en el simulador — se envía tal cual al
  // guardar el borrador (schedule) mientras scheduleIsManual sea true. Si el usuario nunca
  // confirma un cronograma personalizado, queda null y el backend genera uno automático a
  // partir de la condición de pago (comportamiento por defecto, sin cambios).
  const [confirmedScheduleRows, setConfirmedScheduleRows] = useState<
    CreditRow[] | null
  >(null);
  const [scheduleIsManual, setScheduleIsManual] = useState(false);

  // ── Cash payment: efectivo recibido físicamente (solo POS — el backend no exige este dato;
  // viaja a la tirilla vía el payload de impresión, ver SalesIssueModal). Texto crudo del input
  // controlado: se interpreta en cada pulsación (POS-COLLECTION-SSOT-01), nunca solo al blur.
  const [cashReceivedInput, setCashReceivedInput] = useState("");

  // ── Line key counter ───────────────────────────────────────────────
  const [lineKey, setLineKey] = useState(1);
  const [payKey, setPayKey] = useState(1);

  // ── React Hook Form ────────────────────────────────────────────────
  const form = useForm<SalesInvoiceFormValues>({
    resolver: zodResolver(salesInvoiceSchema),
    defaultValues: emptySalesInvoiceForm(),
    mode: "onBlur",
  });

  const {
    register,
    control,
    reset,
    watch,
    setValue,
    getValues,
    trigger,
    formState: { errors, isDirty },
  } = form;

  const formWatch = watch();
  const lines = watch("lines");
  const payments = watch("payments");

  // ── Derived state ──────────────────────────────────────────────────
  const isDraft = !editing || editing.status === "Draft";
  const readOnly = !isDraft;
  const fieldDisabled = saving || readOnly;

  // SALES-PRICING-UX-TRACEABILITY-07C: cabecera "Lista preferente". Editable → contexto LIVE de
  // Pricing (refresca al cambiar de cliente; un error deja estado neutro, nunca bloquea la venta).
  // Solo lectura → snapshot persistido de la factura, jamás una consulta viva.
  const liveCustomerPriceListContext = useCustomerPriceListContext(
    formWatch.customerId,
    !readOnly,
  );
  const priceListHeader = resolvePriceListHeader({
    hasCustomer: !!formWatch.customerId,
    readOnly,
    saved: editing
      ? {
          pricingTraceabilityVersion: editing.pricingTraceabilityVersion ?? null,
          customerPreferredPriceListName: editing.customerPreferredPriceListName ?? null,
        }
      : null,
    live: liveCustomerPriceListContext,
  });

  const summary = useMemo(
    () => calcSummary(lines, vatRatesMap, iceRatesMap),
    [lines, vatRatesMap, iceRatesMap],
  );

  const hasCustomer = !!formWatch.customerId?.trim();
  const hasLines = lines.length > 0;
  const lineIssues = useMemo(() => readOnly ? [] : collectSalesLineIssues(lines, serverLineIssues), [lines,serverLineIssues,readOnly]);
  const focusFirstInvalidLine = useCallback(() => { if (lineIssues[0]) focusSalesLineIssue(lineIssues[0]); }, [lineIssues]);
  useEffect(() => {
    if (pendingLineFocus) { focusSalesLineIssue(pendingLineFocus); setPendingLineFocus(null); }
  }, [pendingLineFocus]);
  // A document notice describes the rejected input, not a later corrected form.
  const issueInputSignature = JSON.stringify([formWatch, cashReceivedInput, myCashSession]);
  const previousIssueInput = useRef(issueInputSignature);
  useEffect(() => {
    if (previousIssueInput.current !== issueInputSignature) setSaveError(null);
    previousIssueInput.current = issueInputSignature;
  }, [issueInputSignature]);
  const applyIssueValidationError = useCallback((err: unknown) => {
    const mapped = mapSalesServerLineIssues(err, getValues("lines"));
    setServerLineIssues(mapped.lineIssues);
    if (mapped.lineIssues[0]) setPendingLineFocus(mapped.lineIssues[0]);
    setSaveError(mapped.globalMessages.length ? {title:"No se puede emitir la factura.",detail:mapped.globalMessages[0]} : mapped.lineIssues.length ? null : buildSalesErrorNotice(err,"No se puede emitir la factura.","Revise los datos de la factura."));
  }, [getValues]);


  // ── POS-EMISSION-TYPE-SNAPSHOT-01 — tipo de emisión EFECTIVO de la venta en pantalla ──
  // Venta nueva (sin documento): CashSession → EmissionPoint → EmissionType, resuelto en vivo
  // por el backend en myCashSession.emissionType. Venta ya creada (borrador, autorizada,
  // anulada, histórica): manda SIEMPRE el snapshot inmutable SalesInvoice.EmissionType — es el
  // mismo que usa el backend para emitir (AuthorizeSalesInvoiceHandler), el XML, el RIDE y la
  // tirilla. Antes la caja actual tenía prioridad: una factura electrónica histórica abierta
  // desde una caja física ocultaba su clave de acceso / RIDE / diagnóstico (y viceversa).
  const sessionEmissionType = myCashSession?.emissionType ?? null;
  const emissionType = resolveSalesEmissionType(editing, myCashSession);
  const isElectronic = emissionType === "Electronic";

  // ── POS-COLLECTION-SSOT-01 — estado único del cobro (ver utils/salesCollectionStatus.ts) ──
  // Tolerancia = política de settlement REAL de la empresa (CompanyPrecisionPolicy), la misma que
  // el backend usa al autorizar — no una constante propia de UI.
  const precisionPolicy = getPrecisionPolicy();
  const isCashMethodId = useCallback(
    (paymentMethodId: string) =>
      isCashPaymentMethod(paymentMethods.find((pm) => pm.id === paymentMethodId)),
    [paymentMethods],
  );
  const cashReceivedValue = parseCashReceivedInput(cashReceivedInput);
  const collection = computeSalesCollectionStatus({
    total: summary.total,
    payments,
    isCashMethod: isCashMethodId,
    cashReceived: cashReceivedValue,
    tolerance: precisionPolicy.settlementToleranceAmount,
    moneyDecimals: precisionPolicy.moneyDecimals,
  });
  const paidTotal = collection.appliedTotal;
  const paymentOk = collection.appliedOk;
  const cashDue = collection.cashApplied;
  const cashChange = collection.cashChange;

  // POS-CASH-ONLY-FOLLOWS-TOTAL-01: con un ÚNICO cobro y en Efectivo, el monto aplicado es un
  // derivado del total (el cajero solo edita "Efectivo recibido"): si cambian las líneas después
  // de cobrar, lo aplicado sigue al total y el efectivo recibido se conserva — Falta/Vuelto se
  // recalculan solos. Con 2+ formas de cobro los montos son explícitos del cajero y NUNCA se
  // redistribuyen: la diferencia se muestra como Falta / Excede.
  const cashOnlyApplied = collection.isCashOnly ? collection.cashApplied : null;
  useEffect(() => {
    if (readOnly || cashOnlyApplied === null) return;
    const next = syncCashOnlyAppliedAmount(getValues("payments"), isCashMethodId, summary.total);
    if (next) setValue("payments", next, { shouldDirty: true });
  }, [readOnly, cashOnlyApplied, summary.total, isCashMethodId, getValues, setValue]);

  // Advertencia preventiva de stock (UX) — nunca bloquea si el frontend no tiene el dato de
  // disponibilidad (_stockQty), solo anticipa el mismo resultado que ya valida el backend al
  // emitir (AuthorizeSalesUseCases). No duplica la regla de stock, solo evita el roundtrip.
  const hasInsufficientStock = useMemo(
    () => lines.some((l) => lineExceedsStock(l)),
    [lines],
  );

  const grandTotal = editing && readOnly ? editing.grandTotal : summary.total;
  const totalDiscount =
    editing && readOnly ? editing.totalDiscount : summary.discount;

  // Consumidor Final: el backend es la autoridad (AuthorizeSalesInvoiceHandler bloquea igual
  // si se manipula el payload) — esto solo previene el intento en UI con un mensaje claro,
  // usando siempre el monto ya resuelto por el backend (nunca 50/200 hardcodeado aquí).
  const isConsumerFinalCustomer =
    customerProfile?.identificationType === "07" &&
    customerProfile?.taxId === CONSUMIDOR_FINAL_IDENTIFICATION_NUMBER;
  const consumerFinalPolicy = runtimeContext?.consumerFinalPolicy ?? null;
  const consumerFinalAmountExceeded =
    isConsumerFinalCustomer &&
    !!consumerFinalPolicy &&
    grandTotal > consumerFinalPolicy.consumerFinalMaxAmount;

  // POS-CONFIG-STATUS-SEVERITY-01: solo los issues `error` bloquean (y SIEMPRE bloquean).
  const configStatus = computeSalesConfigStatus({
    hasCashSession,
    emissionType,
    docTypeCode: readOnly ? editing?.docTypeCode : formWatch.docTypeCode,
    lines,
    defaultSriPaymentCode: readOnly
      ? editing?.sriPaymentMethodCode
      : formWatch.sriPaymentMethodCode,
    payments,
    paymentMethods,
  });

  // ── POS-CANEMIT-SSOT-01 — ÚNICA fuente de verdad de "¿se puede emitir?" ──
  // Lista ordenada de bloqueantes: el primero es el "Siguiente paso" del checklist y el motivo
  // del botón Emitir; `canEmit` es exactamente "no hay bloqueantes". La usan EmitButton,
  // SalesFormChecklist, F8/Enter y el estado del cobro — nadie recalcula reglas por su cuenta.
  const emitBlockers: EmitBlocker[] = [];
  if (!hasCustomer)
    emitBlockers.push({ source: "customer", message: "Seleccione un cliente para comenzar." });
  if (!hasLines)
    emitBlockers.push({ source: "lines", message: "Agregue productos a la factura." });
  if (lineIssues.length)
    emitBlockers.push({source:"lines",message:salesLineCorrectionSummary(lineIssues)});
  if (saveError) emitBlockers.push({source:"document",message:saveError.detail || saveError.title});
  if (hasCashSession !== true)
    emitBlockers.push({
      source: "cashSession",
      message: cashSessionCheckError
        ? "No se pudo verificar la caja — reintente arriba antes de emitir."
        : "Debe abrir una caja antes de emitir.",
    });
  if (configStatus.missing.length > 0)
    emitBlockers.push({
      source: "config",
      message: `Revise la configuración de venta: ${configStatus.missing.join(", ")}.`,
    });
  if (consumerFinalAmountExceeded)
    emitBlockers.push({
      source: "consumerFinal",
      message:
        "El total supera el monto permitido para Consumidor Final — seleccione un cliente identificado.",
    });
  if (hasLines && !collection.isComplete)
    emitBlockers.push({ source: "payment", message: "Complete el cobro en Formas de Cobro." });

  const canEmit = !fieldDisabled && emitBlockers.length === 0;

  const taxBreakdown: TaxBreakdownEntry[] = useMemo(() => {
    if (editing && readOnly && editing.lines.length > 0) {
      const byRate = new Map<number, { base: number; tax: number }>();
      for (const l of editing.lines) {
        const entry = byRate.get(l.vatRate) ?? { base: 0, tax: 0 };
        entry.base += l.taxableBase;
        entry.tax += l.vatAmount;
        byRate.set(l.vatRate, entry);
      }
      return Array.from(byRate.entries())
        .sort((a, b) => a[0] - b[0])
        .map(([rate, v]) => ({
          label: formatVatLabel(rate),
          rate,
          base: v.base,
          tax: v.tax,
        }));
    }
    return summary.taxBreakdown;
  }, [editing, readOnly, summary.taxBreakdown]);

  // ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01: conectividad SRI acotada a Ventas —
  // el bootstrap global (SessionBootstrap) ya no hace ping al SRI, solo esta pantalla, y solo
  // cuando el punto de emisión activo es electrónico (una venta física no necesita el SRI). El
  // propio store cachea el resultado (SRI_CONNECTIVITY_TTL_MS) y deduplica llamadas
  // concurrentes, así que refreshSriConnectivity() puede llamarse sin miedo a multiplicar pings;
  // el efecto solo depende de isElectronic, no de cliente/producto/cantidad, para no repetir la
  // llamada por cada edición del formulario.
  const sriStatus = useElectronicInvoicingStatusStore((s) => s.status);
  const refreshSriConnectivity = useElectronicInvoicingStatusStore(
    (s) => s.refreshConnectivity,
  );
  useEffect(() => {
    if (isElectronic) void refreshSriConnectivity();
  }, [isElectronic, refreshSriConnectivity]);

  const selectedPt = useMemo(
    () => paymentTermsList.find((p) => p.id === formWatch.paymentTermId),
    [paymentTermsList, formWatch.paymentTermId],
  );

  // Misma condición que usa el backend para decidir "es crédito" por PaymentTerm
  // (AuthorizeSalesInvoiceHandler: CreditTermDays>0 || Installments>1) — nunca hardcodear un id
  // de condición de pago, siempre los flags reales del PaymentTerm seleccionado.
  const isCreditTerm =
    !!selectedPt && (selectedPt.totalDays > 0 || selectedPt.installments > 1);

  // BUGFIX-SALES-CREDIT-PAYMENT-CONSISTENCY-01: si la condición de pago deja de ser crédito
  // (p.ej. el cliente cambia y se resuelve un PaymentTerm de contado), cualquier pago ya
  // registrado con un método de crédito (PaymentMethod.IsCreditAllowed) queda inválido — se
  // limpia aquí mismo, en el único lugar que conoce ambas señales (término + métodos). El
  // backend sigue siendo la autoridad real: esto solo evita que el usuario intente autorizar
  // una combinación que ya sabemos inválida.
  useEffect(() => {
    if (isCreditTerm || paymentMethods.length === 0) return;
    const creditMethodIds = new Set(
      paymentMethods.filter((pm) => pm.isCreditAllowed).map((pm) => pm.id),
    );
    if (creditMethodIds.size === 0) return;
    const current = getValues("payments");
    if (!current.some((p) => creditMethodIds.has(p.paymentMethodId))) return;
    setValue(
      "payments",
      current.filter((p) => !creditMethodIds.has(p.paymentMethodId)),
      { shouldDirty: true },
    );
    message.warning(
      "La condición de pago es contado — se quitó el método de pago Crédito.",
    );
  }, [isCreditTerm, paymentMethods, getValues, setValue]);

  // ── Init reference data ────────────────────────────────────────────
  // Flujo: config empresa → catálogos → aplicar defaults → listo para renderizar
  useEffect(() => {
    void (async () => {
      // 1. Datos independientes en paralelo
      const [defaults, , , , , , whs, , mySession] = await Promise.allSettled([
        salesDefaultsService.get(),
        paymentTermLookupFacade
          .list()
          .then(setPaymentTermsList)
          .catch(() => {}),
        paymentMethodService
          .list(true)
          .then(setPaymentMethods)
          .catch(() => {}),
        loadPrecisionPolicy(),
        sriLookupFacade
          .paymentMethods()
          .then((pms) =>
            setSriPaymentMethods(
              pms.map((p) => ({ code: p.code, name: p.name })),
            ),
          )
          .catch(() => {}),
        sriLookupFacade
          .docTypes()
          .then((dts) =>
            setSriDocTypes(dts.map((d) => ({ code: d.code, name: d.name }))),
          )
          .catch(() => {}),
        warehouseLookupFacade.list("active"),
        sriLookupFacade
          .vatRates()
          .then((rates) => {
            const map: Record<string, number> = {};
            for (const r of rates) map[r.code] = r.percentage;
            setVatRatesMap(map);
          })
          .catch(() => {}),
        checkCashSession(),
        sriLookupFacade
          .iceRates()
          .then((rates) => {
            const map: Record<string, number> = {};
            for (const r of rates) map[r.code] = r.percentage;
            setIceRatesMap(map);
          })
          .catch(() => {}),
        sriLookupFacade
          .idTypes("Customer")
          .then((types) =>
            setSriIdTypes(types.map((t) => ({ code: t.code, name: t.name }))),
          )
          .catch(() => {}),
        salesRuntimeContextService
          .get()
          .then(setRuntimeContext)
          .catch(() => {}),
        // SALES-TRANSFER-BANK-ACCOUNT-01
        bankAccountLookupFacade
          .list(true)
          .then(setBankAccounts)
          .catch(() => {}),
        bankLookupFacade
          .list(true)
          .then(setBanks)
          .catch(() => {}),
      ]);

      // 2. Bodegas
      const whsData = whs.status === "fulfilled" ? whs.value : [];
      setWarehouses(whsData);

      // 3. Aplicar defaults del tenant al formulario inicial
      const d = defaults.status === "fulfilled" ? defaults.value : null;
      setTenantDefaults(d);

      const effectiveDocTypeCode =
        d?.defaultDocTypeCode ?? d?.fallbackDocTypeCode ?? "";
      const effectiveSriPaymentMethodCode =
        d?.defaultSriPaymentMethodCode ?? d?.fallbackSriPaymentMethodCode ?? "";

      // Bodega: Caja (CashRegister.DefaultWarehouseId, la más específica) → default resuelto
      // en backend (Branch OrgSetting → Warehouse.IsMain de la sucursal). CONFIG-FOUNDATION-P0-01:
      // ya NUNCA se sustituye por "la primera bodega del listado" — si ninguna de las dos fuentes
      // resuelve un valor, el campo queda vacío y exige selección manual (el guard existente en
      // addLineWithItem ya bloquea agregar ítems que requieren bodega sin selección).
      const sessionData =
        mySession.status === "fulfilled" ? mySession.value : null;
      const effectiveWhId = sessionData?.defaultWarehouseId ?? d?.defaultWarehouseId ?? "";

      setValue("docTypeCode", effectiveDocTypeCode);
      setValue("sriPaymentMethodCode", effectiveSriPaymentMethodCode);
      if (effectiveWhId) setSelectedWarehouseId(effectiveWhId);

      if (!effectiveWhId && d?.requiresManualWarehouseSelection) {
        message.warning(
          d.configurationWarnings[0] ??
            "No hay una bodega predeterminada configurada para esta sucursal. Seleccione una bodega antes de facturar.",
        );
      }

      // Cliente por defecto de la Caja — mismo loadCustomerProfile ya usado al elegir
      // cliente manualmente (handleCustomerChange), sin lógica paralela. Si la caja no
      // trae DefaultCustomerId, se cae al fallback universal de Consumidor Final (venta
      // mostrador) — nunca queda el campo con datos inventados, solo vacío si tampoco existe.
      if (sessionData?.defaultCustomerId) {
        setValue("customerId", sessionData.defaultCustomerId, {
          shouldDirty: true,
        });
        const profile = await loadCustomerProfile(
          sessionData.defaultCustomerId,
        );
        setCustomerProfile(profile);
      } else {
        const consumidorFinal = await resolveConsumidorFinal();
        if (consumidorFinal) {
          setValue("customerId", consumidorFinal.id, { shouldDirty: true });
          const profile = await loadCustomerProfile(consumidorFinal.id);
          setCustomerProfile(profile);
        }
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ── List ───────────────────────────────────────────────────────────
  const fetchList = useCallback(async () => {
    setListLoading(true);
    try {
      const r = await salesService.list(listSearch || undefined);
      setListItems(r.items);
    } catch {
      /* silent */
    }
    setListLoading(false);
  }, [listSearch]);

  useEffect(() => {
    fetchList();
  }, [fetchList]);

  // ── Customer profile loader ────────────────────────────────────────
  const loadCustomerProfile = useCallback(
    async (bpId: string): Promise<CustomerProfile | null> => {
      try {
        const [bp, locations, contacts, trading] = await Promise.all([
          businessPartnerLookupFacade.getBusinessPartner(bpId),
          businessPartnerLookupFacade.getLocations(bpId, true).catch(() => []),
          businessPartnerLookupFacade.getContacts(bpId, true).catch(() => []),
          businessPartnerLookupFacade.getSalesSettings(bpId),
        ]);

        const {
          paymentTermId: ptId,
        } = trading;

        // ADR-033, Fase 3c: el backend resuelve el default (CompanyBpSalesSettings.PaymentTermId
        // de la empresa activa) al crear/actualizar el borrador — es la autoridad final. Aquí solo
        // se previsualiza esa misma fuente (mismo dato que el backend usaría en primer lugar) para
        // no dejar el campo vacío mientras el usuario arma la venta. Eliminado: inferencia por
        // duración numérica y fallback a un default genérico de empresa — ninguno de los dos es
        // parte de la cadena de resolución aprobada.
        if (ptId && paymentTermsList.some((p) => p.id === ptId && p.isActive)) {
          setValue("paymentTermId", ptId, { shouldDirty: true });
        }

        // Consumidor Final nunca puede crédito (regla fija del backend, ver
        // ISalesFiscalPolicyResolver) — si la condición de pago recién resuelta implica
        // crédito, se fuerza a contado aquí mismo (misma fuente que resolvió el paymentTermId
        // arriba, para no duplicar la lógica de detección en otro lugar). El backend sigue
        // siendo la autoridad real: esto es solo prevención de UX.
        if (
          bp.identificationType === "07" &&
          bp.identificationNumber === CONSUMIDOR_FINAL_IDENTIFICATION_NUMBER
        ) {
          const resolvedPtId = getValues("paymentTermId");
          const resolvedPt = paymentTermsList.find((p) => p.id === resolvedPtId);
          const impliesCredit =
            !!resolvedPt && (resolvedPt.totalDays > 0 || resolvedPt.installments > 1);
          if (impliesCredit) {
            const contado = paymentTermsList.find(
              (p) => p.isActive && p.totalDays === 0 && p.installments === 1,
            );
            if (contado) {
              setValue("paymentTermId", contado.id, { shouldDirty: true });
              message.warning(
                "Consumidor Final no puede registrar ventas a crédito. Se cambió la condición de pago a contado.",
              );
            }
          }
        }

        return {
          name: bp.tradeName || bp.legalName,
          taxId: bp.identificationNumber,
          identificationType: bp.identificationType,
          email: contacts[0]?.email ?? locations[0]?.email ?? null,
          phone: contacts[0]?.phone ?? locations[0]?.phone ?? null,
          address: locations[0]?.addressLine ?? null,
          paymentTermId: ptId,
        };
      } catch {
        return null;
      }
    },
    [paymentTermsList, setValue, getValues],
  );

  // ── Line operations ────────────────────────────────────────────────
  const addLineWithItem = useCallback(
    async (item: InvoiceItemSearchResultDto) => {
      const selectedWh = warehouses.find((w) => w.id === selectedWarehouseId);

      // Kardex: la bodega de despacho es obligatoria por línea para ítems que
      // controlan inventario — se toma la bodega activa del buscador al momento
      // de agregar el ítem (una misma factura puede combinar líneas de bodegas distintas).
      if (item.participatesInInventory && !selectedWarehouseId) {
        message.error("Seleccione una bodega antes de agregar este producto.");
        return;
      }

      // Precio dinámico: SSOT es el Pricing Engine v2 (PricingResolver), resuelto
      // puntualmente al seleccionar el ítem — no se usa el precio base del buscador.
      // SALES-CONTEXTUAL-PRICING-READ-06A: se informa el cliente actual (o ninguno) — Sales no
      // decide qué lista corresponde, solo deja que el backend resuelva Customer → CompanyDefault
      // → PVP con ese dato.
      const lineLoadVersion = lineLoadVersionRef.current;
      let pricing;
      try {
        pricing = await salesItemPricingService.get(
          item.id,
          getValues("customerId") || undefined,
        );
      } catch (err: unknown) {
        message.error(
          extractErrorText(err, "No se pudo obtener el precio del producto."),
        );
        return;
      }

      if (lineLoadVersion !== lineLoadVersionRef.current) return;

      const cost =
        item.averageCost != null && item.averageCost > 0
          ? item.averageCost
          : undefined;
      const stockQty = item.availableStock ?? undefined;
      const vatCode = pricing.vatCode ?? "";
      const iceCode = normalizeOptionalCode(pricing.iceCode);
      const lineWarehouseId = item.participatesInInventory ? selectedWarehouseId : null;

      // SALES-PRESENTATIONS-03: por defecto se vende en unidad base (comportamiento actual
      // preservado) — salvo que el texto buscado haya coincidido con el barcode de una
      // presentación específica (ItemPackagingLevel.Barcode), en cuyo caso esa presentación se
      // autoselecciona (regla 2/5 de la tarea). IsSaleDefault deliberadamente NO se usa todavía
      // (backend tampoco lo consume en esta fase — ver SalesLinePackagingResolver).
      const defaultPresentation = resolveDefaultLinePresentation(item);
      const { packagingLevelId, uomCode, conversionFactor } = defaultPresentation;
      // SALES-CUSTOMER-REPRICE-METADATA-06C2A: mismo mapeo Pricing → línea que usa el repricing
      // de cambio de cliente (useSalesCustomerRepricing) — única fuente de esta semántica.
      const pricingFields = mapResolvedPricingToLineFields(
        pricing.unitPrice ?? 0,
        pricing.basePrice,
        pricing.priceListName,
        pricing.discountDescription,
        conversionFactor,
        pricing.priceListId,
      );
      const { unitPrice } = pricingFields;

      const currentLines = getValues("lines");

      // Reescaneo del mismo producto (código de barras o texto) bajo condiciones idénticas —
      // acumula cantidad en la línea existente en vez de duplicar la línea (flujo POS: escanear
      // 3 veces el mismo producto suma 3 unidades, no crea 3 filas). Condición de fusión
      // centralizada en salesCalc.ts (findMergeableLineIndex) — único punto, testeable sin
      // levantar todo el hook.
      const matchIndex = findMergeableLineIndex(currentLines, {
        itemId: item.id,
        unitPrice,
        vatCode,
        iceCode,
        warehouseId: lineWarehouseId,
        packagingLevelId,
      });

      if (matchIndex >= 0) {
        setValue(
          "lines",
          currentLines.map((l, idx) =>
            idx === matchIndex ? { ...l, quantity: l.quantity + 1 } : l,
          ),
          { shouldValidate: true, shouldDirty: true },
        );
        setProductSearchFocusKey((k) => k + 1); // reenfoca "buscar producto" — flujo continuo POS
        return;
      }

      const newLine: SalesLineFormValues = {
        _key: Math.max(lineKey, ...currentLines.map((line) => line._key + 1)),
        itemId: item.id,
        warehouseId: lineWarehouseId,
        description: `${item.sku} — ${item.description}`,
        quantity: 1,
        vatCode,
        discountPct: 0,
        iceCode: iceCode ?? undefined,
        packagingLevelId,
        uomCode,
        baseUomCode: item.baseUomCode,
        conversionFactor,
        _sku: item.sku,
        _name: item.description,
        ...pricingFields,
        _cost: cost,
        _stockQty: stockQty,
        _stockWarehouse: selectedWh?.name,
        _participatesInInventory: item.participatesInInventory,
        _stockControlEnabled: item.stockControlEnabled,
        _packagingLevels: item.packagingLevels,
      };

      setValue("lines", [...currentLines, newLine], {
        shouldValidate: true,
        shouldDirty: true,
      });
      setLineKey((k) => k + 1);
      setProductSearchFocusKey((k) => k + 1); // reenfoca "buscar producto" tras agregar línea — flujo continuo POS
    },
    [lineKey, selectedWarehouseId, warehouses, getValues, setValue],
  );

  const removeLine = useCallback(
    (key: number) => {
      const currentLines = getValues("lines");
      setValue(
        "lines",
        currentLines.filter((l) => l._key !== key),
        { shouldValidate: true, shouldDirty: true },
      );
    },
    [getValues, setValue],
  );

  const updateLine = useCallback(
    (key: number, field: string, value: unknown) => {
      const currentLines = getValues("lines");
      setValue(
        "lines",
        currentLines.map((l) => {
          if (l._key !== key) return l;
          if (field === "invoicedUnitPrice") {
            const pricing = resolveInvoicedUnitPriceEdit(
              l.unitPrice, Number(value), getPrecisionPolicy().percentageDecimals,
            );
            return {
              ...l, ...pricing,
              ...(pricing.unitPrice !== l.unitPrice ? { _isManualPrice: true } : {}),
            };
          }
          return {
            ...l,
            [field]: value,
            // A direct reference-price change remains a manual price.
            ...(field === "unitPrice" ? { _isManualPrice: true } : {}),
          };
        }),
        { shouldDirty: true },
      );
    },
    [getValues, setValue],
  );

  // Bodega de una sola línea: el selector inteligente ya trae la disponibilidad
  // (misma respuesta que pobló su lista) — se aplica en un solo setValue, sin
  // una segunda consulta de stock.
  const onUpdateLineWarehouse = useCallback(
    (
      key: number,
      warehouseId: string,
      option?: ItemWarehouseAvailabilityDto,
    ) => {
      lineWarehouseRequestsRef.current.delete(key);
      const currentLines = getValues("lines");
      setValue(
        "lines",
        currentLines.map((l) =>
          l._key === key
            ? {
                ...l,
                warehouseId,
                ...(option
                  ? {
                      _stockQty: option.available,
                      _stockWarehouse: option.warehouseName,
                    }
                  : {}),
              }
            : l,
        ),
        { shouldDirty: true },
      );
    },
    [getValues, setValue],
  );

  // SALES-PRESENTATIONS-03: cambio de presentación (unidad/caja/pack) de una línea ya agregada —
  // recalcula uomCode/baseUomCode/conversionFactor y sugiere un nuevo Precio Facturado
  // (precio base * factor, ver suggestedUnitPriceForPresentation) sin tocar PricingResolver ni
  // crear una tabla de precios por presentación. quantityInBaseUom no se guarda aquí: se deriva
  // en pantalla (lineQuantityInBaseUom) y la persiste el backend al guardar el draft.
  const onUpdateLinePresentation = useCallback(
    (key: number, packagingLevelId: string) => {
      const currentLines = getValues("lines");
      const line = currentLines.find((l) => l._key === key);
      if (!line) return;
      // basePrice: el precio base resuelto UNA vez al agregar el producto (_pvp, nunca mutado
      // por esta función) — nunca line.unitPrice, que ya puede estar escalado por una
      // presentación anterior (evitaría doble multiplicación, regla 8).
      const basePrice = line._pvp ?? line.unitPrice;
      const change = resolveLinePresentationChange(
        packagingLevelId,
        line._packagingLevels ?? [],
        line.baseUomCode ?? "UNIT",
        basePrice,
      );
      setValue(
        "lines",
        currentLines.map((l) => (l._key === key ? { ...l, ...change } : l)),
        { shouldValidate: true, shouldDirty: true },
      );
    },
    [getValues, setValue],
  );

  // Bodega de encabezado: default para líneas nuevas + cascada a líneas existentes
  // que controlan inventario (una línea de servicio nunca adquiere bodega), recargando
  // el stock de cada línea afectada con el mismo servicio que usa el selector por línea.
  const handleWarehouseChange = useCallback(
    (id: string) => {
      const version = ++warehouseLoadVersionRef.current;
      setSelectedWarehouseId(id);
      const currentLines = getValues("lines");
      setValue(
        "lines",
        currentLines.map((l) =>
          l._participatesInInventory ? { ...l, warehouseId: id, _stockQty: undefined, _stockWarehouse: undefined } : l,
        ),
        { shouldValidate: true, shouldDirty: true },
      );

      const affected = currentLines.filter((l) => l._participatesInInventory && l.itemId);
      void Promise.all(
        affected.map(async (l) => {
          const request = {};
          lineWarehouseRequestsRef.current.set(l._key, request);
          try {
            const options = await stockLookupFacade.getWarehouseAvailability(
              l.itemId!,
            );
            const match = options.find((o) => o.warehouseId === id);
            if (!match || version !== warehouseLoadVersionRef.current
                || lineWarehouseRequestsRef.current.get(l._key) !== request) return;
            const latest = getValues("lines");
            setValue(
              "lines",
              latest.map((x) =>
                x._key === l._key && x.itemId === l.itemId && x.warehouseId === id
                  ? {
                      ...x,
                      _stockQty: match.available,
                      _stockWarehouse: match.warehouseName,
                    }
                  : x,
              ),
              { shouldDirty: true },
            );
          } catch {
            /* conserva el valor anterior si falla la consulta */
          }
        }),
      );
    },
    [getValues, setValue],
  );

  // ── Payment operations ─────────────────────────────────────────────
  const setInvoicePayments = useCallback(
    (
      updater:
        | SalesPaymentFormValues[]
        | ((prev: SalesPaymentFormValues[]) => SalesPaymentFormValues[]),
    ) => {
      const current = getValues("payments");
      const next = typeof updater === "function" ? updater(current) : updater;
      setValue("payments", next, { shouldDirty: true });
    },
    [getValues, setValue],
  );

  // POS-CASH-ONLY-FOLLOWS-TOTAL-01 — pagos base al sumar OTRA forma de cobro. Si hoy el único
  // cobro es Efectivo (aplicado = total, derivado), pasar a multipago fija lo aplicado en
  // Efectivo a lo realmente recibido (tope: el total) — o lo quita si aún no se ingresó nada —
  // para que la nueva forma de cobro cubra solo el resto. Pura: no muta el formulario; el
  // llamador aplica el resultado junto con el nuevo cobro en un solo setInvoicePayments (así
  // la regla "Efectivo único sigue al total" nunca ve un estado intermedio). En multipago
  // devuelve los pagos tal cual: los montos explícitos nunca se redistribuyen.
  const paymentsForAdditionalMethod = useCallback(
    (paymentMethodId: string): SalesPaymentFormValues[] =>
      basePaymentsForAdditionalMethod(
        getValues("payments"),
        paymentMethodId,
        isCashMethodId,
        cashReceivedValue,
        summary.total,
      ),
    [getValues, isCashMethodId, cashReceivedValue, summary.total],
  );

  // ── Form reset ─────────────────────────────────────────────────────
  const resetForm = useCallback(async () => {
    ++lineLoadVersionRef.current;
    ++warehouseLoadVersionRef.current;
    lineWarehouseRequestsRef.current.clear();
    setServerLineIssues([]);
    const base = emptySalesInvoiceForm();
    reset({
      ...base,
      docTypeCode:
        tenantDefaults?.defaultDocTypeCode ??
        tenantDefaults?.fallbackDocTypeCode ??
        "",
      sriPaymentMethodCode:
        tenantDefaults?.defaultSriPaymentMethodCode ??
        tenantDefaults?.fallbackSriPaymentMethodCode ??
        "",
      paymentTermId: tenantDefaults?.defaultPaymentTermId ?? "",
    });
    // Restaurar bodega por defecto del tenant
    if (tenantDefaults?.defaultWarehouseId) {
      setSelectedWarehouseId(tenantDefaults.defaultWarehouseId);
    }
    setCustomerProfile(null);
    setEditing(null);
    setSaveError(null);
    setLineKey(1);
    setPayKey(1);
    setCashReceivedInput("");
    setCreditRows([]);
    setConfirmedScheduleRows(null);
    setScheduleIsManual(false);
    setProductSearchFocusKey((k) => k + 1); // reenfoca "buscar producto" — UX retail

    // Cliente por defecto: Caja (DefaultCustomerId) → fallback universal Consumidor
    // Final — mismo criterio de prioridad que la carga inicial de la pantalla.
    const defaultCustomerId = myCashSession?.defaultCustomerId;
    if (defaultCustomerId) {
      setValue("customerId", defaultCustomerId, { shouldDirty: true });
      const profile = await loadCustomerProfile(defaultCustomerId);
      setCustomerProfile(profile);
    } else {
      const consumidorFinal = await resolveConsumidorFinal();
      if (consumidorFinal) {
        setValue("customerId", consumidorFinal.id, { shouldDirty: true });
        const profile = await loadCustomerProfile(consumidorFinal.id);
        setCustomerProfile(profile);
      }
    }
  }, [reset, tenantDefaults, myCashSession, setValue, loadCustomerProfile]);

  // "Limpiar Todo": solo pide confirmación si hay algo que perder (cliente y/o líneas
  // cargadas) — evita fricción cuando el formulario ya está vacío.
  const clearForm = useCallback(async () => {
    const hasData = getValues("customerId") || getValues("lines").length > 0;
    if (hasData) {
      const confirmed = await message.confirm({
        title: "Limpiar factura",
        message:
          "Se perderán el cliente y los productos ya agregados. ¿Deseas continuar?",
        variant: "danger",
        confirmLabel: "Limpiar",
        cancelLabel: "Cancelar",
      });
      if (!confirmed) return;
    }
    void resetForm();
  }, [getValues, resetForm]);

  // ── Customer change handler ────────────────────────────────────────
  // SALES-CUSTOMER-CHANGE-REPRICE-UX-06C2: aplicación "real" del cambio de cliente (customerId,
  // perfil, paymentTerm/schedule) — sin tocar líneas. Se usa tanto en el camino directo (sin
  // líneas o sin diferencias de precio) como tras confirmar el modal de repricing.
  const applyCustomerChangeCore = useCallback(
    async (c: CustomerPickerRow | null) => {
      setValue("customerId", c?.id ?? "", {
        shouldValidate: true,
        shouldDirty: true,
      });
      // ADR-033, Fase 4: cambiar de cliente puede cambiar el PaymentTerm resuelto — cualquier
      // cronograma personalizado confirmado para el cliente anterior deja de ser válido; el
      // backend igual regenera el automático en este caso, pero se limpia aquí para que el
      // simulador no muestre "Personalizado" con datos obsoletos si se reabre.
      setConfirmedScheduleRows(null);
      setScheduleIsManual(false);
      if (c) {
        const profile = await loadCustomerProfile(c.id);
        setCustomerProfile(profile);
        setProductSearchFocusKey((k) => k + 1); // reenfoca "buscar producto" tras seleccionar cliente — flujo continuo POS
      } else {
        setCustomerProfile(null);
      }
    },
    [setValue, loadCustomerProfile],
  );

  const repricing = useSalesCustomerRepricing({
    applyCustomerChange: applyCustomerChangeCore,
    applyPlanToLines: (plan) => {
      const currentLines = getValues("lines");
      setValue("lines", applyRepricingPlanToLines(currentLines, plan), {
        shouldDirty: true,
        shouldValidate: true,
      });
    },
    onPreviewError: () => {
      // Fail-closed: si no se pudo calcular el nuevo precio, no se cambia el cliente ni se toca
      // ninguna línea — mejor no aplicar el cambio que aplicarlo con precios desactualizados.
      message.error(
        "No se pudo calcular el nuevo precio para el cliente seleccionado. El cliente no fue cambiado — intente nuevamente.",
      );
    },
  });

  const handleCustomerChange = useCallback(
    (c: CustomerPickerRow | null) => {
      const currentLines = getValues("lines");
      const decimals = getPrecisionPolicy().salesUnitPriceDecimals;
      void repricing.requestCustomerChange(
        c,
        currentLines.map((l) => ({
          key: l._key,
          itemId: l.itemId,
          description: l.description,
          unitPrice: l.unitPrice,
          conversionFactor: l.conversionFactor,
          _pvp: l._pvp,
          _basePrice: l._basePrice,
          _priceListName: l._priceListName,
          _discountDescription: l._discountDescription,
          _priceListId: l._priceListId,
        })),
        getValues("customerId") || undefined,
        decimals,
      );
    },
    [getValues, repricing],
  );

  const confirmRepricing = useCallback(() => {
    void repricing.confirm();
  }, [repricing]);

  const cancelRepricing = useCallback(() => repricing.cancel(), [repricing]);

  // ── Load for edit ──────────────────────────────────────────────────
  const loadForEdit = useCallback(
    async (id: string) => {
      try {
        const inv = await salesService.getById(id);
        setEditing(inv);
        setCustomerProfile({
          name: inv.customerName,
          taxId: inv.customerTaxId,
          identificationType: inv.customerIdentificationType,
          email: inv.customerEmail,
          address: inv.customerAddress,
          phone: null,
          paymentTermId: null,
        });

        const mappedLines: SalesLineFormValues[] = mapInvoiceLinesToFormValues(inv);

        const mappedPayments: SalesPaymentFormValues[] = (
          inv.payments ?? []
        ).map((p, i) => ({
          _key: i + 1,
          paymentMethodId: p.paymentMethodId,
          amount: p.amount,
          reference: p.reference,
          cardDetail: p.cardDetail
            ? nullFieldsToUndefined(p.cardDetail)
            : undefined,
          transferDetail: p.transferDetail
            ? nullFieldsToUndefined(p.transferDetail)
            : undefined,
          chequeDetail: p.chequeDetail
            ? nullFieldsToUndefined(p.chequeDetail)
            : undefined,
        }));

        reset({
          customerId: inv.customerId,
          issueDate: inv.issueDate,
          dueDate: inv.dueDate ?? "",
          notes: inv.notes ?? "",
          paymentTermId: inv.paymentTermId ?? "",
          docTypeCode:
            inv.docTypeCode ?? tenantDefaults?.fallbackDocTypeCode ?? "",
          sriPaymentMethodCode:
            inv.sriPaymentMethodCode ??
            tenantDefaults?.fallbackSriPaymentMethodCode ??
            "",
          lines: mappedLines,
          payments: mappedPayments,
        });

        setLineKey(inv.lines.length + 1);
        setPayKey((inv.payments?.length ?? 0) + 1);
        // POS-CASH-TENDERED-01: el efectivo recibido persistido vuelve al POS al reabrir la venta.
        const persistedTendered = (inv.payments ?? []).find(
          (p) => p.tenderedAmount != null,
        )?.tenderedAmount;
        setCashReceivedInput(persistedTendered != null ? String(persistedTendered) : "");

        // ADR-033, Fase 4: hidrata el cronograma persistido — si el usuario reabre el
        // simulador, ve exactamente lo que ya tiene el borrador (automático o personalizado).
        const scheduleRows: CreditRow[] = (inv.paymentSchedule ?? []).map((s) => ({
          number: s.installmentNumber,
          dueDate: s.dueDate,
          amount: s.amount,
        }));
        setCreditRows(scheduleRows);
        setConfirmedScheduleRows(
          inv.isPaymentScheduleManual && scheduleRows.length > 0 ? scheduleRows : null,
        );
        setScheduleIsManual(inv.isPaymentScheduleManual);

        setTab("nuevo");
      } catch (err: unknown) {
        setSaveError(
          buildSalesErrorNotice(
            err,
            "No se pudo cargar la información de ventas.",
            "Error al cargar la factura.",
          ),
        );
      }
    },
    [reset, tenantDefaults],
  );

  // ── Draft persistence (compartido por Guardar y por el auto-guardado previo a Emitir) ──
  const persistDraft = useCallback(
    async (data: SalesInvoiceFormValues): Promise<SalesInvoiceDto> => {
      const payload = {
        customerId: data.customerId,
        issueDate: data.issueDate || todayIso(),
        lines: data.lines.map((l) => ({
          itemId: l.itemId,
          warehouseId: l.warehouseId,
          description: l.description,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          vatCode: l.vatCode,
          discountPct: l.discountPct ?? 0,
          iceCode: l.iceCode,
          notes: l.notes,
          // SALES-PRESENTATIONS-03: Quantity/UnitPrice siguen siendo la cantidad/precio en la
          // presentación vendida — el backend (SalesLinePackagingResolver) es la única autoridad
          // que resuelve UomCode/ConversionFactor/QuantityInBaseUom a partir de este Id; el
          // frontend nunca envía esos valores calculados como si fueran la fuente de verdad.
          packagingLevelId: l.packagingLevelId ?? null,
        })),
        dueDate: data.dueDate || null,
        notes: data.notes || null,
        paymentTermId: data.paymentTermId || null,
        docTypeCode: data.docTypeCode || null,
        sriPaymentMethodCode: data.sriPaymentMethodCode || null,
        payments: data.payments.map((p) => ({
          paymentMethodId: p.paymentMethodId,
          amount: p.amount,
          // POS-CASH-TENDERED-01: efectivo entregado persistido en el pago en efectivo — fuente de
          // la tirilla y de cualquier reimpresión (el backend lo valida y no cambia `amount`).
          tenderedAmount: tenderedForPayment(
            isCashMethodId(p.paymentMethodId),
            p.amount,
            cashReceivedValue,
          ),
          reference: p.reference,
          cardDetail: p.cardDetail ?? undefined,
          // ZH-TEMPORAL-CONTRACT-02J: transferDate/cashDate son fechas de negocio (API DateOnly
          // "YYYY-MM-DD"); un input vacío se omite en vez de enviar "" (inválido para DateOnly).
          transferDetail: p.transferDetail
            ? { ...p.transferDetail, transferDate: p.transferDetail.transferDate || undefined }
            : undefined,
          chequeDetail: p.chequeDetail
            ? { ...p.chequeDetail, cashDate: p.chequeDetail.cashDate || undefined }
            : undefined,
        })),
        // ADR-033, Fase 4: solo se envía si el usuario confirmó explícitamente un cronograma
        // personalizado en el simulador — si no, se omite y el backend genera/regenera el
        // cronograma automático a partir de la condición de pago vigente.
        schedule:
          scheduleIsManual && confirmedScheduleRows && confirmedScheduleRows.length > 0
            ? confirmedScheduleRows.map((r) => ({
                installmentNumber: r.number,
                dueDate: r.dueDate,
                amount: r.amount,
              }))
            : null,
      };

      return editing
        ? salesService.update(editing.id, { ...payload, id: editing.id })
        : salesService.create(payload);
    },
    [editing, scheduleIsManual, confirmedScheduleRows, isCashMethodId, cashReceivedValue],
  );

  // ── Issue flow (Nueva Venta → Emitir Factura → Confirmación → Emisión →
  // Pantalla de éxito) ─────────────────────────────────────────────────
  // Único punto de emisión visible al usuario. No existe un paso separado de
  // "guardar borrador" — el Draft se crea/actualiza de forma transparente
  // (persistDraft) como paso interno previo a autorizar. El concepto de
  // Draft se conserva en el dominio para otros escenarios (POS offline,
  // cotizaciones, recuperación de fallos), pero el usuario solo ve una
  // acción: emitir. Emitir valida stock contra lo último persistido en BD
  // (AuthorizeSalesInvoiceHandler), no contra el formulario en pantalla —
  // por eso, si no existe un Draft aún o hay cambios sin guardar, se
  // persiste automáticamente antes de emitir.
  const openIssueFlow = useCallback(() => {
    if (lineIssues.length) { focusFirstInvalidLine(); return; }
    if (!canEmit || issuePhase !== "idle") return;
    setIssueError(null);
    setIssuePhase("confirm");
  }, [canEmit, issuePhase, lineIssues, focusFirstInvalidLine]);

  const closeIssueFlow = useCallback(() => {
    if (issuePhase === "processing") return; // no se puede cerrar mientras se emite
    setIssuePhase("idle");
    setIssueError(null);
  }, [issuePhase]);

  const confirmIssue = useCallback(async () => {
    if (issueInFlightRef.current || issuePhase === "processing") return;
    const currentLineIssues = collectSalesLineIssues(getValues("lines"), serverLineIssues);
    if (currentLineIssues.length) {
      setPendingLineFocus(currentLineIssues[0]);
      setIssuePhase("idle");
      return;
    }
    // Lock synchronously: React state updates alone do not guard calls in the same render.
    issueInFlightRef.current = true;
    setIssuePhase("processing");
    setIssueStepIndex(0); // Validando
    setSaving(true);
    try {
      // ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01: si el último check de
      // conectividad no existe o venció (SRI_CONNECTIVITY_TTL_MS), se refresca acá antes de
      // emitir — refreshConnectivity() ya es no-op si el check sigue vigente, así que esto no
      // agrega un ping por cada clic en "Emitir". Solo advierte (no bloquea): el backend
      // (AuthorizeSalesInvoiceHandler) autoriza la venta igual y reintenta el envío al SRI de
      // forma tolerante — bloquear la caja por una falla externa del SRI sería un cambio de
      // política de emisión que este ticket no pidió.
      if (isElectronic) {
        await refreshSriConnectivity();
        if (shouldWarnSriUnavailable(useElectronicInvoicingStatusStore.getState().status)) {
          message.warning(SRI_UNAVAILABLE_ISSUE_WARNING);
        }
      }

      let invoiceId = editing?.id;

      // POS-CASH-TENDERED-01: el efectivo recibido no vive en el form (no marca isDirty) — si
      // cambió respecto de lo ya persistido en el borrador, también hay que re-guardar.
      const persistedTendered =
        editing?.payments.find((p) => isCashMethodId(p.paymentMethodId))?.tenderedAmount ?? null;
      const currentCash = getValues("payments").find((p) => isCashMethodId(p.paymentMethodId));
      const currentTendered = currentCash
        ? tenderedForPayment(true, currentCash.amount, cashReceivedValue)
        : null;
      const tenderedChanged = persistedTendered !== currentTendered;

      if (!editing || isDirty || tenderedChanged) {
        const valid = await trigger();
        if (!valid) {
          // Error de validación: vuelve al formulario — el usuario corrige
          // inline, ahí es donde el estándar del repo (F-V3/F-V4) espera
          // que se muestre, no en un modal genérico.
          const validationErrors = form.formState.errors;
          const msgs: string[] = [];
          if (validationErrors.customerId) msgs.push("Seleccione un cliente");
          if (validationErrors.lines) msgs.push("Agregue al menos un producto");
          if (validationErrors.issueDate)
            msgs.push("Fecha de emisión requerida");
          const fieldErrors = validationErrors.lines as unknown as
            { message?: string }[] | undefined;
          if (Array.isArray(fieldErrors)) {
            for (const le of fieldErrors) {
              if (le?.message) {
                msgs.push(le.message);
                break;
              }
            }
          }
          setSaveError({
            title: "No se puede emitir la factura.",
            detail:
              msgs.length > 0
                ? msgs.join(". ") + "."
                : "Revise los campos del formulario antes de emitir.",
          });
          setIssuePhase("idle");
          return;
        }

        setIssueStepIndex(1); // Guardando
        let saved: SalesInvoiceDto;
        try {
          saved = await persistDraft(getValues());
        } catch (err: unknown) {
          applyIssueValidationError(err);
          setIssuePhase("idle");
          return; // guardado falló — se cancela la emisión, el usuario corrige en el formulario
        }

        invoiceId = saved.id;
        setEditing(saved);
        reset(getValues()); // limpia isDirty sin navegar ni alterar lo mostrado en pantalla
      }

      // Único request de autorización (numeración + emisión): no expone progreso intermedio,
      // así que se muestra un solo paso real (ver issueStepsFor) hasta que responde.
      setIssueStepIndex(2);
      let authorized: SalesInvoiceDto;
      try {
        // invoiceId siempre queda definido en este punto: o ya existía
        // `editing`, o el bloque anterior lo creó/actualizó y lo asignó.
        // El punto de emisión no se envía — el servidor usa el ya fijado en el borrador
        // (resuelto entonces desde ICurrentCashSession).
        authorized = await salesService.authorize(invoiceId!);
      } catch (err: unknown) {
        if (issueErrorStatus(err) === 422) {
          // Error de validación de negocio (stock insuficiente, punto de
          // emisión inválido, etc.) — mismo tratamiento que arriba: se
          // resuelve en el formulario, no en el modal de emisión. El detalle
          // prioriza siempre data.errors (p. ej. "Línea 'X': stock insuficiente...")
          // sobre el mensaje genérico del catálogo — ver buildSalesErrorNotice.
          applyIssueValidationError(err);
          setIssuePhase("idle");
          return;
        }
        // Error interno o de comunicación con el SRI: el draft ya existe y
        // conserva su secuencial (no se pierde ni se duplica — la captura
        // del secuencial y la autorización comparten una única transacción
        // atómica en AuthorizeSalesInvoiceHandler). Reintentar es seguro.
        setIssueError({
          kind:
            issueErrorStatus(err) === undefined ? "communication" : "internal",
          message: extractErrorText(
            err,
            "Ocurrió un error al emitir la factura. Intente nuevamente o contacte a soporte.",
          ),
        });
        setIssuePhase("error");
        return;
      }

      setIssueResult(authorized);
      setIssuePhase("success");
      fetchList(); // refresca el listado en segundo plano — sin recargar la página
    } finally {
      issueInFlightRef.current = false;
      setSaving(false);
    }
  }, [
    issuePhase,
    applyIssueValidationError,
    serverLineIssues,
    isElectronic,
    refreshSriConnectivity,
    editing,
    isDirty,
    isCashMethodId,
    cashReceivedValue,
    trigger,
    form,
    getValues,
    persistDraft,
    reset,
    fetchList,
  ]);

  const retryIssue = useCallback(() => {
    setIssueError(null);
    void confirmIssue();
  }, [confirmIssue]);

  const startNewSale = useCallback(() => {
    void resetForm();
    setIssuePhase("idle");
    setIssueResult(null);
    setIssueError(null);
    setIssueStepIndex(0);
  }, [resetForm]);

  const handleDownloadXml = useCallback(async () => {
    if (!issueResult || xmlDownloading) return; // evita descargas simultáneas — igual que RIDE/PDF con ridePending
    setXmlDownloading(true);
    try {
      const variant: ElectronicDocumentXmlVariant =
        issueResult.electronicStatus === "Authorized" ? "Authorized" : "Signed";
      const xml = await electronicDocumentAccessFacade.getXml(
        "Sales",
        issueResult.id,
        variant,
      );
      downloadTextFile(xml, `Factura-${issueResult.invoiceNumber}.xml`);
    } catch (e) {
      message.error(extractErrorText(e, "No se pudo descargar el XML."));
    }
    setXmlDownloading(false);
  }, [issueResult, xmlDownloading]);

  // F8: mismo disparador que el botón "Emitir Factura" — única fuente de verdad (canEmit).
  // SALES-QUICK-CUSTOMER-MODAL-INPUT-FIX-07: F8 tampoco es un carácter tecleable, pero se guarda
  // el mismo guard explícito que F2 — ningún atajo global de Sales debe reaccionar mientras el
  // foco está en un control editable (p. ej. dentro de un modal abierto sobre la página).
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (!shouldTriggerF8Emit(e, { tab, issuePhase, canEmit: canEmit || lineIssues.length > 0 })) return;
      e.preventDefault();
      openIssueFlow();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [tab, issuePhase, canEmit, lineIssues, openIssueFlow]);

  // ── Generar documento electrónico (backfill) ───────────────────────
  // Para facturas autorizadas comercialmente que nunca llegaron a generar un ElectronicDocument
  // (p.ej. autorizadas antes de que existiera esta infraestructura, o cuyo primer intento falló
  // sin dejar registro en versiones anteriores). Reutiliza el mismo endpoint que el Monitor.
  const handleGenerateElectronicDocument = useCallback(async () => {
    if (!editing) return;
    setSaving(true);
    try {
      const result = await electronicDocumentAccessFacade.register(
        "Invoice",
        "Sales",
        editing.id,
      );
      if (result.currentState === "Authorized") {
        message.success(
          `Documento electrónico autorizado por el SRI. Clave de acceso: ${result.accessKey}.`,
        );
      } else if (result.currentState === "Failed") {
        message.warning(
          "No se pudo generar el documento electrónico. Revise el Monitor de Documentos Electrónicos para ver el motivo.",
        );
      } else {
        message.warning(
          `Documento electrónico registrado en estado "${result.currentState}". Revise el Monitor de Documentos Electrónicos.`,
        );
      }
      const refreshed = await salesService.getById(editing.id);
      setEditing(refreshed);
    } catch (e: unknown) {
      setSaveError(
        buildSalesErrorNotice(
          e,
          "No se pudo generar el documento electrónico.",
          "No se pudo generar el documento electrónico.",
        ),
      );
    }
    setSaving(false);
  }, [editing]);

  // ── Cancel ─────────────────────────────────────────────────────────
  const handleCancel = useCallback(
    async (reason: string) => {
      setModalCancelReason(false);
      if (!editing) return;
      setSaving(true);
      try {
        await salesService.cancel(editing.id, reason);
        message.success("Factura anulada correctamente.");
        void resetForm();
        setTab("listado");
        fetchList();
      } catch (e: unknown) {
        setSaveError(
          buildSalesErrorNotice(e, "No se pudo anular la factura.", "Error al anular."),
        );
      }
      setSaving(false);
    },
    [editing, resetForm, fetchList],
  );

  // ── Credit simulation ──────────────────────────────────────────────
  const simulateCreditInstallments = useCallback(
    (amount: number): CreditRow[] => {
      if (amount <= 0) return [];
      if (!selectedPt?.isActive) {
        message.error("Debe seleccionar una condición de pago activa.");
        return [];
      }
      const count = selectedPt.installments;
      const interval = selectedPt.daysBetweenInstallments;
      const factor = 10 ** getPrecisionPolicy().moneyDecimals;
      const base = Math.round((amount / count) * factor) / factor;
      const rows: CreditRow[] = [];
      let accumulated = 0;
      // Hoy de la empresa + n días — fecha de negocio, sin Date/zona del navegador.
      const today = todayIso();
      for (let i = 1; i <= count; i++) {
        const due = addDaysIso(today, interval * i);
        const isLast = i === count;
        const amt = isLast
          ? Math.round((amount - accumulated) * factor) / factor
          : base;
        rows.push({ number: i, dueDate: due, amount: amt });
        accumulated += amt;
      }
      return rows;
    },
    [selectedPt],
  );

  // ── Quick customer create/edit ─────────────────────────────────────
  const openNewCustomerModal = useCallback(
    (text: string) => {
      setNewCustName(text);
      setNewCustId("");
      setNewCustIdType(sriIdTypes[0]?.code ?? "05");
      setNewCustAddress("");
      setNewCustEmail("");
      setNewCustPhone("");
      setNewCustError("");
      setNewCustIsEdit(false);
      setModalNewCustomer(true);
    },
    [sriIdTypes],
  );

  const openEditCustomerModal = useCallback(() => {
    if (!customerProfile) return;
    setNewCustIsEdit(true);
    setNewCustName(customerProfile.name);
    setNewCustId(customerProfile.taxId);
    setNewCustIdType(customerProfile.identificationType);
    setNewCustAddress(customerProfile.address ?? "");
    setNewCustEmail(customerProfile.email ?? "");
    setNewCustPhone(customerProfile.phone ?? "");
    setNewCustError("");
    setModalNewCustomer(true);
  }, [customerProfile]);

  const handleSaveQuickCustomer = useCallback(async () => {
    setNewCustSaving(true);
    setNewCustError("");
    try {
      let bpId = getValues("customerId");
      const addr = newCustAddress.trim();
      const email = newCustEmail.trim() || null;
      const phone = newCustPhone.trim() || null;
      const locTypeMap: Record<string, LocationTypeValue> = {
        Matrix: 1,
        Branch: 2,
        Office: 3,
        Warehouse: 4,
        DeliveryPoint: 5,
        Other: 99,
      };
      const roleMap: Record<string, ContactRoleValue> = {
        Commercial: 1,
        Accounting: 2,
        Management: 3,
        Reception: 4,
        Dispatch: 5,
        Billing: 6,
        Technical: 7,
        Purchasing: 8,
        Legal: 9,
        Other: 99,
      };

      if (newCustIsEdit) {
        await businessPartnerRegistrationFacade.updateBusinessPartner(bpId, {
          legalName: newCustName.trim(),
          tradeName: null,
          countryCode: null,
        });
      } else {
        const bp = await businessPartnerRegistrationFacade.createBusinessPartner({
          identificationType: newCustIdType,
          identificationNumber: newCustId.trim(),
          legalName: newCustName.trim(),
        });
        bpId = bp.id;
        await businessPartnerRegistrationFacade.assignRole(bp.id, {
          roleType: RoleTypeEnum.Customer,
        });
      }

      const [locations, contacts] = await Promise.all([
        businessPartnerLookupFacade.getLocations(bpId, true).catch(() => []),
        businessPartnerLookupFacade.getContacts(bpId, true).catch(() => []),
      ]);

      if (addr) {
        if (locations.length > 0) {
          const loc = locations[0];
          const purposeBits = Array.isArray(loc.purposes)
            ? (loc.purposes.includes("Facturación") ? 1 : 0) |
              (loc.purposes.includes("Entrega") ? 2 : 0) |
              (loc.purposes.includes("Fiscal") ? 4 : 0) |
              (loc.purposes.includes("Correspondencia") ? 8 : 0)
            : 5;
          await businessPartnerRegistrationFacade.updateLocation(bpId, loc.id, {
            name: loc.name,
            type: locTypeMap[loc.locationType] ?? 1,
            purpose: purposeBits,
            addressLine: addr,
          });
        } else {
          await businessPartnerRegistrationFacade.createLocation(bpId, {
            name: "Principal",
            type: 1,
            purpose: 5,
            addressLine: addr,
          });
        }
      }

      if (email || phone) {
        if (contacts.length > 0) {
          const ct = contacts[0];
          await businessPartnerRegistrationFacade.updateContact(bpId, ct.id, {
            firstName: ct.firstName,
            role: roleMap[ct.contactRole] ?? 1,
            phone,
            email,
          });
        } else {
          await businessPartnerRegistrationFacade.createContact(bpId, {
            firstName: newCustName.trim().split(" ")[0],
            role: 1,
            phone,
            email,
            isPrimary: true,
          });
        }
      }

      setValue("customerId", bpId, { shouldValidate: true, shouldDirty: true });
      const profile = await loadCustomerProfile(bpId);
      setCustomerProfile(profile);
      setModalNewCustomer(false);
    } catch (err: unknown) {
      setNewCustError(
        extractErrorText(
          err,
          err instanceof Error && err.message ? err.message : "Error al guardar.",
        ),
      );
    }
    setNewCustSaving(false);
  }, [
    newCustIsEdit,
    newCustName,
    newCustId,
    newCustIdType,
    newCustAddress,
    newCustEmail,
    newCustPhone,
    getValues,
    setValue,
    loadCustomerProfile,
  ]);

  // ── Return ─────────────────────────────────────────────────────────
  return {
    // Page
    tab,
    setTab,
    listItems,
    listLoading,
    listSearch,
    setListSearch,
    saving,
    saveError,
    setSaveError,
    editing,

    // Form (RHF)
    form,
    register,
    control,
    errors,
    formWatch,
    setValue,
    getValues,
    reset,

    // Lines
    lines,
    addLineWithItem,
    removeLine,
    updateLine,
    lineKey,
    handleWarehouseChange,
    onUpdateLineWarehouse,
    onUpdateLinePresentation,

    // Payments
    payments,
    setInvoicePayments,
    paymentsForAdditionalMethod,
    isCashMethodId,
    payKey,
    setPayKey,
    paymentMethods,
    bankAccountOptions,
    // Único cómputo de "total ya cobrado" — evita que el checklist y la grilla de formas de
    // cobro recalculen el mismo reduce() por separado (ver SalesPage.tsx).
    paidTotal,
    // POS-COLLECTION-SSOT-01: estado único del cobro (resumen, mensaje, Falta/Vuelto).
    collection,

    // Customer
    customerProfile,
    setCustomerProfile,
    handleCustomerChange,
    priceListHeader,

    // SALES-CUSTOMER-CHANGE-REPRICE-UX-06C2: modal de confirmación de repricing al cambiar de
    // cliente con líneas ya cargadas — repricingModal es null salvo cuando hay al menos una
    // línea con diferencia de precio pendiente de confirmar.
    repricingLoading: repricing.loading,
    repricingModal: repricing.pending,
    confirmRepricing,
    cancelRepricing,

    // Reference data
    paymentTermsList,
    warehouses,
    selectedWarehouseId,
    setSelectedWarehouseId,
    vatRatesMap,
    iceRatesMap,
    sriDocTypes,
    sriPaymentMethods,
    sriIdTypes,

    // Cash session (contexto operativo POS — ICurrentCashSession vía GET /cash-sessions/my)
    hasCashSession,
    myCashSession,
    cashSessionCheckError,
    refreshCashSession,
    branchName,

    // SALES-MANUAL-CASH-MOVEMENT-INTEGRATION-08 — mismo flujo que /treasury/cash, ver arriba.
    manualCashMovement,

    // Derived
    isDraft,
    readOnly,
    fieldDisabled,
    canEmit,
    emitBlockers,
    lineIssues,
    focusFirstInvalidLine,
    configStatus,
    paymentOk,
    summary,
    grandTotal,
    totalDiscount,
    taxBreakdown,
    isElectronic,
    emissionType,
    sessionEmissionType,
    // Estado discreto de conectividad SRI (ver refreshSriConnectivity arriba) — "Available" /
    // "Unavailable" / "Unknown" ("no verificado", incl. antes de que resuelva el primer check).
    sriAvailability: sriStatus?.sriAvailability ?? "Unknown",
    selectedPt,
    isCreditTerm,

    // Consumidor Final — política fiscal (runtime context, autoridad backend)
    runtimeContext,
    isConsumerFinalCustomer,
    consumerFinalPolicy,
    consumerFinalAmountExceeded,

    // Actions
    fetchList,
    resetForm,
    clearForm,
    loadForEdit,
    handleCancel,
    handleGenerateElectronicDocument,

    // Issue flow (Nueva Venta → Emitir Factura → Confirmación → Emisión → Éxito)
    issuePhase,
    issueStepIndex,
    issueResult,
    issueError,
    xmlDownloading,
    productSearchFocusKey,
    openIssueFlow,
    closeIssueFlow,
    confirmIssue,
    retryIssue,
    startNewSale,
    handleDownloadXml,

    // Modals
    modalCancelReason,
    setModalCancelReason,
    modalNewCustomer,
    setModalNewCustomer,
    modalDetail,
    setModalDetail,
    modalCredit,
    setModalCredit,

    // Quick customer
    newCustId,
    setNewCustId,
    newCustName,
    setNewCustName,
    newCustIdType,
    setNewCustIdType,
    newCustAddress,
    setNewCustAddress,
    newCustEmail,
    setNewCustEmail,
    newCustPhone,
    setNewCustPhone,
    newCustIsEdit,
    newCustSaving,
    newCustError,
    openNewCustomerModal,
    openEditCustomerModal,
    handleSaveQuickCustomer,

    // Payment detail modal
    detailMethodId,
    setDetailMethodId,
    detailMethodType,
    setDetailMethodType,
    detailMethodName,
    setDetailMethodName,
    detailRows,
    setDetailRows,
    detailKey,
    setDetailKey,

    // Credit modal
    creditAmount,
    setCreditAmount,
    creditRows,
    setCreditRows,
    simulateCreditInstallments,
    confirmedScheduleRows,
    setConfirmedScheduleRows,
    scheduleIsManual,
    setScheduleIsManual,

    // Cash payment (Efectivo recibido / Vuelto) — derivados de `collection`
    cashReceivedInput,
    setCashReceivedInput,
    cashReceived: cashReceivedValue,
    cashDue,
    cashChange,

    // Stock (advertencia preventiva antes de emitir)
    hasInsufficientStock,
  };
}

export type SalesPageContext = ReturnType<typeof useSalesPage>;
