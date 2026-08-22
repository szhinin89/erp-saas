import { useCallback, useEffect, useMemo, useState } from "react";
import axios from "axios";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import type {
  SalesInvoiceDto,
  SalesListItemDto,
  PaymentMethodDto,
  PaymentMethodDetailType,
  CardDetailInput,
  TransferDetailInput,
  ChequeDetailInput,
} from "../api/salesService";
import { salesService } from "../api/salesService";
import { warehouseService } from "../../inventory/warehouses/api/warehouseService";
import type { WarehouseDto } from "../../inventory/warehouses/api/warehouseService";
import { stockService } from "../../inventory/stock/api/stockService";
import type { ItemWarehouseAvailabilityDto } from "../../inventory/stock/api/stockService";
import { electronicDocumentAccessFacade } from "../../electronicDocuments/facades/electronicDocumentAccessFacade";
import type { ElectronicDocumentXmlVariant } from "../../electronicDocuments/facades/electronicDocumentAccessFacade";
import { downloadTextFile } from "../../electronicDocuments/monitor/utils/download";
import type { InvoiceItemSearchResultDto } from "../api/invoiceItemSearchService";
import type {
  CustomerPickerRow,
  LocationTypeValue,
  ContactRoleValue,
} from "../../masterData/types/businessPartner.types";
import { RoleTypeEnum } from "../../masterData/types/businessPartner.types";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";
import {
  bpLocationService,
  bpContactService,
} from "../../masterData/api/businessPartnerService";
import { paymentTermService } from "../../masterData/api/paymentTermService";
import type { PaymentTermDto } from "../../masterData/api/paymentTermService";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import { salesDefaultsService } from "../api/salesDefaultsService";
import type { SalesInvoiceDefaultsDto } from "../api/salesDefaultsService";
import { salesRuntimeContextService } from "../api/salesRuntimeContextService";
import type { SalesRuntimeContextDto } from "../api/salesRuntimeContextService";
import { salesItemPricingService } from "../api/salesItemPricingService";
import {
  loadDecimalConfig,
  getDecimalConfig,
} from "../../../lib/config/decimal.config";
import {
  todayIso,
  toLocalIsoDate,
} from "../../../lib/formatters/dateFormatters";
import { normalizeOptionalCode } from "../../../lib/sanitizers";
import {
  readApiErrorMessage,
  readApiErrorMessages,
  logApiDevError,
} from "../../lib/apiError";
import {
  calcSummary,
  formatVatLabel,
  lineExceedsStock,
  findMergeableLineIndex,
  resolveDefaultLinePresentation,
  resolveLinePresentationChange,
  type TaxBreakdownEntry,
} from "../utils/salesCalc";
import { applyServerErrors } from "../../lib/validationErrors";
import { cajaSessionLookupFacade } from "../../caja/facades/cajaSessionLookupFacade";
import type { CashSessionDto } from "../../caja/facades/cajaSessionLookupFacade";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { message } from "../../../lib/messages";
import {
  salesInvoiceSchema,
  emptySalesInvoiceForm,
  type SalesInvoiceFormValues,
  type SalesLineFormValues,
  type SalesPaymentFormValues,
} from "../schemas/salesInvoiceSchema";
import { INVOICE_PAYMENT_TOLERANCE } from "../constants/tolerances";

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
  paymentDays: number;
  installments: number;
  daysBetweenInstallments: number;
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

// Pasos 0-1 (Validando/Guardando) son awaits reales del formulario y de
// persistDraft. Pasos 2-5 ocurren dentro de un único request atómico en el
// servidor (numeración + XML + firma + envío SRI + autorización) — no hay
// progreso real intermedio que consultar, por eso se muestran escalonados
// mientras se espera esa respuesta única (ver simulateRemainingSteps).
export const ISSUE_STEPS = [
  "Validando",
  "Guardando",
  "Generando XML",
  "Firmando",
  "Enviando al SRI",
  "Consultando autorización",
] as const;

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
    const rows = await businessPartnerFacade.searchCustomersForPicker(
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

/** Identifica la forma de pago "Efectivo" del catálogo tenant-editable — código sembrado
 * por SalesBootstrapStep (ver CLAUDE.md/backend), no un enum fijo. Solo habilita el campo
 * de "Monto recibido / Vuelto" en pantalla; no afecta reglas de negocio del backend. */
function isCashPaymentMethod(pm: PaymentMethodDto | undefined): boolean {
  return !!pm && pm.code.trim().toUpperCase() === "EFECTIVO";
}

/** Avanza el índice de paso mostrado mientras se espera la respuesta de /authorize; se detiene en el último paso si la respuesta tarda más que la animación. */
function simulateRemainingSteps(
  setIndex: (updater: (i: number) => number) => void,
  toIndex: number,
  stepMs = 750,
) {
  const id = setInterval(() => {
    setIndex((i) => Math.min(i + 1, toIndex));
  }, stepMs);
  return { stop: () => clearInterval(id) };
}

// ── Hook ───────────────────────────────────────────────────────────────

export function useSalesPage() {
  // ── Page state ─────────────────────────────────────────────────────
  const [tab, setTab] = useState<Tab>("nuevo");
  const [listItems, setListItems] = useState<SalesListItemDto[]>([]);
  const [listLoading, setListLoading] = useState(false);
  const [listSearch, setListSearch] = useState("");

  const [saving, setSaving] = useState(false);
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

  // ── Customer state ─────────────────────────────────────────────────
  const [customerProfile, setCustomerProfile] =
    useState<CustomerProfile | null>(null);

  // ── Reference data ─────────────────────────────────────────────────
  const [paymentTermsList, setPaymentTermsList] = useState<PaymentTermDto[]>(
    [],
  );
  const [paymentMethods, setPaymentMethods] = useState<PaymentMethodDto[]>([]);
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

  // ── Cash payment: monto recibido / vuelto (solo UI — el backend no exige este dato) ──
  const [cashReceived, setCashReceived] = useState(0);

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
    setError: setFieldError,
    formState: { errors, isDirty },
  } = form;

  const formWatch = watch();
  const lines = watch("lines");
  const payments = watch("payments");

  // ── Derived state ──────────────────────────────────────────────────
  const isDraft = !editing || editing.status === "Draft";
  const readOnly = !isDraft;
  const fieldDisabled = saving || readOnly;

  const summary = useMemo(
    () => calcSummary(lines, vatRatesMap, iceRatesMap),
    [lines, vatRatesMap, iceRatesMap],
  );

  // Única fuente de verdad de "¿se puede emitir?" — la usan tanto el botón
  // "Emitir Factura" como el atajo F8, para no duplicar la validación. También expuesta
  // como `paymentOk` para que el checklist visual de SalesPage.tsx no la recalcule aparte.
  const hasCustomer = !!formWatch.customerId?.trim();
  const hasLines = lines.length > 0;
  const paidTotal = payments.reduce((s, p) => s + (Number(p.amount) || 0), 0);
  const paymentOk =
    summary.total > 0 &&
    paidTotal > 0 &&
    Math.abs(summary.total - paidTotal) < INVOICE_PAYMENT_TOLERANCE;
  // Efectivo: si hay un cobro asignado a la forma de pago "Efectivo", el monto recibido
  // no puede ser menor al cobro requerido — de lo contrario no hay vuelto que calcular.
  const cashPaymentEntry = payments.find((p) =>
    isCashPaymentMethod(paymentMethods.find((pm) => pm.id === p.paymentMethodId)),
  );
  const cashDue = cashPaymentEntry?.amount || 0;
  const cashChangeFactor = 10 ** getDecimalConfig().totalAmount;
  const cashChange =
    cashDue > 0
      ? Math.max(0, Math.round((cashReceived - cashDue) * cashChangeFactor) / cashChangeFactor)
      : 0;
  const cashInsufficient =
    cashDue > 0 && cashReceived + INVOICE_PAYMENT_TOLERANCE < cashDue;

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

  const canEmit =
    !fieldDisabled &&
    hasCustomer &&
    hasLines &&
    hasCashSession === true &&
    paymentOk &&
    !cashInsufficient &&
    !hasInsufficientStock &&
    !consumerFinalAmountExceeded;

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

  // Único punto de decisión Electronic/Physical de toda la pantalla de Ventas — fuente de verdad:
  // CashRegister → EmissionPoint → EmissionType, resuelto en vivo por el backend en
  // myCashSession.emissionType (disponible desde que carga la pantalla, sin esperar a que exista
  // un borrador). Se usa editing.emissionType únicamente como respaldo al ver/editar una factura
  // ya creada fuera de una sesión de caja activa (ej. sin sesión abierta en este navegador).
  const isElectronic =
    (myCashSession?.emissionType ?? editing?.emissionType) === "Electronic";

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
        paymentTermService
          .list()
          .then(setPaymentTermsList)
          .catch(() => {}),
        salesService
          .listPaymentMethods(true)
          .then(setPaymentMethods)
          .catch(() => {}),
        loadDecimalConfig(),
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
        warehouseService.list("active"),
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
          businessPartnerFacade.getBusinessPartner(bpId),
          bpLocationService.list(bpId, true).catch(() => []),
          bpContactService.list(bpId, true).catch(() => []),
          businessPartnerFacade.getTradingSettings(bpId),
        ]);

        const {
          installments,
          daysBetweenInstallments: daysBetween,
          paymentDays,
          paymentTermId: ptId,
        } = trading;

        if (ptId && paymentTermsList.some((p) => p.id === ptId)) {
          // 1. Condición de pago del cliente (máxima prioridad)
          setValue("paymentTermId", ptId, { shouldDirty: true });
        } else if (paymentDays > 0 && paymentTermsList.length > 0) {
          // 2. Buscar por días de crédito del cliente
          const match = paymentTermsList.find(
            (p) => p.isActive && p.totalDays === paymentDays,
          );
          if (match) setValue("paymentTermId", match.id, { shouldDirty: true });
        } else {
          // 3. Default de empresa (único punto de fallback para condición de pago)
          const companyDefault = tenantDefaults?.defaultPaymentTermId;
          if (
            companyDefault &&
            paymentTermsList.some((p) => p.id === companyDefault && p.isActive)
          ) {
            setValue("paymentTermId", companyDefault, { shouldDirty: true });
          }
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
          paymentDays,
          installments,
          daysBetweenInstallments: daysBetween,
          paymentTermId: ptId,
        };
      } catch {
        return null;
      }
    },
    [paymentTermsList, setValue, getValues, tenantDefaults],
  );

  // ── Line operations ────────────────────────────────────────────────
  const addLineWithItem = useCallback(
    async (item: InvoiceItemSearchResultDto) => {
      const selectedWh = warehouses.find((w) => w.id === selectedWarehouseId);

      // Kardex: la bodega de despacho es obligatoria por línea para ítems que
      // controlan inventario — se toma la bodega activa del buscador al momento
      // de agregar el ítem (una misma factura puede combinar líneas de bodegas distintas).
      if (item.tracksStock && !selectedWarehouseId) {
        message.error("Seleccione una bodega antes de agregar este producto.");
        return;
      }

      // Precio dinámico: SSOT es el Pricing Engine v2 (PricingResolver), resuelto
      // puntualmente al seleccionar el ítem — no se usa el precio base del buscador.
      let pricing;
      try {
        pricing = await salesItemPricingService.get(item.id);
      } catch (err: unknown) {
        message.error(
          extractErrorText(err, "No se pudo obtener el precio del producto."),
        );
        return;
      }

      const pvp = pricing.unitPrice ?? undefined;
      const cost =
        item.averageCost != null && item.averageCost > 0
          ? item.averageCost
          : undefined;
      const stockQty = item.availableStock ?? undefined;
      const vatCode = pricing.vatCode ?? "";
      const iceCode = normalizeOptionalCode(pricing.iceCode);
      const lineWarehouseId = item.tracksStock ? selectedWarehouseId : null;

      // SALES-PRESENTATIONS-03: por defecto se vende en unidad base (comportamiento actual
      // preservado) — salvo que el texto buscado haya coincidido con el barcode de una
      // presentación específica (ItemPackagingLevel.Barcode), en cuyo caso esa presentación se
      // autoselecciona (regla 2/5 de la tarea). IsSaleDefault deliberadamente NO se usa todavía
      // (backend tampoco lo consume en esta fase — ver SalesLinePackagingResolver).
      const defaultPresentation = resolveDefaultLinePresentation(item);
      const { packagingLevelId, uomCode, conversionFactor } = defaultPresentation;
      const unitPrice = (pvp ?? 0) * conversionFactor;

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
        _key: lineKey,
        itemId: item.id,
        warehouseId: lineWarehouseId,
        description: `${item.sku} — ${item.description}`,
        quantity: 1,
        unitPrice,
        vatCode,
        discountPct: 0,
        iceCode: iceCode ?? undefined,
        packagingLevelId,
        uomCode,
        baseUomCode: item.baseUomCode,
        conversionFactor,
        _sku: item.sku,
        _name: item.description,
        _pvp: pvp,
        _cost: cost,
        _stockQty: stockQty,
        _stockWarehouse: selectedWh?.name,
        _tracksStock: item.tracksStock,
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
        currentLines.map((l) =>
          l._key === key ? { ...l, [field]: value } : l,
        ),
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
      setSelectedWarehouseId(id);
      const currentLines = getValues("lines");
      setValue(
        "lines",
        currentLines.map((l) =>
          l._tracksStock ? { ...l, warehouseId: id } : l,
        ),
        { shouldValidate: true, shouldDirty: true },
      );

      const affected = currentLines.filter((l) => l._tracksStock && l.itemId);
      void Promise.all(
        affected.map(async (l) => {
          try {
            const options = await stockService.getWarehouseAvailability(
              l.itemId!,
            );
            const match = options.find((o) => o.warehouseId === id);
            if (!match) return;
            const latest = getValues("lines");
            setValue(
              "lines",
              latest.map((x) =>
                x._key === l._key
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

  // ── Form reset ─────────────────────────────────────────────────────
  const resetForm = useCallback(async () => {
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
    setCashReceived(0);
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
  const handleCustomerChange = useCallback(
    async (c: CustomerPickerRow | null) => {
      setValue("customerId", c?.id ?? "", {
        shouldValidate: true,
        shouldDirty: true,
      });
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
          paymentDays: 0,
          installments: 0,
          daysBetweenInstallments: 0,
          paymentTermId: null,
        });

        const mappedLines: SalesLineFormValues[] = inv.lines.map((l, i) => ({
          _key: i + 1,
          itemId: l.itemId,
          warehouseId: l.warehouseId,
          description: l.description,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          vatCode: l.vatCode,
          discountPct: l.discountPct,
          iceCode: l.iceCode,
          notes: l.notes,
          packagingLevelId: l.packagingLevelId,
          uomCode: l.uomCode,
          baseUomCode: l.baseUomCode,
          conversionFactor: l.conversionFactor,
          _sku: l.snapshotSku ?? undefined,
          _name: l.snapshotItemName ?? undefined,
          // El backend solo persiste warehouseId para ítems que controlan stock
          // (SalesLineBuilder) — su presencia es una señal segura de _tracksStock.
          _tracksStock: l.warehouseId != null,
        }));

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
          reference: p.reference,
          cardDetail: p.cardDetail ?? undefined,
          transferDetail: p.transferDetail ?? undefined,
          chequeDetail: p.chequeDetail ?? undefined,
        })),
      };

      return editing
        ? salesService.update(editing.id, { ...payload, id: editing.id })
        : salesService.create(payload);
    },
    [editing],
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
    if (!canEmit || issuePhase !== "idle") return;
    setIssueError(null);
    setIssuePhase("confirm");
  }, [canEmit, issuePhase]);

  const closeIssueFlow = useCallback(() => {
    if (issuePhase === "processing") return; // no se puede cerrar mientras se emite
    setIssuePhase("idle");
    setIssueError(null);
  }, [issuePhase]);

  const confirmIssue = useCallback(async () => {
    if (issuePhase === "processing") return; // reentrancia: doble clic en el modal
    setIssuePhase("processing");
    setIssueStepIndex(0); // Validando
    setSaving(true);
    try {
      let invoiceId = editing?.id;

      if (!editing || isDirty) {
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
          const applied = applyServerErrors(err, setFieldError, (msg) =>
            setSaveError({ title: "No se puede guardar la venta.", detail: msg }),
          );
          if (!applied)
            setSaveError(
              buildSalesErrorNotice(
                err,
                "No se puede guardar la venta.",
                "No se pudieron guardar los cambios pendientes.",
              ),
            );
          setIssuePhase("idle");
          return; // guardado falló — se cancela la emisión, el usuario corrige en el formulario
        }

        invoiceId = saved.id;
        setEditing(saved);
        reset(getValues()); // limpia isDirty sin navegar ni alterar lo mostrado en pantalla
      }

      // Generando XML / Firmando / Enviando al SRI / Consultando autorización
      // ocurren dentro de un único request atómico — se muestran escalonados
      // mientras se espera esa respuesta (ver comentario de ISSUE_STEPS).
      const stepTimer = simulateRemainingSteps(
        setIssueStepIndex,
        ISSUE_STEPS.length - 1,
      );
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
          setSaveError(
            buildSalesErrorNotice(
              err,
              "No se puede emitir la factura.",
              "Revise los datos de la factura antes de emitir.",
            ),
          );
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
      } finally {
        stepTimer.stop();
      }

      setIssueStepIndex(ISSUE_STEPS.length - 1);
      setIssueResult(authorized);
      setIssuePhase("success");
      fetchList(); // refresca el listado en segundo plano — sin recargar la página
    } finally {
      setSaving(false);
    }
  }, [
    issuePhase,
    editing,
    isDirty,
    trigger,
    form,
    getValues,
    persistDraft,
    setFieldError,
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
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key !== "F8") return;
      if (tab !== "nuevo" || issuePhase !== "idle" || !canEmit) return;
      e.preventDefault();
      openIssueFlow();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [tab, issuePhase, canEmit, openIssueFlow]);

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
      const count =
        selectedPt?.installments ?? customerProfile?.installments ?? 1;
      const interval =
        selectedPt?.daysBetweenInstallments ??
        customerProfile?.daysBetweenInstallments ??
        30;
      const factor = 10 ** getDecimalConfig().totalAmount;
      const base = Math.round((amount / count) * factor) / factor;
      const rows: CreditRow[] = [];
      let accumulated = 0;
      const today = new Date();
      for (let i = 1; i <= count; i++) {
        const due = new Date(today);
        due.setDate(due.getDate() + interval * i);
        const isLast = i === count;
        const amt = isLast
          ? Math.round((amount - accumulated) * factor) / factor
          : base;
        rows.push({ number: i, dueDate: toLocalIsoDate(due), amount: amt });
        accumulated += amt;
      }
      return rows;
    },
    [selectedPt, customerProfile],
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
        await businessPartnerFacade.updateBusinessPartner(bpId, {
          legalName: newCustName.trim(),
          tradeName: null,
          countryCode: null,
        });
      } else {
        const bp = await businessPartnerFacade.createBusinessPartner({
          identificationType: newCustIdType,
          identificationNumber: newCustId.trim(),
          legalName: newCustName.trim(),
        });
        bpId = bp.id;
        await businessPartnerFacade.assignRole(bp.id, {
          roleType: RoleTypeEnum.Customer,
        });
      }

      const [locations, contacts] = await Promise.all([
        bpLocationService.list(bpId, true).catch(() => []),
        bpContactService.list(bpId, true).catch(() => []),
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
          await businessPartnerFacade.updateLocation(bpId, loc.id, {
            name: loc.name,
            type: locTypeMap[loc.locationType] ?? 1,
            purpose: purposeBits,
            addressLine: addr,
          });
        } else {
          await businessPartnerFacade.createLocation(bpId, {
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
          await businessPartnerFacade.updateContact(bpId, ct.id, {
            firstName: ct.firstName,
            role: roleMap[ct.contactRole] ?? 1,
            phone,
            email,
          });
        } else {
          await businessPartnerFacade.createContact(bpId, {
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
    payKey,
    setPayKey,
    paymentMethods,
    // Único cómputo de "total ya cobrado" — evita que el checklist y la grilla de formas de
    // cobro recalculen el mismo reduce() por separado (ver SalesPage.tsx).
    paidTotal,

    // Customer
    customerProfile,
    setCustomerProfile,
    handleCustomerChange,

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

    // Derived
    isDraft,
    readOnly,
    fieldDisabled,
    canEmit,
    paymentOk,
    summary,
    grandTotal,
    totalDiscount,
    taxBreakdown,
    isElectronic,
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

    // Cash payment (Monto recibido / Vuelto)
    cashReceived,
    setCashReceived,
    cashDue,
    cashChange,
    cashInsufficient,

    // Stock (advertencia preventiva antes de emitir)
    hasInsufficientStock,
  };
}

export type SalesPageContext = ReturnType<typeof useSalesPage>;
