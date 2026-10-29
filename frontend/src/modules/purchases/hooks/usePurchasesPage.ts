import { usePurchaseProductSearch } from "./usePurchaseProductSearch";
import { restoreXmlAllocation, replaceBreakdownRows } from "../utils/purchaseXmlBreakdown";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useForm, type FieldPath } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import type {
  PurchaseAccessKeyLookupDto,
  PurchaseInvoiceDto,
  PurchaseListItemDto,
  RetentionPreviewDto,
} from "../api/purchaseService";
import { purchaseService } from "../api/purchaseService";
import { summarizeCostDistributions, usePurchaseCostDistributions } from "./usePurchaseCostDistributions";
import {
  purchaseRetentionFacade,
  SRI_ANNULMENT_REQUIRED_CODE,
  type RetentionDocumentDto,
} from "../../retentions/facades/purchaseRetentionFacade";
import {
  emissionPointLookupFacade,
  type EmissionPointListItemDto,
} from "../../emissionPoints/facades/emissionPointLookupFacade";
import {
  buildPurchaseRetentionIntent,
  canApplyPurchaseRetention,
  emptyPurchaseRetentionIntent,
  isPurchaseRetentionIntentComplete,
  type PurchaseRetentionIntentState,
} from "../utils/purchaseRetentionIntent";
import { downloadBlob } from "../../../lib/download";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import {
  purchaseReceptionService,
  type PurchaseDraftDto,
  type ResolveReceptionLinesResult,
  type ResolvedReceptionLine,
} from "../api/purchaseReceptionService";
import {
  applyResolvedReceptionLines,
  selectPendingProductSources,
  summarizeXmlLines,
} from "../utils/pendingProductsResolution";
import { toDateTimeLocalInputValue } from "../../../lib/formatters/dateFormatters";
import { itemLookupFacade } from "../../items/facades/itemLookupFacade";
import { useItemTypeOptions } from "../../items/facades/itemTypeLookupFacade";
import type { ItemDto } from "../../../types/items";
import type { SupplierPickerRow } from "../../masterData/facades/supplierPickerFacade";
import { businessPartnerLookupFacade } from "../../masterData/facades/businessPartnerLookupFacade";
import { businessPartnerPurchaseSettingsFacade } from "../../masterData/facades/businessPartnerPurchaseSettingsFacade";
import { warehouseLookupFacade } from "../../inventory/facades/warehouseLookupFacade";
import type { WarehouseDto } from "../../inventory/facades/warehouseLookupFacade";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { paymentTermLookupFacade } from "../../masterData/facades/paymentTermLookupFacade";
import type { PaymentTermDto } from "../../masterData/facades/paymentTermLookupFacade";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import type {
  SriDocTypeLookup,
  SriPaymentMethodLookup,
  SriTaxSupportLookup,
} from "../../items/facades/sriLookupFacade";
import {
  calcSummary,
  documentalTaxSnapshot,
  generateScheduleRows,
  roundToTotalAmount,
} from "../utils/purchaseCalc";
import { applyServerErrors } from "../../lib/validationErrors";
import {
  readApiErrorCode,
  readApiErrorMessage,
  readApiErrorMessages,
  formatApiRequestError,
} from "../../lib/apiError";
import { message } from "../../../lib/messages";
import { useI18n } from "../../../i18n/i18n";
import { usePrecisionPolicy } from "../../../hooks/usePrecisionPolicy";
import { resolvePrecisionDecimals } from "../../../lib/config/precisionPolicy.config";
import {
  todayIso,
  addDaysIso,
} from "../../../lib/formatters/dateFormatters";
import {
  createPurchaseInvoiceSchema,
  emptyPurchaseInvoiceForm,
  type PurchaseInvoiceFormValues,
  type PurchaseLineFormValues,
} from "../schemas/purchaseInvoiceSchema";
import {
  buildPurchaseDraftExtrasSnapshot,
  buildPurchaseDraftPayload,
  buildPurchaseDraftSnapshot,
  type PurchaseDraftExtras,
} from "../utils/purchaseDraftPayload";
import {
  buildSupplierInactiveMessage,
  buildSupplierProfile,
  type SupplierProfile,
} from "../utils/supplierProfile";
import {
  buildPurchaseLineFromItem,
  normalizePurchaseLinePresentation,
} from "../utils/purchaseItemProfile";
import { resolvePurchaseDocumentState } from "../utils/purchaseDocumentState";
import { resolveXmlOriginPresentation } from "../utils/purchaseLinePresentation";
import {
  getPurchaseLineBlockingReasons,
  getPurchaseLineReadiness,
  getPurchaseLineSaveBlockingReasons,
} from "../utils/purchaseLineReadiness";
import {
  recoverLearnedMatch,
  xmlEvidenceFromDraftLine,
} from "../utils/purchaseXmlEvidence";

// ── Types ──────────────────────────────────────────────────────────────

export type Tab = "listado" | "nuevo";

// Forma mínima esperada de un error HTTP (axios) para extraer un mensaje legible;
// todos los accesos son opcionales porque el shape real puede variar según el catch.
type ApiErrorLike = {
  response?: {
    data?: {
      message?: { user?: string };
      data?: { errors?: string[] };
    };
  };
  message?: string;
};

/**
 * PILOT-PURCHASE-UI-FIX-01 — motivo real devuelto por el backend: `data.errors` primero (detalle
 * del handler) y `message.user` (mensaje genérico del catálogo) después, vía readApiErrorMessages.
 * El fallback genérico solo se usa si no hay ningún detalle.
 */
function resolveApiErrorDetails(err: unknown, fallback: string): [string, ...string[]] {
  const messages = readApiErrorMessages(err);
  if (messages.length > 0) return [messages[0], ...messages.slice(1)];
  const e = err as ApiErrorLike;
  return [e?.response?.data?.data?.errors?.[0] ?? e?.response?.data?.message?.user ?? fallback];
}

export type ScheduleRow = {
  number: number;
  dueDate: string;
  amount: number;
  notes: string;
};

// 'all' o un ItemTypeId (Guid) del catálogo tenant-editable /api/v1/item-types.
type SearchFilter = "all" | string;

const DUPLICATE_PURCHASE_TITLE = "No se puede crear esta compra.";
const DUPLICATE_PURCHASE_DETAIL =
  "Ya existe una compra registrada con esta clave de acceso SRI.";
const SUPPLIER_CODE_CONFLICT_DETAIL =
  "El código de proveedor ya está asociado a otro ítem.";

// ── Hook ───────────────────────────────────────────────────────────────

export function usePurchasesPage() {
  const { t } = useI18n();
  // Policy de la empresa para la presentación/readiness de líneas (utilidades puras, 06).
  const precisionPolicy = usePrecisionPolicy();
  const activeBranchId = useActiveBranchStore((s) => s.branch)?.id ?? null;
  // ── Page state ─────────────────────────────────────────────────────
  const [tab, setTab] = useState<Tab>("nuevo");
  const [listItems, setListItems] = useState<PurchaseListItemDto[]>([]);
  const [listLoading, setListLoading] = useState(false);
  const [listSearchInput, setListSearchInput] = useState("");
  const [listSearch, setListSearch] = useState("");
  const [listStatus, setListStatus] = useState("");
  const [listPage, setListPage] = useState(1);
  const [listTotal, setListTotal] = useState(0);
  const listPageSize = 25;
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [saveErrorDetails, setSaveErrorDetails] = useState<string[]>([]);
  const [duplicateAccessKey, setDuplicateAccessKey] =
    useState<PurchaseAccessKeyLookupDto | null>(null);
  const [duplicateAccessKeyChecking, setDuplicateAccessKeyChecking] =
    useState(false);
  const duplicateAccessKeySeq = useRef(0);
  const [editing, setEditing] = useState<PurchaseInvoiceDto | null>(null);
  // Huella del borrador tal como quedó persistido al cargarlo (null = sin borrador persistido).
  const [savedDraftSnapshot, setSavedDraftSnapshot] = useState<string | null>(null);
  // PURCHASE-DRAFT-RECOVERY D3 — huella guardada de cuotas manuales + intención de retención.
  const [savedExtrasSnapshot, setSavedExtrasSnapshot] = useState<string | null>(null);
  // Aviso no bloqueante cuando el borrador viene de una Recepción Electrónica con
  // ProcessingStatus PROCESSED_WITH_WARNINGS (algunas líneas del XML no se pudieron interpretar).
  const [receptionProcessingNotice, setReceptionProcessingNotice] = useState<
    string | null
  >(null);

  // ── Supplier ───────────────────────────────────────────────────────
  const [supplierProfile, setSupplierProfile] =
    useState<SupplierProfile | null>(null);
  const profileCache = useRef<Map<string, SupplierProfile>>(new Map());

  // ── Reference data ─────────────────────────────────────────────────
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [sriDocTypes, setSriDocTypes] = useState<SriDocTypeLookup[]>([]);
  const [sriDocTypesLoaded, setSriDocTypesLoaded] = useState(false);
  const [sriDocTypesLoadFailed, setSriDocTypesLoadFailed] = useState(false);
  const [sriPaymentMethods, setSriPaymentMethods] = useState<
    SriPaymentMethodLookup[]
  >([]);
  const [sriTaxSupports, setSriTaxSupports] = useState<SriTaxSupportLookup[]>(
    [],
  );
  const [paymentTermsList, setPaymentTermsList] = useState<PaymentTermDto[]>(
    [],
  );
  const [vatRatesMap, setVatRatesMap] = useState<Record<string, number>>({});
  const [iceRatesMap, setIceRatesMap] = useState<Record<string, number>>({});

  // ── Payment schedule ───────────────────────────────────────────────
  const [ptInstallments, setPtInstallments] = useState(1);
  const [ptDaysBetween, setPtDaysBetween] = useState(0);
  const [ptRows, setPtRows] = useState<ScheduleRow[]>([]);
  const [ptLoaded, setPtLoaded] = useState(false);
  // PURCHASE-DRAFT-RECOVERY F2 — true si las cuotas fueron personalizadas (editadas, agregadas,
  // eliminadas o regeneradas con nro./días distintos a la condición de pago, o restauradas desde el
  // borrador guardado). Solo las manuales se guardan con el borrador; las generadas se recalculan.
  const [ptManual, setPtManual] = useState(false);

  // ── Retención ────────────────────────────────────────────────────
  // ZH-PURCHASE-RETENTION-CONFIRM-01: la retención se define en el BORRADOR y se emite dentro de
  // "Confirmar compra" (RetentionIntent). whPreview = vista previa del backend (elegibilidad +
  // montos propuestos, se carga sola al abrir/recargar el borrador); retentionIntent = decisión del
  // usuario (emitir o no, punto de emisión, fecha). retention = retención ya emitida (compra
  // confirmada), sobre la que solo quedan XML/RIDE/registro SRI/anulación.
  const [whPreview, setWhPreview] = useState<RetentionPreviewDto | null>(null);
  const [whPreviewError, setWhPreviewError] = useState<string | null>(null);
  const [retentionIntent, setRetentionIntent] = useState<PurchaseRetentionIntentState>(() =>
    emptyPurchaseRetentionIntent(todayIso()),
  );
  const [emissionPoints, setEmissionPoints] = useState<EmissionPointListItemDto[]>([]);
  const [retention, setRetention] = useState<RetentionDocumentDto | null>(
    null,
  );
  const [whLoading, setWhLoading] = useState(false);
  const [electronicPending, setElectronicPending] = useState(false);
  const { has } = usePermissionsUi();
  const canReadEmissionPoints = has("settings.emission-points.view");

  // ── Modals ─────────────────────────────────────────────────────────
  const [modalConfirm, setModalConfirm] = useState(false);
  const [breakdownSourceId, setBreakdownSourceId] = useState<string | null>(null);
  const [modalDiscount, setModalDiscount] = useState(false);
  const [modalCancelReason, setModalCancelReason] = useState(false);
  // FASE 5 PURCHASE-UX — confirmación de "Descartar borrador" (motivo opcional).
  const [modalDiscardDraft, setModalDiscardDraft] = useState(false);
  // ZH-RETENTION-SRI-ANNULMENT-01 — la retención ya está AUTORIZADA: el motivo se conserva para
  // iniciar la anulación ante el SRI sin pedirlo de nuevo.
  const [sriAnnulmentReason, setSriAnnulmentReason] = useState<string | null>(null);
  // COMPRAS-METODO-ZH-01B — resolución masiva de líneas XML sin producto.
  const [modalPendingProducts, setModalPendingProducts] = useState(false);
  // PURCHASE-DRAFT-RECOVERY D3 — salida pendiente de confirmación por cambios sin guardar.
  const [pendingLeave, setPendingLeave] = useState<(() => void) | null>(null);

  // ── Collapsible sections ───────────────────────────────────────────
  const [showElectronic, setShowElectronic] = useState(false);
  const [showNotes, setShowNotes] = useState(false);

  // ── Global search ──────────────────────────────────────────────────
  const [globalQuery, setGlobalQuery] = useState("");
  const [globalOpen, setGlobalOpen] = useState(false);
  const [globalFilter, setGlobalFilter] = useState<SearchFilter>("all");
  const itemTypesState = useItemTypeOptions();
  const itemTypeOptions = itemTypesState.data ?? [];
  const [globalFocusIdx, setGlobalFocusIdx] = useState(-1);
  const globalSearchRef = useRef<HTMLDivElement>(null);
  const { results: globalResults, status: globalSearchStatus, retry: retryGlobalSearch } =
    usePurchaseProductSearch(globalQuery, globalOpen, 10, globalFilter === "all" ? undefined : globalFilter);
  useEffect(() => setGlobalFocusIdx(-1), [globalQuery, globalFilter, globalSearchStatus]);

  // ── Line key counter ───────────────────────────────────────────────
  const [lineKey, setLineKey] = useState(1);

  const showSaveError = useCallback((error: string, details: string[] = []) => {
    setSaveError(error);
    setSaveErrorDetails(details);
  }, []);

  const purchaseInvoiceSchema = useMemo(() => createPurchaseInvoiceSchema(t), [t]);

  // ── React Hook Form ────────────────────────────────────────────────
  const form = useForm<PurchaseInvoiceFormValues>({
    resolver: zodResolver(purchaseInvoiceSchema),
    defaultValues: emptyPurchaseInvoiceForm(),
    mode: "onBlur",
  });

  const {
    register,
    control,
    handleSubmit,
    reset,
    watch,
    setValue,
    getValues,
    getFieldState,
    trigger,
    setError: setFieldError,
    formState: { errors },
  } = form;

  const formWatch = watch();
  const lines = watch("lines");
  const accessKeyValue = watch("accessKey");

  // ── Derived state ──────────────────────────────────────────────────
  const isDraft = !editing || editing.status === "Draft";
  const readOnly = !isDraft;
  const fieldDisabled = saving || readOnly;
  // PILOT-PURCHASE-UI-FIX-01 — cambios locales sin guardar sobre un borrador ya persistido.
  // PURCHASE-DRAFT-RECOVERY D3 — cuotas manuales e intención de retención forman parte de lo que
  // "Guardar borrador" persiste y, por tanto, del dirty-check.
  const draftExtras = useMemo<PurchaseDraftExtras>(
    () => ({ manualSchedule: ptManual ? ptRows : null, retentionIntent }),
    [ptManual, ptRows, retentionIntent],
  );
  const currentDraftSnapshot = useMemo(
    () => (savedDraftSnapshot === null ? null : buildPurchaseDraftSnapshot(formWatch)),
    [savedDraftSnapshot, formWatch],
  );
  const currentExtrasSnapshot = useMemo(
    () => buildPurchaseDraftExtrasSnapshot(draftExtras),
    [draftExtras],
  );
  // Cambios del formulario (cabecera/líneas): Confirmar usa la versión guardada, así que lo
  // bloquean. Cuotas e intención de retención viajan además en el body de Confirmar, así que solo
  // cuentan para salir o recargar el borrador (hasUnsavedChanges).
  const hasUnsavedFormChanges =
    editing?.status === "Draft" &&
    savedDraftSnapshot !== null &&
    currentDraftSnapshot !== savedDraftSnapshot;
  const hasUnsavedChanges =
    hasUnsavedFormChanges ||
    (editing?.status === "Draft" &&
      savedExtrasSnapshot !== null &&
      currentExtrasSnapshot !== savedExtrasSnapshot);
  const unsavedChangesMessage = t(
    "purchases.validation.saveBeforeConfirm",
    "Guarda los cambios antes de confirmar la compra.",
  );

  // PURCHASE-DRAFT-RECOVERY D3 — salir del formulario (Cancelar, pestaña Listado, abrir otra compra
  // o cerrar/recargar la pestaña) nunca descarta en silencio un borrador con cambios sin guardar,
  // ni una compra nueva que todavía no se guardó.
  const hasUnsavedWork =
    isDraft &&
    tab === "nuevo" &&
    (hasUnsavedChanges || (!editing && lines.length > 0));
  const requestLeave = useCallback(
    (action: () => void) => {
      if (hasUnsavedWork) setPendingLeave(() => action);
      else action();
    },
    [hasUnsavedWork],
  );
  const confirmLeave = useCallback(() => {
    const action = pendingLeave;
    setPendingLeave(null);
    action?.();
  }, [pendingLeave]);
  const cancelLeave = useCallback(() => setPendingLeave(null), []);
  useEffect(() => {
    if (!hasUnsavedWork) return;
    const onBeforeUnload = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener("beforeunload", onBeforeUnload);
    return () => window.removeEventListener("beforeunload", onBeforeUnload);
  }, [hasUnsavedWork]);
  // Acciones que operan sobre la versión GUARDADA y recargan el borrador: con cambios locales sin
  // guardar, esos cambios se perderían.
  const blockIfUnsaved = useCallback(() => {
    if (!hasUnsavedChanges) return false;
    message.warning(
      t(
        "purchases.draftRecovery.saveBeforeAction",
        "Guarde los cambios del borrador antes de ejecutar esta acción.",
      ),
    );
    return true;
  }, [hasUnsavedChanges, t]);
  // Cronograma definitivo (solo lectura) de una compra ya confirmada. Las cuotas manuales de un
  // borrador (PURCHASE-DRAFT-RECOVERY F2) siguen siendo editables en ptRows.
  const hasPersistedSchedule =
    !!editing &&
    editing.status !== "Draft" &&
    editing.paymentSchedules &&
    editing.paymentSchedules.length > 0;

  const localSummary = useMemo(
    () => calcSummary(lines, vatRatesMap, iceRatesMap),
    [lines, vatRatesMap, iceRatesMap],
  );
  // PURCHASES-HARDENING-P0 (B4-B6) — los totales vienen de UNA sola fuente: los persistidos
  // mientras no haya cambios locales; en cuanto el borrador se edita, todos salen del formulario.
  // Nunca se mezcla (p. ej. IVA persistido con líneas ya editadas).
  const showPersistedTotals = !!editing && !hasUnsavedFormChanges;
  const costDistributionSummary = useMemo(
    () => summarizeCostDistributions(editing?.costDistributions),
    [editing],
  );
  const costSummary = {
    externalCosts: costDistributionSummary.externalTotal,
    xmlTransportCosts: costDistributionSummary.xmlTransportTotal,
  };
  const summaryTotals =
    showPersistedTotals && editing
      ? {
          subtotal: editing.subtotal,
          discount: editing.totalDiscount,
          ice: editing.totalIce,
          vat: editing.totalVat,
          irbpnr: editing.totalIrbpnr,
          ...costSummary,
          total: editing.grandTotal,
        }
      : {
          subtotal: localSummary.subtotal,
          discount: localSummary.discount,
          ice: localSummary.ice,
          vat: localSummary.vat,
          irbpnr: localSummary.irbpnr,
          ...costSummary,
          // Solo los costos externos suman: el transporte del XML ya está en las líneas.
          total: localSummary.total + costSummary.externalCosts,
        };
  const localTotal = summaryTotals.total;

  // Líneas que vienen de Recepción Electrónica y todavía no tienen Item resuelto — bloquean
  // Confirmar Compra (nunca Guardar Borrador) porque ahí es donde se generan Inventario/Kardex/
  // CxP/Contabilidad. Líneas manuales sin Item quedan fuera: ese caso ya era válido antes.
  const pendingReceptionItems = useMemo(
    () =>
      lines
        .filter((l) => l.purchaseReceptionLineId && !l.itemId)
        .map((l) => l.description || "(sin descripción)"),
    [lines],
  );

  const lineReadinessByKey = useMemo(() => {
    const entries = lines.map((line) => [
      line._key,
      getPurchaseLineReadiness(line, {
        precisionPolicy,
        globalWarehouseId: formWatch.globalWarehouseId,
        vatRates: vatRatesMap,
        iceRates: iceRatesMap,
        t,
      }),
    ] as const);
    return Object.fromEntries(entries);
  }, [formWatch.globalWarehouseId, iceRatesMap, lines, precisionPolicy, t, vatRatesMap]);

  // COMPRAS-METODO-ZH-01B — orientación a excepciones: resueltas / pendientes / con advertencias.
  const pendingProductSources = useMemo(() => selectPendingProductSources(lines), [lines]);
  // PURCHASES-HARDENING-P0 (B9) — "Crear producto" de una línea XML abre el mismo resolver masivo
  // limitado a esa línea (mismo SSOT de barcode unidad/presentación/provisional).
  const [pendingProductsLineId, setPendingProductsLineId] = useState<string | null>(null);
  const modalPendingProductSources = useMemo(
    () =>
      pendingProductsLineId
        ? pendingProductSources.filter((s) => s.rowId === pendingProductsLineId)
        : pendingProductSources,
    [pendingProductSources, pendingProductsLineId],
  );
  const openPendingProducts = useCallback((receptionLineId?: string) => {
    setPendingProductsLineId(receptionLineId ?? null);
    setModalPendingProducts(true);
  }, []);
  const closePendingProducts = useCallback(() => {
    setModalPendingProducts(false);
    setPendingProductsLineId(null);
  }, []);
  const xmlLinesSummary = useMemo(
    () => summarizeXmlLines(lines, lineReadinessByKey),
    [lines, lineReadinessByKey],
  );

  const lineReadinessBlockers = useMemo(
    () =>
      getPurchaseLineBlockingReasons(lines, {
        precisionPolicy,
        globalWarehouseId: formWatch.globalWarehouseId,
        vatRates: vatRatesMap,
        iceRates: iceRatesMap,
        t,
      }),
    [formWatch.globalWarehouseId, iceRatesMap, lines, precisionPolicy, t, vatRatesMap],
  );
  const hasLineReadinessBlockers = lineReadinessBlockers.length > 0;
  // PURCHASE-DRAFT-RECOVERY B1 — Guardar solo se bloquea por lo que el backend rechazaría igual;
  // hasLineReadinessBlockers (todos los bloqueos) queda como regla de Confirmar.
  const hasLineSaveBlockers = useMemo(
    () =>
      getPurchaseLineSaveBlockingReasons(lines, {
        precisionPolicy,
        vatRates: vatRatesMap,
        iceRates: iceRatesMap,
      }).length > 0,
    [iceRatesMap, lines, precisionPolicy, vatRatesMap],
  );
  // El detalle por línea (número + motivo) ya se muestra dentro de cada línea;
  // el aviso superior solo debe indicar que hay líneas por revisar, sin repetirlo.
  const lineReadinessBlockerDetails = useMemo(
    () =>
      lineReadinessBlockers.length > 0
        ? [
            t(
              "purchases.lineReadiness.reviewLinesDetail",
              "Revise las líneas marcadas antes de continuar.",
            ),
          ]
        : [],
    [lineReadinessBlockers, t],
  );
  const confirmBlockedTitle = t(
    "purchases.lineReadiness.confirmBlockedSubmitTitle",
    "No se puede confirmar la compra: hay líneas por resolver.",
  );
  const lineReadinessBlockedTitle = hasLineSaveBlockers
    ? t("purchases.lineReadiness.saveBlockedTitle", "No se puede guardar la compra.")
    : t(
        "purchases.lineReadiness.confirmBlockedTitle",
        "Puede guardar el borrador, pero debe resolver las líneas marcadas antes de confirmar.",
      );

  const ptRowsSum = ptRows.reduce((s, r) => s + r.amount, 0);
  const ptMismatch =
    ptRows.length > 0 &&
    localTotal > 0 &&
    roundToTotalAmount(ptRowsSum) !== roundToTotalAmount(localTotal);
  // PURCHASE-DRAFT-RECOVERY F2 — cuotas que no cuadran (p. ej. cambió el total o la condición): el
  // borrador se guarda igual, tal cual (nunca se ajustan solas); solo Confirmar exige corregirlas.
  const scheduleBlocksConfirm = ptMismatch;
  const scheduleMismatchConfirmMessage = t(
    "purchases.schedule.mismatchBlocksConfirm",
    "Las cuotas no cuadran con el total de la compra. Regenere o corrija las cuotas antes de confirmar.",
  );
  // FASE 5 PURCHASE-UX — estado visible de la compra (mismas condiciones que deshabilitan Confirmar).
  const documentState = resolvePurchaseDocumentState({
    status: editing?.status,
    lineCount: lines.length,
    lineBlockerCount: lineReadinessBlockers.length,
    pendingReceptionItemCount: pendingReceptionItems.length,
    scheduleBlocksConfirm,
    hasUnsavedFormChanges,
  });
  const sriDocTypesUnavailable =
    sriDocTypesLoaded && (sriDocTypesLoadFailed || sriDocTypes.length === 0);
  const canUseSriDocTypes = sriDocTypesLoaded && !sriDocTypesUnavailable;
  const isDuplicateAccessKeyBlocking =
    !!duplicateAccessKey?.exists &&
    duplicateAccessKey.purchaseId !== editing?.id;
  const isSupplierInactiveBlocking = supplierProfile?.isActive === false;

  const duplicateAccessKeyTitle = t(
    "purchases.duplicate.title",
    DUPLICATE_PURCHASE_TITLE,
  );
  const duplicateAccessKeyDetail = t(
    "purchases.duplicate.detail",
    DUPLICATE_PURCHASE_DETAIL,
  );

  const checkAccessKeyDuplicate = useCallback(
    async (accessKey: string, showMessage = false) => {
      const normalized = accessKey.trim();
      if (!normalized) {
        setDuplicateAccessKey(null);
        return null;
      }

      const seq = duplicateAccessKeySeq.current + 1;
      duplicateAccessKeySeq.current = seq;
      setDuplicateAccessKeyChecking(true);
      try {
        const lookup = await purchaseService.getByAccessKey(normalized);
        if (duplicateAccessKeySeq.current !== seq) return null;

        const blocks =
          lookup.exists && lookup.purchaseId !== editing?.id;
        setDuplicateAccessKey(blocks ? lookup : null);
        if (!blocks) setSaveError(previous => previous === duplicateAccessKeyTitle ? "" : previous);
        if (blocks && showMessage) {
          showSaveError(duplicateAccessKeyTitle, [duplicateAccessKeyDetail]);
        }
        return blocks ? lookup : null;
      } catch {
        if (duplicateAccessKeySeq.current === seq) setDuplicateAccessKey(null);
        return null;
      } finally {
        if (duplicateAccessKeySeq.current === seq) {
          setDuplicateAccessKeyChecking(false);
        }
      }
    },
    [duplicateAccessKeyDetail, duplicateAccessKeyTitle, editing?.id, showSaveError],
  );

  useEffect(() => {
    const normalized = (accessKeyValue ?? "").trim();
    if (!normalized || readOnly) {
      setDuplicateAccessKey(null);
      setDuplicateAccessKeyChecking(false);
      return;
    }

    const timer = setTimeout(() => {
      void checkAccessKeyDuplicate(normalized);
    }, 450);
    return () => clearTimeout(timer);
  }, [accessKeyValue, checkAccessKeyDuplicate, readOnly]);

  // Solo bodegas de la sucursal activa (Branch Ownership Rule): una compra
  // solo puede enviar mercadería a bodegas de su propia sucursal.
  useEffect(() => {
    if (!activeBranchId) {
      setWarehouses([]);
      return;
    }
    warehouseLookupFacade
      .list("active", undefined, activeBranchId)
      .then(setWarehouses)
      .catch(() => {});
  }, [activeBranchId]);

  // ── Init reference data ────────────────────────────────────────────
  useEffect(() => {
    sriLookupFacade
      .docTypes()
      .then((types) => {
        setSriDocTypes(types);
        setSriDocTypesLoadFailed(false);
      })
      .catch(() => {
        setSriDocTypes([]);
        setSriDocTypesLoadFailed(true);
      })
      .finally(() => setSriDocTypesLoaded(true));
    sriLookupFacade
      .paymentMethods()
      .then(setSriPaymentMethods)
      .catch(() => {});
    sriLookupFacade
      .taxSupportCodes()
      .then(setSriTaxSupports)
      .catch(() => {});
    paymentTermLookupFacade
      .list()
      .then(setPaymentTermsList)
      .catch(() => {});
    sriLookupFacade
      .vatRates()
      .then((rates) => {
        const map: Record<string, number> = {};
        for (const r of rates) map[r.code] = r.percentage;
        setVatRatesMap(map);
      })
      .catch(() => {});
    sriLookupFacade
      .iceRates()
      .then((rates) => {
        // Solo ICE porcentual es calculable/seleccionable en Compras; específico y mixto exigen
        // el importe declarado en el XML (backend PURCHASE_ICE_DATA_REQUIRED).
        const map: Record<string, number> = {};
        for (const r of rates) if (r.calculationType === "Percentage") map[r.code] = r.percentage;
        setIceRatesMap(map);
      })
      .catch(() => {});
  }, []);

  // ── Global search effects ─────────────────────────────────────────

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (
        globalSearchRef.current &&
        !globalSearchRef.current.contains(e.target as Node)
      )
        setGlobalOpen(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  // ── List ───────────────────────────────────────────────────────────
  // Debounce del texto de búsqueda — evita disparar una petición por tecla.
  useEffect(() => {
    const t = setTimeout(() => {
      setListSearch(listSearchInput);
      setListPage(1);
    }, 300);
    return () => clearTimeout(t);
  }, [listSearchInput]);

  useEffect(() => {
    setListPage(1);
  }, [listStatus]);

  const fetchList = useCallback(async () => {
    setListLoading(true);
    try {
      const r = await purchaseService.list(
        listSearch || undefined,
        listStatus || undefined,
        listPage,
        listPageSize,
      );
      setListItems(r.items);
      setListTotal(r.total);
    } catch {
      message.error(
        t(
          "purchases.messages.listLoadFailed",
          "No se pudo cargar el listado de compras.",
        ),
      );
    }
    setListLoading(false);
  }, [listSearch, listStatus, listPage, t]);

  useEffect(() => {
    fetchList();
  }, [fetchList]);

  // ── Item context loader ────────────────────────────────────────────
  // PURCHASES-HARDENING-P0 (B8) — la bodega es opcional: sin ella el backend igual informa si el
  // ítem participa en inventario. Ese contexto sin bodega solo se guarda para ítems NO
  // inventariables (servicio → no exige bodega); para uno inventariable no hay stock/costo real
  // que mostrar, así que la línea sigue sin contexto y exigiendo bodega, como antes.
  const fetchItemContext = useCallback(
    async (key: number, itemId: string, warehouseId?: string | null) => {
      const currentLines = getValues("lines");
      setValue(
        "lines",
        currentLines.map((l) =>
          l._key === key ? { ...l, _contextLoading: true } : l,
        ),
      );
      try {
        // El proveedor de la factura resuelve el código de proveedor específico del
        // ítem (ItemSupplierCode), única fuente de códigos de compra.
        const supplierId = getValues("supplierId") || undefined;
        const ctx = await purchaseService.getItemContext(
          itemId,
          warehouseId,
          supplierId,
        );
        const keepContext = !!warehouseId || !ctx.participatesInInventory;
        const latest = getValues("lines");
        setValue(
          "lines",
          latest.map((l) =>
            l._key === key
              ? keepContext
                ? { ...l, context: ctx, _contextLoading: false }
                : { ...l, _contextLoading: false }
              : l,
          ),
        );
      } catch {
        const latest = getValues("lines");
        setValue(
          "lines",
          latest.map((l) =>
            l._key === key ? { ...l, _contextLoading: false } : l,
          ),
        );
      }
    },
    [getValues, setValue],
  );

  // ── Line operations ────────────────────────────────────────────────
  const addLine = useCallback(() => {
    const currentLines = getValues("lines");
    setValue(
      "lines",
      [
        ...currentLines,
        {
          _key: lineKey,
          description: "",
          quantity: 1,
          unitPrice: 0,
          vatCode: "",
          discountPct: 0,
          conversionFactor: 1,
        },
      ],
      { shouldValidate: true },
    );
    setLineKey((k) => k + 1);
  }, [lineKey, getValues, setValue]);

  const removeLine = useCallback(
    (key: number) => {
      const currentLines = getValues("lines");
      if (currentLines.find(l => l._key === key)?.xmlAllocation) return;
      setValue(
        "lines",
        currentLines.filter((l) => l._key !== key),
        { shouldValidate: true },
      );
    },
    [getValues, setValue],
  );

  const duplicateLine = useCallback(
    (key: number) => {
      const currentLines = getValues("lines");
      const src = currentLines.find((l) => l._key === key);
      if (src && !src.xmlAllocation) {
        // La copia es una línea nueva: sin Id guardado ni costos distribuidos de la original.
        const { _readinessIssue: _discard, lineId: _id, freightAllocated: _f, otherCostsAllocated: _o, ...copy } = src;
        void [_discard, _id, _f, _o];
        setValue("lines", [
          ...currentLines,
          { ...copy, _key: lineKey, context: undefined },
        ]);
        setLineKey((k) => k + 1);
      }
    },
    [lineKey, getValues, setValue],
  );

  const updateLine = useCallback(
    (key: number, field: string, value: unknown) => {
      const currentLines = getValues("lines");
      const index = currentLines.findIndex(line => line._key === key);
      const fieldPath = `lines.${index}.${field}` as FieldPath<PurchaseInvoiceFormValues>;
      const revalidate = index >= 0 && getFieldState(fieldPath).invalid;
      setValue(
        "lines",
        currentLines.map((l) =>
          l._key === key && (!l.xmlAllocation || ["context", "_contextLoading", "warehouseId"].includes(field)) ? { ...l, [field]: value } : l,
        ),
      );
      if (revalidate) void trigger(fieldPath);
    },
    [getValues, setValue, getFieldState, trigger],
  );

  // ── Bodega de línea (docs/architecture/purchases.md § Bodega por línea) ──
  // Una compra puede tener líneas en múltiples bodegas — WarehouseId pertenece a la
  // línea, nunca al documento. updateLineWarehouse es el ÚNICO flujo de actualización
  // (Single Source of Truth): asigna warehouseId y, si la línea ya tiene Item, refresca
  // su contexto (stock/costos/indicadores). Cualquier lógica futura sobre "cambiar
  // bodega de una línea" (reservas, disponibilidad, lotes, costos por bodega, etc.) se
  // agrega acá una sola vez — nunca en applyGlobalWarehouse ni en ningún otro punto.
  const updateLineWarehouse = useCallback(
    (key: number, warehouseId: string | null) => {
      const line = getValues("lines").find((l) => l._key === key);
      updateLine(key, "warehouseId", warehouseId);
      if (line?.itemId && warehouseId)
        void fetchItemContext(key, line.itemId, warehouseId);
    },
    [getValues, updateLine, fetchItemContext],
  );

  // Aplicación masiva, NO sincronización permanente (purchases.md § Bodega por línea): corre una única vez, al
  // clic. El selector general no implementa lógica propia — solo guarda el default del
  // documento y delega en updateLineWarehouse línea por línea, exactamente igual que si
  // el usuario hubiera cambiado cada línea manualmente. Un cambio posterior en una línea
  // individual nunca es sobrescrito automáticamente (no hay watcher sobre este campo).
  const applyGlobalWarehouse = useCallback(
    (warehouseId: string) => {
      setValue("globalWarehouseId", warehouseId);
      const currentLines = getValues("lines");
      for (const line of currentLines) {
        updateLineWarehouse(line._key, warehouseId || null);
      }
    },
    [setValue, getValues, updateLineWarehouse],
  );

  // ── Item Matching (líneas que vienen de Recepción Electrónica) ────────
  // Reutiliza el mismo backend que la pantalla de Recepción (MatchItemCommand /
  // UnmatchPurchaseReceptionItemCommand) vía purchaseReceptionLineId — nunca duplica el motor de
  // Item Matching. El resultado solo actualiza el formulario local; se persiste recién al Guardar.
  const [matchingKey, setMatchingKey] = useState<number | null>(null);

  const handleMatchItem = useCallback(
    async (
      key: number,
      itemId: string,
      itemLabel: string,
      packagingLevelId?: string | null,
    ) => {
      const currentLines = getValues("lines");
      const line = currentLines.find((l) => l._key === key);
      if (!line?.purchaseReceptionLineId) return;

      // The purchase owns its operational product; changing it does not rewrite supplier matching.
      if (line.itemId) {
        if (line.itemId === itemId) return;
        setValue("lines", currentLines.map(l => l._key === key
          ? { ...l, itemId, description: itemLabel, packagingLevelId: packagingLevelId ?? null,
              xmlOriginPackagingLevelId: null, xmlOriginFactor: undefined, conversionFactor: undefined, context: undefined, _readinessIssue: undefined }
          : l));
        void fetchItemContext(key, itemId, line.warehouseId || getValues("globalWarehouseId"));
        return;
      }

      setMatchingKey(key);
      try {
        const updated = await purchaseReceptionService.matchItem(
          line.purchaseReceptionLineId,
          itemId,
          packagingLevelId,
        );
        const latest = getValues("lines");
        setValue(
          "lines",
          latest.map((l) =>
            l._key === key
              ? {
                  ...l,
                  itemId: updated.itemId ?? undefined,
                  itemMatchStatus: updated.matchStatus,
                  description: itemLabel,
                  _readinessIssue: undefined,
                }
              : l,
          ),
        );
        const wh = line.warehouseId || getValues("globalWarehouseId");
        if (updated.itemId) void fetchItemContext(key, updated.itemId, wh);
      } catch (err) {
        const errorMessage = readApiErrorMessage(err);
        if (errorMessage === SUPPLIER_CODE_CONFLICT_DETAIL) {
          const latest = getValues("lines");
          setValue(
            "lines",
            latest.map((l) =>
              l._key === key
                ? { ...l, _readinessIssue: "SUPPLIER_CODE_CONFLICT" }
                : l,
            ),
          );
        }
        message.error(errorMessage ?? "No se pudo vincular el ítem.");
      }
      setMatchingKey(null);
    },
    [getValues, setValue, fetchItemContext],
  );

  const handleSaveSupplierPresentation = useCallback(
    async (key: number) => {
      const currentLines = getValues("lines");
      const line = currentLines.find((l) => l._key === key);
      // FASE 5 PURCHASE-UX — lo que se aprende para Proveedor + Código es la presentación con la que
      // se interpreta el documento del proveedor (ORIGEN), no la operativa (una XML editada puede
      // estar en unidad base). Acción explícita e independiente: no toca cantidad/costo/presentación
      // operativa de la línea, y guardar la compra nunca la dispara.
      const supplierPresentationId = line?.xmlOriginPackagingLevelId ?? line?.packagingLevelId;
      if (!line?.purchaseReceptionLineId || !line.itemId || !supplierPresentationId)
        return;

      setMatchingKey(key);
      try {
        const updated = await purchaseReceptionService.matchItem(
          line.purchaseReceptionLineId,
          line.itemId,
          supplierPresentationId,
        );
        const latest = getValues("lines");
        setValue(
          "lines",
          latest.map((l) =>
            l._key === key
              ? {
                  ...l,
                  itemMatchStatus: updated.matchStatus,
                  _readinessIssue: undefined,
                }
              : l,
          ),
        );
        message.success("Presentación guardada para este proveedor.");
      } catch (err) {
        const errorMessage = readApiErrorMessage(err);
        if (errorMessage === SUPPLIER_CODE_CONFLICT_DETAIL) {
          const latest = getValues("lines");
          setValue(
            "lines",
            latest.map((l) =>
              l._key === key
                ? { ...l, _readinessIssue: "SUPPLIER_CODE_CONFLICT" }
                : l,
            ),
          );
        }
        message.error(
          errorMessage ??
            "No se pudo guardar la presentación para este proveedor.",
        );
      }
      setMatchingKey(null);
    },
    [getValues, setValue],
  );

  const handleUnmatchItem = useCallback(
    async (key: number) => {
      const currentLines = getValues("lines");
      const line = currentLines.find((l) => l._key === key);
      if (!line?.purchaseReceptionLineId) return;

      const confirmed = await message.confirm({
        title: "Desvincular Item",
        message:
          "La línea volverá a estado Pendiente y podrá buscar o crear un ítem nuevamente. Esta acción no afecta compras ya confirmadas.",
        confirmLabel: "Desvincular",
        variant: "warning",
      });
      if (!confirmed) return;

      setMatchingKey(key);
      try {
        const updated = await purchaseReceptionService.unmatchItem(
          line.purchaseReceptionLineId,
        );
        const latest = getValues("lines");
        setValue(
          "lines",
          latest.map((l) =>
            l._key === key
              ? {
                  ...l,
                  itemId: undefined,
                  itemMatchStatus: updated.matchStatus,
                  description: "",
                  context: undefined,
                  _readinessIssue: undefined,
                }
              : l,
          ),
        );
        message.success(
          "Item desvinculado. La línea está pendiente de matching.",
        );
      } catch (err) {
        message.error(
          readApiErrorMessage(err) ?? "No se pudo desvincular el ítem.",
        );
      }
      setMatchingKey(null);
    },
    [getValues, setValue],
  );

  /**
   * COMPRAS-METODO-ZH-01B — aplica al formulario el lote ya persistido por el backend (producto,
   * presentación y conversión aprendidos por línea). Igual que la vinculación individual, solo
   * actualiza el formulario local; la compra se guarda con "Guardar".
   */
  /** Aplica líneas XML resueltas/corregidas y recarga el contexto de las que cambiaron de producto. */
  const mergeResolvedReceptionLines = useCallback(
    (resolvedLines: ResolvedReceptionLine[]) => {
      const updated = applyResolvedReceptionLines(getValues("lines"), resolvedLines);
      setValue("lines", updated, { shouldValidate: true });
      const globalWarehouseId = getValues("globalWarehouseId");
      for (const l of updated) {
        const warehouseId = l.warehouseId || globalWarehouseId;
        if (l.itemId && l.purchaseReceptionLineId && !l.context)
          void fetchItemContext(l._key, l.itemId, warehouseId);
      }
    },
    [getValues, setValue, fetchItemContext],
  );

  const applyResolvedLines = useCallback(
    async (result: ResolveReceptionLinesResult) => {
      mergeResolvedReceptionLines(result.lines);
      setModalPendingProducts(false);
      setPendingProductsLineId(null);
      message.success(
        t("purchases.pendingProducts.done", {
          created: result.itemsCreated,
          linked: result.linesLinked,
          learned: result.equivalencesLearned,
        }),
      );
    },
    [mergeResolvedReceptionLines, t],
  );

  /**
   * "Corregir relación" (FASE 3): producto/presentación correctos para una línea XML ya vinculada.
   * El backend corrige la línea, reemplaza la equivalencia futura y devuelve todas las líneas del
   * documento afectadas (mismo código). Solo actualiza el formulario; la compra se guarda con
   * "Guardar". Devuelve el mensaje de error, o `null` si se aplicó.
   */
  const handleCorrectMatch = useCallback(
    async (key: number, itemId: string, packagingLevelId: string | null) => {
      const line = getValues("lines").find((l) => l._key === key);
      if (!line?.purchaseReceptionLineId) return null;
      setMatchingKey(key);
      try {
        const result = await purchaseReceptionService.correctMatch(
          line.purchaseReceptionLineId,
          itemId,
          packagingLevelId,
        );
        mergeResolvedReceptionLines(result.lines);
        message.success(t("purchases.correctMatch.done", { count: result.lines.length }));
        return null;
      } catch (err) {
        return (
          readApiErrorMessage(err) ??
          t("purchases.correctMatch.errors.generic", "No se pudo corregir la relación.")
        );
      } finally {
        setMatchingKey(null);
      }
    },
    [getValues, mergeResolvedReceptionLines, t],
  );

  const addLineWithItem = useCallback(
    async (item: ItemDto) => {
      setGlobalOpen(false);
      setGlobalQuery("");
      const key = lineKey;
      const detail = await itemLookupFacade.getById(item.id);
      const newLine = buildPurchaseLineFromItem(detail, { key });
      const currentLines = getValues("lines");
      setValue("lines", [...currentLines, newLine], { shouldValidate: true });
      setLineKey((k) => k + 1);
      void fetchItemContext(key, item.id, getValues("globalWarehouseId"));
    },
    [lineKey, getValues, setValue, fetchItemContext],
  );

  const handleGlobalKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === "Escape") { setGlobalOpen(false); return; }
      if (!globalOpen || globalResults.length === 0) return;
      if (e.key === "ArrowDown") {
        e.preventDefault();
        setGlobalFocusIdx((i) => Math.min(i + 1, globalResults.length - 1));
      }
      if (e.key === "ArrowUp") {
        e.preventDefault();
        setGlobalFocusIdx((i) => Math.max(i - 1, 0));
      }
      if (e.key === "Enter" && globalFocusIdx >= 0 && globalResults[globalFocusIdx]) {
        e.preventDefault();
        void addLineWithItem(globalResults[globalFocusIdx]);
      }
    },
    [globalOpen, globalResults, globalFocusIdx, addLineWithItem],
  );

  // ── Supplier handler ───────────────────────────────────────────────
  const applySupplierDefaults = useCallback(
    async (profile: SupplierProfile) => {
      if (profile.config) {
        setValue(
          "sriPaymentMethodCode",
          profile.config.defaultPaymentMethodCode ?? "",
        );
        setValue("taxSupportCode", profile.config.defaultTaxSupportCode ?? "");
      }
      if (!profile.purchaseDefaultPaymentTermId) return;
      try {
        const pt = await paymentTermLookupFacade.getById(
          profile.purchaseDefaultPaymentTermId,
        );
        setValue("paymentTermId", pt.id);
        setPtInstallments(pt.installments);
        setPtDaysBetween(pt.daysBetweenInstallments);
        setPtLoaded(true);
        setPtManual(false);
        setPtRows(
          generateScheduleRows(
            pt.installments,
            pt.daysBetweenInstallments,
            localTotal,
            getValues("issueDate"),
          ),
        );
      } catch {
        /* PaymentTerm load failed */
      }
    },
    [setValue, getValues, localTotal],
  );

  const supplierInactiveDetail = t(
    "purchases.supplier.inactiveDetail",
    "Debe activarlo antes de crear la compra.",
  );

  const formatSupplierInactiveMessage = useCallback(
    (supplierName: string) =>
      t("purchases.supplier.inactiveMessage", { name: supplierName }),
    [t],
  );

  const resolveSupplierProfile = useCallback(async (
    supplierId: string,
    options?: { forceRefresh?: boolean },
  ) => {
    const cached = profileCache.current.get(supplierId);
    if (cached && !options?.forceRefresh) return cached;

    const [bp, purchaseSettings] = await Promise.all([
      businessPartnerLookupFacade.getBusinessPartner(supplierId),
      businessPartnerPurchaseSettingsFacade
        .getPurchaseSettings(supplierId)
        .catch(() => null),
    ]);
    const profile = buildSupplierProfile(
      bp,
      purchaseSettings?.paymentTermId ?? null,
    );
    profileCache.current.set(supplierId, profile);
    return profile;
  }, []);

  const handleSupplierChange = useCallback(
    async (s: SupplierPickerRow | null) => {
      setValue("supplierId", s?.id ?? "", { shouldValidate: true });
      if (!s) {
        setSupplierProfile(null);
        return;
      }
      try {
        const profile = await resolveSupplierProfile(s.id);
        setSupplierProfile(profile);
        applySupplierDefaults(profile);
      } catch {
        setSupplierProfile({
          ruc: s.identificationNumber,
          name: s.fullName,
          isActive: s.isActive,
          config: s.supplierConfig,
          isRequiredToKeepAccounting: false,
          purchaseDefaultPaymentTermId: null,
        });
      }
    },
    [setValue, applySupplierDefaults, resolveSupplierProfile],
  );

  const validateSupplierIsActive = useCallback(
    async (supplierId: string, showMessage = false) => {
      if (!supplierId.trim()) return null;
      const profile = await resolveSupplierProfile(supplierId, {
        forceRefresh: true,
      });
      setSupplierProfile(profile);
      if (profile.isActive) return null;

      const text =
        formatSupplierInactiveMessage(profile.name) ||
        buildSupplierInactiveMessage(profile.name);
      if (showMessage) {
        showSaveError(text, [supplierInactiveDetail]);
      }
      return profile;
    },
    [
      formatSupplierInactiveMessage,
      resolveSupplierProfile,
      showSaveError,
      supplierInactiveDetail,
    ],
  );

  // ── Form reset ─────────────────────────────────────────────────────
  const resetForm = useCallback(() => {
    reset(emptyPurchaseInvoiceForm());
    setEditing(null);
    setSavedDraftSnapshot(null);
    setSavedExtrasSnapshot(null);
    showSaveError("");
    setDuplicateAccessKey(null);
    setDuplicateAccessKeyChecking(false);
    setLineKey(1);
    setSupplierProfile(null);
    setWhPreview(null);
    setWhPreviewError(null);
    setRetentionIntent(emptyPurchaseRetentionIntent(todayIso()));
    setRetention(null);
    setPtInstallments(1);
    setPtDaysBetween(0);
    setPtRows([]);
    setPtLoaded(false);
    setPtManual(false);
    setGlobalQuery("");
    setGlobalOpen(false);
    setGlobalFilter("all");
  }, [reset, showSaveError]);

  // ── Load for edit ──────────────────────────────────────────────────
  const loadForEdit = useCallback(
    async (id: string) => {
      try {
        setDuplicateAccessKey(null);
        setDuplicateAccessKeyChecking(false);
        const inv = await purchaseService.getById(id);
        setEditing(inv);

        // Supplier profile
        if (inv.supplierId) {
          try {
            const profile = await resolveSupplierProfile(inv.supplierId);
            setSupplierProfile(profile);
          } catch {
            /* profile load failed */
          }
        }

        // Map lines
        const mappedLines: PurchaseLineFormValues[] = inv.lines.map((l, i) => ({
          _key: i + 1,
          lineId: l.id,
          itemId: l.itemId,
          description: l.description,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          vatCode: l.vatCode,
          discountPct: l.discountPct,
          iceCode: l.iceCode,
          warehouseId: l.warehouseId,
          notes: l.notes,
          purchaseReceptionLineId: l.purchaseReceptionLineId ?? undefined,
          ...normalizePurchaseLinePresentation(l),
          ...restoreXmlAllocation(l),
          // Derivados de las distribuciones guardadas: solo lectura (costo con flete de la línea).
          freightAllocated: l.freightAllocated,
          otherCostsAllocated: l.otherCostsAllocated,
          // PURCHASE-DRAFT-RECOVERY F1/B2 — evidencia original de la recepción (misma proyección
          // que "Crear compra"): aunque la línea se haya editado y guardado, volver exactamente a
          // los valores del XML vuelve a usar sus montos documentales, igual que el backend.
          // Sin XmlSource (línea manual o compra antigua): montos documentales persistidos (B4-B6).
          ...(l.xmlSource
            ? {
                ...xmlEvidenceFromDraftLine(l.xmlSource),
                itemMatchStatus: l.xmlSource.itemMatchStatus,
                // FASE 5 PURCHASE-UX — presentación de origen del documento del proveedor (una XML
                // editada pudo persistirse en unidad base).
                ...resolveXmlOriginPresentation(l, l.xmlSource),
              }
            : documentalTaxSnapshot(l)),
        }));

        // SRI codes
        const savedPm = inv.sriPaymentMethodCode ?? "";
        const savedTs = inv.taxSupportCode ?? "";
        const cachedProfile = profileCache.current.get(inv.supplierId);

        const loadedValues: PurchaseInvoiceFormValues = {
          supplierId: inv.supplierId,
          docTypeCode: inv.docTypeCode,
          invoiceNumber: inv.invoiceNumber,
          issueDate: inv.issueDate,
          accessKey: inv.accessKey ?? "",
          authorizationNumber: inv.authorizationNumber ?? "",
          authorizationDate: toDateTimeLocalInputValue(inv.authorizationDate),
          globalWarehouseId: inv.globalWarehouseId ?? "",
          dueDate: inv.dueDate ?? "",
          notes: inv.notes ?? "",
          sriPaymentMethodCode:
            savedPm || cachedProfile?.config?.defaultPaymentMethodCode || "",
          taxSupportCode:
            savedTs || cachedProfile?.config?.defaultTaxSupportCode || "",
          paymentTermId: inv.paymentTermId ?? "",
          lines: mappedLines,
        };

        // PURCHASE-DRAFT-RECOVERY F2 — cuotas manuales guardadas en el borrador.
        const isDraftInvoice = inv.status === "Draft";
        const savedManualSchedule: ScheduleRow[] | null =
          isDraftInvoice && inv.paymentSchedules.length > 0
            ? inv.paymentSchedules.map((sc) => ({
                number: sc.installmentNumber,
                dueDate: sc.dueDate,
                amount: sc.amount,
                notes: sc.notes ?? "",
              }))
            : null;
        // PURCHASE-DRAFT-RECOVERY F3 — intención de retención recordada por el borrador.
        const savedRetentionIntent: PurchaseRetentionIntentState = inv.retentionIntent
          ? {
              appliesRetention: inv.retentionIntent.appliesRetention,
              emissionPointId: inv.retentionIntent.emissionPointId ?? "",
              issueDate: inv.retentionIntent.issueDate ?? todayIso(),
            }
          : emptyPurchaseRetentionIntent(todayIso());

        // PURCHASE-DRAFT-RECOVERY D1 — match aprendido en la recepción después de guardar: se
        // propone en el formulario (nunca pisa un producto ya guardado en la compra). La huella se
        // toma de lo PERSISTIDO, así queda como cambio sin guardar hasta que el usuario guarde.
        const recoveredLines = isDraftInvoice
          ? mappedLines.map((ml, i) => {
              const recovered = recoverLearnedMatch(ml, inv.lines[i].xmlSource);
              return recovered ? { ...ml, ...recovered } : ml;
            })
          : mappedLines;
        const recoveredCount = recoveredLines.filter(
          (ml, i) => ml.itemId && !mappedLines[i].itemId,
        ).length;

        reset({ ...loadedValues, lines: recoveredLines });
        setSavedDraftSnapshot(buildPurchaseDraftSnapshot(loadedValues));
        setSavedExtrasSnapshot(
          buildPurchaseDraftExtrasSnapshot({
            manualSchedule: savedManualSchedule,
            retentionIntent: savedRetentionIntent,
          }),
        );
        if (recoveredCount > 0)
          message.info(
            t("purchases.draftRecovery.learnedMatches", {
              count: recoveredCount,
            }),
          );

        setLineKey(inv.lines.length + 1);

        // Fetch context for each line
        for (const ml of recoveredLines) {
          const wh = ml.warehouseId || inv.globalWarehouseId;
          if (ml.itemId) void fetchItemContext(ml._key, ml.itemId, wh);
        }

        // Reconstruye el estado de Item Matching (badge Auto/Manual/Pendiente/Revisar) de las líneas
        // que vienen de Recepción Electrónica — no se persiste en PurchaseInvoiceDetail, se lee en
        // vivo desde PurchaseReceptionLine vía el mismo backend que usa la pantalla de Recepción.
        // Con XmlSource (borrador) el estado ya llegó en la misma lectura.
        for (const ml of mappedLines) {
          if (!ml.purchaseReceptionLineId || ml.itemMatchStatus) continue;
          const lineId = ml.purchaseReceptionLineId;
          purchaseReceptionService
            .getLineMatch(lineId)
            .then((match) => {
              const current = getValues("lines");
              setValue(
                "lines",
                current.map((l) =>
                  l._key === ml._key
                    ? { ...l, itemMatchStatus: match.matchStatus }
                    : l,
                ),
              );
            })
            .catch(() => {
              /* la línea sigue mostrando su estado sin badge de matching */
            });
        }

        // Payment schedule
        setPtInstallments(savedManualSchedule?.length ?? inv.paymentTermInstallments);
        setPtDaysBetween(inv.paymentTermDaysBetween);
        setPtLoaded(true);
        setPtManual(savedManualSchedule !== null);
        if (savedManualSchedule) {
          setPtRows(savedManualSchedule);
        } else if (!inv.paymentSchedules || inv.paymentSchedules.length === 0) {
          setPtRows(
            generateScheduleRows(
              inv.paymentTermInstallments,
              inv.paymentTermDaysBetween,
              inv.grandTotal,
              inv.issueDate,
            ),
          );
        } else {
          setPtRows([]);
        }
        setRetentionIntent(savedRetentionIntent);

        setShowElectronic(!!(inv.accessKey || inv.authorizationNumber));
        setShowNotes(!!inv.notes);
        setWhPreview(null);
        setRetention(null);
        if (inv.status === "Confirmed") {
          try {
            const ret = await purchaseRetentionFacade.getForPurchase(inv.id);
            setRetention(ret);
          } catch {
            /* */
          }
        }

        setTab("nuevo");
      } catch {
        const error = t("purchases.errors.loadFailed", "Error al cargar la compra.");
        showSaveError(error);
      }
    },
    [
      reset,
      fetchItemContext,
      getValues,
      setValue,
      showSaveError,
      t,
      resolveSupplierProfile,
    ],
  );

  // ── Load from Recepción Electrónica (PurchaseReceptionDocument → draft) ────
  const loadFromReception = useCallback(
    async (receptionDocumentId: string, accessKey?: string | null) => {
      setReceptionProcessingNotice(null);
      showSaveError("");
      setDuplicateAccessKey(null);
      if (accessKey?.trim()) {
        const duplicate = await checkAccessKeyDuplicate(accessKey, true);
        if (duplicate) {
          setTab("nuevo");
          return;
        }
      }
      try {
        const draft: PurchaseDraftDto =
          await purchaseReceptionService.createDraft(receptionDocumentId);
        if (!accessKey?.trim() && draft.accessKey?.trim()) {
          const duplicate = await checkAccessKeyDuplicate(draft.accessKey, true);
          if (duplicate) {
            setTab("nuevo");
            return;
          }
        }
        setEditing(null);
        if (
          draft.processingStatus === "PROCESSED_WITH_WARNINGS" &&
          draft.processingNotes
        ) {
          setReceptionProcessingNotice(
            t("purchases.receptionDraft.processingWarnings", {
              notes: draft.processingNotes,
            }),
          );
        }

        if (draft.supplierId) {
          try {
            const profile = await resolveSupplierProfile(draft.supplierId);
            setSupplierProfile(profile);
          } catch {
            /* profile load failed */
          }
        } else {
          setSupplierProfile(null);
        }

        const mappedLines: PurchaseLineFormValues[] = draft.lines.map(
          (l, i) => ({
            _key: i + 1,
            itemId: l.itemId ?? undefined,
            description: l.description,
            quantity: l.quantity,
            unitPrice: l.unitPrice,
            vatCode: l.vatCode,
            discountPct: l.discountPct ?? 0,
            iceCode: l.iceCode ?? undefined,
            warehouseId: l.warehouseId ?? undefined,
            notes: l.notes ?? undefined,
            purchaseReceptionLineId: l.purchaseReceptionLineId,
            ...normalizePurchaseLinePresentation(l),

            itemMatchStatus: l.itemMatchStatus,
            // PURCHASE-LINE-PACKAGING-XML-SNAPSHOT-IMMUTABLE-01 — copia congelada del comprobante,
            // ANTES de que el usuario edite: `quantity`/`unitPrice` de arriba siguen siendo los
            // campos editables; los xml* nunca se tocan de nuevo. PURCHASE-DRAFT-RECOVERY F1: misma
            // proyección que al reabrir el borrador guardado.
            ...xmlEvidenceFromDraftLine(l),
            // FASE 5 PURCHASE-UX — la presentación propuesta por la recepción interpreta el documento.
            ...resolveXmlOriginPresentation(l),
          }),
        );

        reset({
          ...emptyPurchaseInvoiceForm(),
          supplierId: draft.supplierId ?? "",
          docTypeCode: draft.docTypeCode ?? "",
          invoiceNumber: draft.invoiceNumber,
          issueDate: draft.issueDate,
          accessKey: draft.accessKey ?? "",
          authorizationNumber: draft.authorizationNumber ?? "",
          authorizationDate: toDateTimeLocalInputValue(draft.authorizationDate),
          sriPaymentMethodCode: draft.sriPaymentMethodCode ?? "",
          lines: mappedLines,
        });

        setLineKey(mappedLines.length + 1);
        setShowElectronic(!!(draft.accessKey || draft.authorizationNumber));
        setPtInstallments(1);
        setPtDaysBetween(0);
        setPtRows([]);
        setPtLoaded(false);
        setPtManual(false);
        setWhPreview(null);
        setRetentionIntent(emptyPurchaseRetentionIntent(todayIso()));
        setRetention(null);
        setTab("nuevo");
      } catch (err) {
        const backendMessage = readApiErrorMessage(err);
        const supplierInactiveBackendMessage = backendMessage ?? "";
        const isSupplierInactiveError =
          supplierInactiveBackendMessage.includes("se encuentra inactivo");
        if (backendMessage === DUPLICATE_PURCHASE_DETAIL) {
          showSaveError(duplicateAccessKeyTitle, [duplicateAccessKeyDetail]);
        } else if (isSupplierInactiveError) {
          showSaveError(supplierInactiveBackendMessage, [supplierInactiveDetail]);
        } else {
          showSaveError(
            backendMessage ??
              t(
                "purchases.errors.receptionDraftFailed",
                "No se pudo generar el borrador de compra desde el documento de recepción.",
              ),
          );
        }

      }
    },
    [
      reset,
      showSaveError,
      t,
      resolveSupplierProfile,
      checkAccessKeyDuplicate,
      duplicateAccessKeyTitle,
      duplicateAccessKeyDetail,
      supplierInactiveDetail,
    ],
  );

  // ── Save (create/update) ───────────────────────────────────────────
  const handleSave = handleSubmit(async (data) => {
    showSaveError("");
    const duplicate = await checkAccessKeyDuplicate(data.accessKey ?? "", true);
    if (duplicate) return;
    const inactiveSupplier = await validateSupplierIsActive(
      data.supplierId,
      true,
    );
    if (inactiveSupplier) return;
    if (hasLineSaveBlockers) {
      showSaveError(lineReadinessBlockedTitle, lineReadinessBlockerDetails);
      return;
    }
    if (!canUseSriDocTypes) {
      const title = t(
        "purchases.validation.sriDocTypesUnavailableTitle",
        "No se puede guardar la compra.",
      );
      const detail = t(
        "purchases.validation.sriDocTypesUnavailableDetail",
        "El catálogo SRI de tipos de documento no está disponible. Recargue la página o inténtelo nuevamente.",
      );
      showSaveError(title, [detail]);
      return;
    }
    if (!sriDocTypes.some((d) => d.code === data.docTypeCode)) {
      const title = t(
        "purchases.validation.sriDocTypeInvalidTitle",
        "No se puede guardar la compra.",
      );
      const detail = t(
        "purchases.validation.sriDocTypeInvalidDetail",
        "Seleccione un tipo de documento SRI activo del catálogo.",
      );
      showSaveError(title, [detail]);
      return;
    }
    setSaving(true);
    try {
      const payload = buildPurchaseDraftPayload(data, draftExtras, {
        purchaseUnitPriceDecimals: resolvePrecisionDecimals(precisionPolicy, "purchaseUnitPrice"),
      });

      let savedId: string;
      if (editing) {
        await purchaseService.update(editing.id, {
          ...payload,
          id: editing.id,
        });
        savedId = editing.id;
        message.success(
          t(
            "purchases.messages.updated",
            "Compra actualizada correctamente.",
          ),
        );
      } else {
        const created = await purchaseService.create(payload);
        savedId = created.id;
        message.success(
          t("purchases.messages.draftSaved", "Borrador guardado correctamente."),
        );
      }
      // FASE 5 PURCHASE-UX — después de Guardar se permanece en la compra, recargada por id desde
      // el backend (versión persistida = la que Confirmar usará), para poder confirmar sin volver
      // a buscarla en el listado. El listado se refresca en segundo plano.
      await loadForEdit(savedId);
      fetchList();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setFieldError);
      if (applied) return;
      const apiMessages = readApiErrorMessages(err);
      if (apiMessages.length > 0) {
        const isDuplicateAccessKeyError = apiMessages.some(
          (msg) =>
            msg === duplicateAccessKeyDetail ||
            msg === DUPLICATE_PURCHASE_DETAIL,
        );
        const supplierInactiveMessage = apiMessages.find((msg) =>
          msg.includes("se encuentra inactivo"),
        );
        if (!isDuplicateAccessKeyError && supplierInactiveMessage) {
          showSaveError(supplierInactiveMessage, [supplierInactiveDetail]);
          return;
        }
        const title = isDuplicateAccessKeyError
          ? duplicateAccessKeyTitle
          : t(
              "purchases.validation.saveBlocked",
              "No se puede guardar la compra.",
            );
        showSaveError(title, apiMessages);
      } else if (!applied) {
        const e = err as ApiErrorLike;
        showSaveError(
            e?.response?.data?.message?.user ??
            e?.response?.data?.data?.errors?.[0] ??
            e?.message ??
            t("purchases.errors.saveFailed", "Error al guardar."),
        );
      }
    } finally {
      setSaving(false);
    }
  });

  // ── Confirm purchase ───────────────────────────────────────────────
  const handleConfirm = useCallback(async () => {
    setModalConfirm(false);
    if (!editing) return;
    // PILOT-PURCHASE-UI-FIX-01 — el backend confirma la versión GUARDADA: con cambios locales sin
    // guardar se perderían en silencio. Nunca guarda+confirma automáticamente. Cuotas e intención
    // de retención no cuentan aquí: viajan en el body de esta misma confirmación.
    if (hasUnsavedFormChanges) {
      showSaveError(unsavedChangesMessage);
      return;
    }
    // PURCHASE-DRAFT-RECOVERY B1 — Guardar ya no exige líneas listas: Confirmar aplica su propia
    // regla (producto, conflicto, presentación, costo sospechoso, bodega, impuesto).
    if (hasLineReadinessBlockers) {
      showSaveError(confirmBlockedTitle, lineReadinessBlockerDetails);
      return;
    }
    if (scheduleBlocksConfirm) {
      showSaveError(scheduleMismatchConfirmMessage);
      return;
    }
    // ZH-PURCHASE-RETENTION-CONFIRM-01 — si el usuario pidió la retención, viaja como intención de
    // esta misma confirmación (todo o nada en el backend). Incompleta → no se confirma a medias.
    if (!isPurchaseRetentionIntentComplete(whPreview, retentionIntent)) {
      showSaveError(
        t(
          "purchases.retention.incompleteIntent",
          "Complete la retención (punto de emisión y fecha) o desactívela antes de confirmar.",
        ),
      );
      return;
    }
    const retentionRequest = buildPurchaseRetentionIntent(whPreview, retentionIntent);
    setSaving(true);
    try {
      const schedule =
        ptRows.length > 0
          ? ptRows.map((r) => ({
              installmentNumber: r.number,
              dueDate: r.dueDate,
              amount: r.amount,
              notes: r.notes || null,
            }))
          : undefined;
      await purchaseService.confirm(editing.id, schedule, retentionRequest);
      message.success(
        retentionRequest
          ? t(
              "purchases.messages.confirmedWithRetention",
              "Compra confirmada y retención emitida correctamente.",
            )
          : t("purchases.messages.confirmed", "Compra confirmada correctamente."),
      );
      resetForm();
      setTab("listado");
      fetchList();
    } catch (err: unknown) {
      const [detail, ...rest] = resolveApiErrorDetails(
        err,
        t("purchases.errors.confirmFailed", "Error al confirmar."),
      );
      showSaveError(detail, rest);
    } finally {
      setSaving(false);
    }
  }, [
    editing,
    hasUnsavedFormChanges,
    unsavedChangesMessage,
    hasLineReadinessBlockers,
    confirmBlockedTitle,
    lineReadinessBlockerDetails,
    scheduleBlocksConfirm,
    scheduleMismatchConfirmMessage,
    ptRows,
    whPreview,
    retentionIntent,
    resetForm,
    fetchList,
    showSaveError,
    t,
  ]);

  // ── Cancel purchase ────────────────────────────────────────────────
  // ZH-RETENTION-SRI-ANNULMENT-01 — si la retención ya está AUTORIZADA por el SRI la compra no se
  // anula localmente: el backend responde SRI_ANNULMENT_REQUIRED_CODE y se ofrece iniciar el trámite
  // de anulación ante el SRI. Iniciarlo NO anula la compra (el DTO devuelto sigue "Confirmed").
  const runCancel = useCallback(
    async (reason: string, requestSriAnnulment: boolean) => {
      if (!editing) return;
      setSaving(true);
      try {
        const result = await purchaseService.cancel(editing.id, reason, requestSriAnnulment);
        if (result.status === "Cancelled") {
          message.success(
            t("purchases.messages.cancelled", "Compra anulada correctamente."),
          );
          resetForm();
          setTab("listado");
          fetchList();
        } else {
          message.info(
            t(
              "purchases.messages.sriAnnulmentRequested",
              "Se registró la solicitud de anulación ante el SRI. La compra sigue vigente hasta que el SRI confirme ANULADO.",
            ),
          );
          await loadForEdit(editing.id);
        }
      } catch (err: unknown) {
        if (!requestSriAnnulment && readApiErrorCode(err) === SRI_ANNULMENT_REQUIRED_CODE) {
          setSriAnnulmentReason(reason);
        } else {
          const [detail, ...rest] = resolveApiErrorDetails(
            err,
            t("purchases.errors.cancelFailed", "Error al anular."),
          );
          showSaveError(detail, rest);
        }
      } finally {
        setSaving(false);
      }
    },
    [editing, resetForm, fetchList, showSaveError, t, loadForEdit],
  );

  // FASE 5 PURCHASE-UX — descarta el borrador guardado (soft, sin reversos). Al completar limpia el
  // formulario y vuelve al listado refrescado. Nunca aplica a una compra confirmada (eso es Anular).
  const handleDiscardDraft = useCallback(
    async (reason: string) => {
      setModalDiscardDraft(false);
      if (!editing || editing.status !== "Draft") return;
      setSaving(true);
      try {
        await purchaseService.discardDraft(editing.id, reason);
        message.success(t("purchases.messages.draftDiscarded", "Borrador descartado."));
        resetForm();
        setTab("listado");
        fetchList();
      } catch (err: unknown) {
        const [detail, ...rest] = resolveApiErrorDetails(
          err,
          t("purchases.errors.discardFailed", "No se pudo descartar el borrador."),
        );
        showSaveError(detail, rest);
      } finally {
        setSaving(false);
      }
    },
    [editing, resetForm, fetchList, showSaveError, t],
  );

  const handleCancel = useCallback(
    async (reason: string) => {
      setModalCancelReason(false);
      await runCancel(reason, false);
    },
    [runCancel],
  );

  const handleStartSriAnnulment = useCallback(async () => {
    const reason = sriAnnulmentReason;
    setSriAnnulmentReason(null);
    if (reason) await runCancel(reason, true);
  }, [sriAnnulmentReason, runCancel]);

  const handleRetentionAnnulmentChanged = useCallback(async () => {
    if (editing) await loadForEdit(editing.id);
  }, [editing, loadForEdit]);

  // ── Discount ───────────────────────────────────────────────────────
  const handleApplyDiscount = useCallback(
    async (val: string) => {
      setModalDiscount(false);
      if (!editing || blockIfUnsaved()) return;
      try {
        const r = await purchaseService.applyDiscount(editing.id, Number(val));
        setEditing(r);
        await loadForEdit(editing.id);
        message.success(
          t(
            "purchases.messages.discountApplied",
            "Descuento aplicado correctamente.",
          ),
        );
      } catch {
      showSaveError(
        t("purchases.errors.discountFailed", "Error al aplicar descuento."),
      );
      }
    },
    [editing, blockIfUnsaved, loadForEdit, showSaveError, t],
  );

  // ── Recalculate ────────────────────────────────────────────────────
  const handleRecalculate = useCallback(async () => {
    if (!editing || blockIfUnsaved()) return;
    try {
      const r = await purchaseService.recalculate(editing.id);
      setEditing(r);
      await loadForEdit(editing.id);
      message.success(
        t("purchases.messages.recalculated", "Compra recalculada correctamente."),
      );
    } catch {
      showSaveError(t("purchases.errors.recalculateFailed", "Error al recalcular."));
    }
  }, [editing, blockIfUnsaved, loadForEdit, showSaveError, t]);

  // PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — distribución de costos del borrador guardado.
  const costDistributions = usePurchaseCostDistributions({
    editing,
    isDraft,
    saving,
    hasUnsavedChanges,
    reload: loadForEdit,
  });

  // ── Retención en el borrador (ZH-PURCHASE-RETENTION-CONFIRM-01) ─────
  // La vista previa se pide sola cada vez que se abre o recarga un borrador (guardar, descuento,
  // flete, recálculo vuelven a cargar `editing`), así los montos propuestos siempre corresponden a
  // la compra persistida. Una respuesta tardía de un borrador anterior se descarta.
  const previewRequestRef = useRef(0);
  const refreshRetentionPreview = useCallback(
    async (purchaseId: string) => {
      const requestId = ++previewRequestRef.current;
      setWhLoading(true);
      setWhPreviewError(null);
      try {
        const preview = await purchaseService.retentionPreview(purchaseId);
        if (requestId === previewRequestRef.current) setWhPreview(preview);
      } catch (err: unknown) {
        if (requestId === previewRequestRef.current) {
          setWhPreview(null);
          setWhPreviewError(
            formatApiRequestError(err, {
              generic: t("purchases.errors.retentionPreviewFailed", "Error al calcular retención."),
            }),
          );
        }
      }
      if (requestId === previewRequestRef.current) setWhLoading(false);
    },
    [t],
  );

  useEffect(() => {
    if (!editing || editing.status !== "Draft") return;
    void refreshRetentionPreview(editing.id);
  }, [editing, refreshRetentionPreview]);

  // Otra compra → la decisión de retener no se arrastra: resetForm/loadFromReception la vacían y
  // loadForEdit restaura la guardada en el borrador (PURCHASE-DRAFT-RECOVERY F3).

  // Sin líneas propuestas (no elegible) la intención no puede quedar activa. Solo se decide con una
  // vista previa ya cargada: al reabrir el borrador la intención restaurada no se apaga antes de
  // que llegue la elegibilidad del backend.
  const canApplyRetention = canApplyPurchaseRetention(whPreview);
  const wantsRetention = retentionIntent.appliesRetention;
  const hasRetentionPreview = whPreview !== null;
  useEffect(() => {
    if (wantsRetention && hasRetentionPreview && !canApplyRetention && !whLoading)
      setRetentionIntent((prev) => ({ ...prev, appliesRetention: false }));
  }, [wantsRetention, hasRetentionPreview, canApplyRetention, whLoading]);

  // Puntos de emisión: solo cuando el usuario decide emitir y puede leerlos; se preselecciona el
  // punto por defecto (el usuario puede cambiarlo).
  useEffect(() => {
    if (!wantsRetention || !canReadEmissionPoints) return;
    let cancelled = false;
    emissionPointLookupFacade
      .list("active")
      .then((rows) => {
        if (cancelled) return;
        setEmissionPoints(rows);
        const fallback = rows.find((p) => p.isDefault) ?? (rows.length === 1 ? rows[0] : undefined);
        if (fallback)
          setRetentionIntent((prev) => (prev.emissionPointId ? prev : { ...prev, emissionPointId: fallback.id }));
      })
      .catch(() => {
        if (!cancelled) setEmissionPoints([]);
      });
    return () => {
      cancelled = true;
    };
  }, [wantsRetention, canReadEmissionPoints]);

  const updateRetentionIntent = useCallback(
    (patch: Partial<PurchaseRetentionIntentState>) =>
      setRetentionIntent((prev) => ({ ...prev, ...patch })),
    [],
  );

  // ── Documento electrónico de la retención (XML/RIDE) ────────────────────────────
  // ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A — la transmisión al SRI es automática al confirmar la
  // compra (con recuperación en el servidor); aquí solo se consultan XML/RIDE de una retención ya
  // Issued (autorizados por el permiso del origen, purchases.view, resuelto en el servidor).
  const handleViewRetentionXml = useCallback(async () => {
    if (!retention) return;
    setElectronicPending(true);
    try {
      const blob = await purchaseRetentionFacade.getElectronicXmlBlob(retention.id);
      downloadBlob(blob, `retencion-${retention.retentionNumber ?? retention.id}.xml`);
    } catch (err: unknown) {
      showSaveError(
        formatApiRequestError(err, { generic: "No se pudo obtener el XML de la retención." }),
      );
    }
    setElectronicPending(false);
  }, [retention, showSaveError]);

  const handleViewRetentionRidePdf = useCallback(async () => {
    if (!retention) return;
    setElectronicPending(true);
    try {
      const blob = await purchaseRetentionFacade.getRidePdfBlob(retention.id);
      downloadBlob(blob, `retencion-${retention.retentionNumber ?? retention.id}.pdf`);
    } catch (err: unknown) {
      showSaveError(
        formatApiRequestError(err, { generic: "No se pudo obtener el PDF (RIDE) de la retención." }),
      );
    }
    setElectronicPending(false);
  }, [retention, showSaveError]);

  // ── Schedule operations ────────────────────────────────────────────
  const regenerateSchedule = useCallback(() => {
    setPtRows(
      generateScheduleRows(
        ptInstallments,
        ptDaysBetween,
        localTotal,
        getValues("issueDate"),
      ),
    );
    // PURCHASE-DRAFT-RECOVERY F2 — regenerar con la condición de pago tal cual = cuotas generadas
    // (no se guardan); con nro./días personalizados = cuotas manuales (se guardan).
    const term = paymentTermsList.find((p) => p.id === getValues("paymentTermId"));
    setPtManual(
      !term ||
        term.installments !== ptInstallments ||
        term.daysBetweenInstallments !== ptDaysBetween,
    );
  }, [ptInstallments, ptDaysBetween, localTotal, getValues, paymentTermsList]);

  const addInstallment = useCallback(() => {
    const issueDate = getValues("issueDate");
    if (!issueDate) return;
    const lastRow = ptRows.length > 0 ? ptRows[ptRows.length - 1] : null;
    // Fecha de negocio: aritmética de calendario pura, sin Date/zona del navegador.
    const due = addDaysIso(lastRow ? lastRow.dueDate : issueDate, ptDaysBetween);
    setPtRows((prev) => [
      ...prev,
      {
        number: prev.length + 1,
        dueDate: due,
        amount: 0,
        notes: "",
      },
    ]);
    setPtInstallments((prev) => prev + 1);
    setPtManual(true);
  }, [ptRows, ptDaysBetween, getValues]);

  const removeInstallment = useCallback(
    (idx: number) => {
      if (ptRows.length <= 1) return;
      setPtRows((prev) =>
        prev
          .filter((_, i) => i !== idx)
          .map((r, i) => ({ ...r, number: i + 1 })),
      );
      setPtInstallments((prev) => Math.max(1, prev - 1));
      setPtManual(true);
    },
    [ptRows],
  );

  const updateScheduleRow = useCallback(
    (
      idx: number,
      field: "dueDate" | "amount" | "notes",
      value: string | number,
    ) => {
      setPtRows((prev) =>
        prev.map((r, i) => (i === idx ? { ...r, [field]: value } : r)),
      );
      setPtManual(true);
    },
    [],
  );

  const handlePaymentTermChange = useCallback(
    (ptId: string) => {
      setValue("paymentTermId", ptId);
      if (!ptId) return;
      const pt = paymentTermsList.find((p) => p.id === ptId);
      if (pt) {
        setPtInstallments(pt.installments);
        setPtDaysBetween(pt.daysBetweenInstallments);
        setPtLoaded(true);
        setPtManual(false);
        setPtRows(
          generateScheduleRows(
            pt.installments,
            pt.daysBetweenInstallments,
            localTotal,
            getValues("issueDate"),
          ),
        );
      }
    },
    [setValue, paymentTermsList, localTotal, getValues],
  );

  // ── Return ─────────────────────────────────────────────────────────
  return {
    // Page
    tab,
    setTab,
    listItems,
    listLoading,
    listSearchInput,
    setListSearchInput,
    listStatus,
    setListStatus,
    listPage,
    setListPage,
    listTotal,
    listPageSize,
    saving,
    saveError,
    saveErrorDetails,
    setSaveError: showSaveError,
    duplicateAccessKey,
    duplicateAccessKeyChecking,
    isDuplicateAccessKeyBlocking,
    duplicateAccessKeyTitle,
    duplicateAccessKeyDetail,
    editing,
    receptionProcessingNotice,
    setReceptionProcessingNotice,

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
    breakdownSourceId, setBreakdownSourceId,
    applyBreakdown: (sourceId: string, components: PurchaseLineFormValues[]) => {
      if (fieldDisabled) return false;
      const current = getValues("lines");
      const next = replaceBreakdownRows(current, sourceId, components, lineKey,
        resolvePrecisionDecimals(precisionPolicy, "quantity"));
      if (next === current) return false;
      setValue("lines", next, { shouldDirty: true, shouldValidate: true });
      setLineKey(lineKey + components.length);
      return true;
    },
    lines,
    addLine,
    removeLine,
    duplicateLine,
    updateLine,
    addLineWithItem,
    lineKey,
    updateLineWarehouse,
    applyGlobalWarehouse,
    matchingKey,
    handleMatchItem,
    handleSaveSupplierPresentation,
    handleUnmatchItem,
    handleCorrectMatch,

    // Supplier
    supplierProfile,
    handleSupplierChange,

    // Reference data
    warehouses,
    sriDocTypes,
    sriDocTypesLoaded,
    sriDocTypesUnavailable,
    canUseSriDocTypes,
    sriPaymentMethods,
    sriTaxSupports,
    paymentTermsList,
    vatRatesMap,
    iceRatesMap,

    // Derived
    isDraft,
    readOnly,
    documentState,
    fieldDisabled,
    hasUnsavedChanges,
    hasUnsavedFormChanges,
    unsavedChangesMessage,
    hasUnsavedWork,
    requestLeave,
    pendingLeave: pendingLeave !== null,
    confirmLeave,
    cancelLeave,
    isSupplierInactiveBlocking,
    supplierInactiveMessage: supplierProfile
      ? formatSupplierInactiveMessage(supplierProfile.name)
      : "",
    supplierInactiveDetail,
    lineReadinessByKey,
    lineReadinessBlockers,
    hasLineReadinessBlockers,
    hasLineSaveBlockers,
    confirmBlockedTitle,
    scheduleBlocksConfirm,
    scheduleMismatchConfirmMessage,
    lineReadinessBlockerDetails,
    lineReadinessBlockedTitle,
    localSummary,
    summaryTotals,
    localTotal,
    hasPersistedSchedule,
    pendingReceptionItems,

    // Schedule
    ptInstallments,
    setPtInstallments,
    ptDaysBetween,
    setPtDaysBetween,
    ptRows,
    setPtRows,
    ptLoaded,
    ptRowsSum,
    ptMismatch,
    regenerateSchedule,
    addInstallment,
    removeInstallment,
    updateScheduleRow,
    handlePaymentTermChange,

    // Retención: definida en el borrador, emitida al confirmar (ZH-PURCHASE-RETENTION-CONFIRM-01)
    whPreview,
    whPreviewError,
    refreshRetentionPreview,
    retentionIntent,
    updateRetentionIntent,
    canApplyRetention,
    emissionPoints,
    canReadEmissionPoints,
    retention,
    whLoading,
    electronicPending,
    handleViewRetentionXml,
    handleViewRetentionRidePdf,

    // Actions
    fetchList,
    resetForm,
    loadForEdit,
    loadFromReception,
    handleSave,
    handleConfirm,
    handleCancel,
    handleDiscardDraft,
    sriAnnulmentReason,
    setSriAnnulmentReason,
    handleStartSriAnnulment,
    handleRetentionAnnulmentChanged,
    canOperateRetentionAnnulment: has("purchases.update"),
    handleApplyDiscount,
    handleRecalculate,
    ...costDistributions,
    costDistributionLineIds: costDistributionSummary.lineIds,

    // Modals
    modalConfirm,
    setModalConfirm,
    modalDiscount,
    setModalDiscount,
    modalCancelReason,
    setModalCancelReason,
    modalDiscardDraft,
    setModalDiscardDraft,
    modalPendingProducts,
    setModalPendingProducts,
    pendingProductSources,
    modalPendingProductSources,
    openPendingProducts,
    closePendingProducts,
    xmlLinesSummary,
    applyResolvedLines,

    // UI toggles
    showElectronic,
    setShowElectronic,
    showNotes,
    setShowNotes,

    // Global search
    globalQuery,
    setGlobalQuery,
    globalResults,
    globalSearchStatus,
    retryGlobalSearch,
    globalOpen,
    setGlobalOpen,
    globalFilter,
    setGlobalFilter,
    globalFocusIdx,
    setGlobalFocusIdx,
    itemTypeOptions,
    globalSearchRef,
    handleGlobalKeyDown,

    // Item context
    fetchItemContext,
  };
}

export type PurchasesPageContext = ReturnType<typeof usePurchasesPage>;
