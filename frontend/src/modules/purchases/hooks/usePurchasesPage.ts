import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import type {
  PurchaseAccessKeyLookupDto,
  PurchaseInvoiceDto,
  PurchaseListItemDto,
  RetentionPreviewDto,
  PurchaseCostDistributionType,
} from "../api/purchaseService";
import { purchaseService } from "../api/purchaseService";
import {
  purchaseRetentionFacade,
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
} from "../api/purchaseReceptionService";
import {
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
  generateScheduleRows,
  roundToTotalAmount,
  buildCostDistributionInputFromFormLines,
  simulateCostDistribution,
} from "../utils/purchaseCalc";
import { applyServerErrors } from "../../lib/validationErrors";
import {
  readApiErrorMessage,
  readApiErrorMessages,
  formatApiRequestError,
} from "../../lib/apiError";
import { message } from "../../../lib/messages";
import { useI18n } from "../../../i18n/i18n";
import { usePrecisionPolicy } from "../../../hooks/usePrecisionPolicy";
import {
  todayIso,
  addDaysIso,
  fromDateTimeLocalInputValue,
} from "../../../lib/formatters/dateFormatters";
import { normalizeOptionalCode } from "../../../lib/sanitizers";
import {
  createPurchaseInvoiceSchema,
  emptyPurchaseInvoiceForm,
  type PurchaseInvoiceFormValues,
  type PurchaseLineFormValues,
} from "../schemas/purchaseInvoiceSchema";
import {
  buildSupplierInactiveMessage,
  buildSupplierProfile,
  type SupplierProfile,
} from "../utils/supplierProfile";
import {
  buildPurchaseItemLabel,
  buildPurchaseLineFromItem,
  normalizePurchaseLinePresentation,
} from "../utils/purchaseItemProfile";
import {
  getPurchaseLineBlockingReasons,
  getPurchaseLineReadiness,
} from "../utils/purchaseLineReadiness";

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
  const { canShow, has } = usePermissionsUi();
  const canRegisterElectronic = canShow("electronic-documents.retry");
  const canReadEmissionPoints = has("settings.emission-points.view");
  // PURCHASES-RETENTIONS-CANCEL-05D — mismo permiso que ya protege emitir/editar la compra
  // (PurchasePermissions.Update); sin permiso nuevo de Retenciones.
  const canUpdatePurchase = canShow("purchases.update");

  // ── Modals ─────────────────────────────────────────────────────────
  const [modalConfirm, setModalConfirm] = useState(false);
  const [modalDiscount, setModalDiscount] = useState(false);
  const [modalCancelReason, setModalCancelReason] = useState(false);
  // PURCHASES-RETENTIONS-CANCEL-05D — modal crítico de anulación de RetentionDocument.
  const [modalRetentionCancel, setModalRetentionCancel] = useState(false);
  // PURCHASE-FREIGHT-DISTRIBUTION-MODAL-01
  const [modalDistributeCost, setModalDistributeCost] = useState(false);
  // COMPRAS-METODO-ZH-01B — resolución masiva de líneas XML sin producto.
  const [modalPendingProducts, setModalPendingProducts] = useState(false);

  // ── Collapsible sections ───────────────────────────────────────────
  const [showElectronic, setShowElectronic] = useState(false);
  const [showNotes, setShowNotes] = useState(false);

  // ── Global search ──────────────────────────────────────────────────
  const [globalQuery, setGlobalQuery] = useState("");
  const [globalResults, setGlobalResults] = useState<ItemDto[]>([]);
  const [globalOpen, setGlobalOpen] = useState(false);
  const [globalFilter, setGlobalFilter] = useState<SearchFilter>("all");
  const itemTypesState = useItemTypeOptions();
  const itemTypeOptions = itemTypesState.data ?? [];
  const [globalFocusIdx, setGlobalFocusIdx] = useState(-1);
  const globalSearchRef = useRef<HTMLDivElement>(null);
  const globalDebounce = useRef<ReturnType<typeof setTimeout>>(undefined);

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
  const hasPersistedSchedule =
    editing && editing.paymentSchedules && editing.paymentSchedules.length > 0;

  const localSummary = useMemo(
    () => calcSummary(lines, 0, 0, vatRatesMap, iceRatesMap),
    [lines, vatRatesMap, iceRatesMap],
  );
  const localTotal = editing
    ? editing.grandTotal
    : localSummary.netSubtotal +
      localSummary.vat +
      localSummary.ice +
      formWatch.freightCost +
      formWatch.otherCosts;

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
  const lineReadinessBlockedTitle = t(
    "purchases.lineReadiness.saveBlockedTitle",
    "No se puede guardar la compra.",
  );

  const ptRowsSum = ptRows.reduce((s, r) => s + r.amount, 0);
  const ptMismatch =
    ptRows.length > 0 &&
    localTotal > 0 &&
    roundToTotalAmount(ptRowsSum) !== roundToTotalAmount(localTotal);
  const sriDocTypesUnavailable =
    sriDocTypesLoaded && (sriDocTypesLoadFailed || sriDocTypes.length === 0);
  const canUseSriDocTypes = sriDocTypesLoaded && !sriDocTypesUnavailable;
  const isDuplicateAccessKeyBlocking =
    !!duplicateAccessKey?.exists &&
    duplicateAccessKey.purchaseId !== editing?.id;
  const isSupplierInactiveBlocking = supplierProfile?.isActive === false;

  // PURCHASE-DISTRIBUTE-COST-BEFORE-SAVE-01 — el botón "Distribuir flete/gasto" NO depende de
  // `editing` (compra ya persistida): se habilita apenas la compra esté lista para guardar, sin
  // forzar guardar/reabrir. Reutiliza EXACTAMENTE los mismos chequeos que bloquean handleSave, para
  // que el botón y "Guardar" queden siempre en sincronía.
  const distributeCostDisabledReason = useMemo(() => {
    if (!isDraft)
      return t(
        "purchases.distributeCost.disabled.notEditable",
        "Solo puede distribuir flete/gasto en compras editables.",
      );
    if (saving)
      return t(
        "purchases.distributeCost.disabled.saving",
        "Espere a que termine la operación en curso.",
      );
    if (lines.length === 0)
      return t(
        "purchases.distributeCost.disabled.noLines",
        "Agregue al menos un ítem antes de distribuir flete/gasto.",
      );
    if (hasLineReadinessBlockers)
      return t(
        "purchases.distributeCost.disabled.linesIncomplete",
        "Complete las líneas antes de distribuir flete/gasto.",
      );
    if (!formWatch.supplierId)
      return t(
        "purchases.distributeCost.disabled.supplier",
        "Seleccione un proveedor antes de distribuir flete/gasto.",
      );
    if (!formWatch.docTypeCode)
      return t(
        "purchases.distributeCost.disabled.docType",
        "Seleccione el tipo de documento antes de distribuir flete/gasto.",
      );
    if (isDuplicateAccessKeyBlocking)
      return t(
        "purchases.distributeCost.disabled.duplicate",
        "Resuelva la clave de acceso duplicada antes de distribuir flete/gasto.",
      );
    if (isSupplierInactiveBlocking)
      return t(
        "purchases.distributeCost.disabled.supplierInactive",
        "El proveedor está inactivo.",
      );
    if (!canUseSriDocTypes)
      return t(
        "purchases.distributeCost.disabled.sriCatalog",
        "El catálogo de tipos de documento SRI no está disponible.",
      );
    return null;
  }, [
    isDraft,
    saving,
    lines.length,
    hasLineReadinessBlockers,
    formWatch.supplierId,
    formWatch.docTypeCode,
    isDuplicateAccessKeyBlocking,
    isSupplierInactiveBlocking,
    canUseSriDocTypes,
    t,
  ]);

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
        if (blocks && showMessage) {
          showSaveError(duplicateAccessKeyTitle, [duplicateAccessKeyDetail]);
          message.error(`${duplicateAccessKeyTitle} ${duplicateAccessKeyDetail}`);
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
        const map: Record<string, number> = {};
        for (const r of rates) map[r.code] = r.percentage;
        setIceRatesMap(map);
      })
      .catch(() => {});
  }, []);

  // ── Global search effects ─────────────────────────────────────────
  useEffect(() => {
    clearTimeout(globalDebounce.current);
    if (!globalOpen || globalQuery.length < 2) {
      setGlobalResults([]);
      return;
    }
    globalDebounce.current = setTimeout(async () => {
      try {
        const res = await itemLookupFacade.search({
          search: globalQuery.trim(),
          isActive: true,
          itemTypeId: globalFilter === "all" ? undefined : globalFilter,
          pageSize: 10,
        });
        setGlobalResults(res.items);
        setGlobalFocusIdx(-1);
      } catch {
        setGlobalResults([]);
      }
    }, 300);
    return () => clearTimeout(globalDebounce.current);
  }, [globalQuery, globalOpen, globalFilter]);

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
  const fetchItemContext = useCallback(
    async (key: number, itemId: string, warehouseId: string) => {
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
        const latest = getValues("lines");
        setValue(
          "lines",
          latest.map((l) =>
            l._key === key ? { ...l, context: ctx, _contextLoading: false } : l,
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
      if (src) {
        const { _readinessIssue: _discard, ...copy } = src;
        void _discard;
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
      setValue(
        "lines",
        currentLines.map((l) =>
          l._key === key ? { ...l, [field]: value } : l,
        ),
      );
    },
    [getValues, setValue],
  );

  // ── Bodega de línea (ADR-030, regla de arquitectura permanente) ──────
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

  // Aplicación masiva, NO sincronización permanente (ADR-030): corre una única vez, al
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
        if (updated.itemId && wh)
          void fetchItemContext(key, updated.itemId, wh);
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
      if (!line?.purchaseReceptionLineId || !line.itemId || !line.packagingLevelId)
        return;

      setMatchingKey(key);
      try {
        const updated = await purchaseReceptionService.matchItem(
          line.purchaseReceptionLineId,
          line.itemId,
          line.packagingLevelId,
        );
        const latest = getValues("lines");
        const selectedPackaging = line.context?.packagingLevels?.find(
          (p) => p.id === line.packagingLevelId,
        );
        const conversionFactor =
          selectedPackaging?.baseQuantity ?? line.conversionFactor ?? 1;
        setValue(
          "lines",
          latest.map((l) =>
            l._key === key
              ? {
                  ...l,
                  itemMatchStatus: updated.matchStatus,
                  packagingLevelId: line.packagingLevelId,
                  uomCode: selectedPackaging?.uomCode ?? line.uomCode,
                  baseUomCode: line.context?.baseUomCode ?? line.baseUomCode,
                  conversionFactor,
                  quantityInBaseUom: line.quantity * conversionFactor,
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
  const applyResolvedLines = useCallback(
    async (result: ResolveReceptionLinesResult) => {
      const resolved = new Map(result.lines.map((r) => [r.purchaseReceptionLineId, r]));
      const updated = getValues("lines").map((l) => {
        const r = l.purchaseReceptionLineId ? resolved.get(l.purchaseReceptionLineId) : undefined;
        if (!r) return l;
        return {
          ...l,
          itemId: r.itemId,
          itemMatchStatus: r.matchStatus,
          description: buildPurchaseItemLabel({ sku: r.itemSku, name: r.itemName }),
          packagingLevelId: r.packagingLevelId,
          uomCode: r.uomCode,
          baseUomCode: r.baseUomCode,
          conversionFactor: r.conversionFactor,
          quantityInBaseUom: l.quantity * r.conversionFactor,
          _readinessIssue: undefined,
        };
      });
      setValue("lines", updated, { shouldValidate: true });
      setModalPendingProducts(false);
      const globalWarehouseId = getValues("globalWarehouseId");
      for (const l of updated) {
        const warehouseId = l.warehouseId || globalWarehouseId;
        if (l.itemId && warehouseId && l.purchaseReceptionLineId && !l.context)
          void fetchItemContext(l._key, l.itemId, warehouseId);
      }
      message.success(
        t("purchases.pendingProducts.done", {
          created: result.itemsCreated,
          linked: result.linesLinked,
          learned: result.equivalencesLearned,
        }),
      );
    },
    [getValues, setValue, fetchItemContext, t],
  );

  const addLineWithItem = useCallback(
    async (item: ItemDto) => {
      setGlobalOpen(false);
      setGlobalQuery("");
      setGlobalResults([]);
      const key = lineKey;
      const detail = await itemLookupFacade.getById(item.id);
      const newLine = buildPurchaseLineFromItem(detail, { key });
      const currentLines = getValues("lines");
      setValue("lines", [...currentLines, newLine], { shouldValidate: true });
      setLineKey((k) => k + 1);
      const wh = getValues("globalWarehouseId");
      if (wh) void fetchItemContext(key, item.id, wh);
    },
    [lineKey, getValues, setValue, fetchItemContext],
  );

  const handleGlobalKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (!globalOpen || globalResults.length === 0) return;
      if (e.key === "ArrowDown") {
        e.preventDefault();
        setGlobalFocusIdx((i) => Math.min(i + 1, globalResults.length - 1));
      }
      if (e.key === "ArrowUp") {
        e.preventDefault();
        setGlobalFocusIdx((i) => Math.max(i - 1, 0));
      }
      if (e.key === "Enter" && globalFocusIdx >= 0) {
        e.preventDefault();
        void addLineWithItem(globalResults[globalFocusIdx]);
      }
      if (e.key === "Escape") setGlobalOpen(false);
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
        message.error(`${text} ${supplierInactiveDetail}`);
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
    setGlobalQuery("");
    setGlobalResults([]);
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
        }));

        // SRI codes
        const savedPm = inv.sriPaymentMethodCode ?? "";
        const savedTs = inv.taxSupportCode ?? "";
        const cachedProfile = profileCache.current.get(inv.supplierId);

        reset({
          supplierId: inv.supplierId,
          docTypeCode: inv.docTypeCode,
          invoiceNumber: inv.invoiceNumber,
          issueDate: inv.issueDate,
          accessKey: inv.accessKey ?? "",
          authorizationNumber: inv.authorizationNumber ?? "",
          authorizationDate: toDateTimeLocalInputValue(inv.authorizationDate),
          globalWarehouseId: inv.globalWarehouseId ?? "",
          freightCost: inv.totalFreight,
          otherCosts: inv.totalOtherCosts,
          dueDate: inv.dueDate ?? "",
          notes: inv.notes ?? "",
          sriPaymentMethodCode:
            savedPm || cachedProfile?.config?.defaultPaymentMethodCode || "",
          taxSupportCode:
            savedTs || cachedProfile?.config?.defaultTaxSupportCode || "",
          paymentTermId: inv.paymentTermId ?? "",
          lines: mappedLines,
        });

        setLineKey(inv.lines.length + 1);

        // Fetch context for each line
        for (const ml of mappedLines) {
          const wh = ml.warehouseId || inv.globalWarehouseId;
          if (ml.itemId && wh) void fetchItemContext(ml._key, ml.itemId, wh);
        }

        // Reconstruye el estado de Item Matching (badge Auto/Manual/Pendiente/Revisar) de las líneas
        // que vienen de Recepción Electrónica — no se persiste en PurchaseInvoiceDetail, se lee en
        // vivo desde PurchaseReceptionLine vía el mismo backend que usa la pantalla de Recepción.
        for (const ml of mappedLines) {
          if (!ml.purchaseReceptionLineId) continue;
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
        setPtInstallments(inv.paymentTermInstallments);
        setPtDaysBetween(inv.paymentTermDaysBetween);
        setPtLoaded(true);
        if (!inv.paymentSchedules || inv.paymentSchedules.length === 0) {
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
        message.error(t("purchases.messages.loadFailed", "No se pudo cargar la compra."));
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
            xmlSupplierCode: l.supplierCode ?? undefined,
            xmlDescription: l.description,
            xmlSupplierAuxCode: l.supplierAuxCode,
            // PURCHASE-LINE-PACKAGING-XML-SNAPSHOT-IMMUTABLE-01 — copia congelada de
            // quantity/unitPrice tal como vino del draft, ANTES de que el usuario edite la
            // cabecera. `quantity`/`unitPrice` de arriba siguen siendo los campos editables
            // (`ctx.updateLine` los sobrescribe); estos dos nunca se tocan de nuevo.
            xmlQuantity: l.quantity,
            xmlUnitPrice: l.unitPrice,
            xmlDiscount: l.discount,
            xmlLineSubtotal: l.lineSubtotal,
            xmlTaxCode: l.taxCode,
            xmlVatPercentage: l.vatPercentage,
            xmlTaxValue: l.taxValue,
            xmlTotalLine: l.totalLine,
            xmlTaxableBase:
              l.taxes.find((tx) => tx.taxCode === "2")?.taxableBase ??
              l.lineSubtotal - l.discount,
            xmlIceAmount: l.taxes.find((tx) => tx.taxCode === "3")?.taxAmount,
            xmlIrbpnrAmount: l.taxes.find((tx) => tx.taxCode === "5")?.taxAmount,
            xmlAdditionalFields: l.additionalFields
              .slice()
              .sort((a, b) => a.sortOrder - b.sortOrder)
              .map((f) => ({ name: f.name, value: f.value })),
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
        setWhPreview(null);
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
        message.error(
          backendMessage === DUPLICATE_PURCHASE_DETAIL
            ? `${duplicateAccessKeyTitle} ${duplicateAccessKeyDetail}`
            : isSupplierInactiveError
              ? `${supplierInactiveBackendMessage} ${supplierInactiveDetail}`
            : backendMessage ??
              t(
                "purchases.messages.receptionDraftFailed",
                "No se pudo generar el borrador de compra.",
              ),
        );
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
    if (hasLineReadinessBlockers) {
      showSaveError(lineReadinessBlockedTitle, lineReadinessBlockerDetails);
      message.error(
        `${lineReadinessBlockedTitle} ${lineReadinessBlockerDetails[0] ?? ""}`,
      );
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
      message.error(`${title} ${detail}`);
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
      message.error(`${title} ${detail}`);
      return;
    }
    setSaving(true);
    try {
      const payload = {
        supplierId: data.supplierId,
        docTypeCode: data.docTypeCode,
        invoiceNumber: data.invoiceNumber,
        issueDate: data.issueDate,
        lines: data.lines.map((l: PurchaseLineFormValues) => ({
          itemId: l.itemId,
          description: l.description,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          vatCode: l.vatCode,
          discountPct: l.discountPct ?? 0,
          iceCode: normalizeOptionalCode(l.iceCode),
          warehouseId: l.warehouseId,
          notes: l.notes,
          purchaseReceptionLineId: l.purchaseReceptionLineId ?? undefined,
          packagingLevelId: l.packagingLevelId ?? undefined,
          freightAllocated: l.freightAllocated ?? undefined,
          otherCostsAllocated: l.otherCostsAllocated ?? undefined,
        })),
        accessKey: normalizeOptionalCode(data.accessKey),
        authorizationNumber: normalizeOptionalCode(data.authorizationNumber),
        // ZH-TEMPORAL-CONTRACT-02: datetime-local = hora de Company.Timezone → UTC una sola vez.
        authorizationDate: fromDateTimeLocalInputValue(data.authorizationDate),
        taxSupportCode: normalizeOptionalCode(data.taxSupportCode),
        sriPaymentMethodCode: normalizeOptionalCode(data.sriPaymentMethodCode),
        globalWarehouseId: data.globalWarehouseId || null,
        freightCost: data.freightCost,
        otherCosts: data.otherCosts,
        dueDate: data.dueDate || null,
        notes: data.notes || null,
        paymentTermId: data.paymentTermId || null,
      };

      if (editing) {
        await purchaseService.update(editing.id, {
          ...payload,
          id: editing.id,
        });
        message.success(
          t(
            "purchases.messages.updated",
            "Compra actualizada correctamente.",
          ),
        );
      } else {
        await purchaseService.create(payload);
        message.success(
          t("purchases.messages.draftSaved", "Borrador guardado correctamente."),
        );
      }
      resetForm();
      setTab("listado");
      fetchList();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setFieldError);
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
          message.error(`${supplierInactiveMessage} ${supplierInactiveDetail}`);
          return;
        }
        const title = isDuplicateAccessKeyError
          ? duplicateAccessKeyTitle
          : t(
              "purchases.validation.saveBlocked",
              "No se puede guardar la compra.",
            );
        showSaveError(title, apiMessages);
        message.error(`${title} ${apiMessages[0]}`);
      } else if (!applied) {
        const e = err as ApiErrorLike;
        showSaveError(
            e?.response?.data?.message?.user ??
            e?.response?.data?.data?.errors?.[0] ??
            e?.message ??
            t("purchases.errors.saveFailed", "Error al guardar."),
        );
      }
    }
    setSaving(false);
  });

  // ── Confirm purchase ───────────────────────────────────────────────
  const handleConfirm = useCallback(async () => {
    setModalConfirm(false);
    if (!editing) return;
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
      const e = err as ApiErrorLike;
      showSaveError(
          e?.response?.data?.message?.user ??
          e?.response?.data?.data?.errors?.[0] ??
          e?.message ??
          t("purchases.errors.confirmFailed", "Error al confirmar."),
      );
    }
    setSaving(false);
  }, [editing, ptRows, whPreview, retentionIntent, resetForm, fetchList, showSaveError, t]);

  // ── Cancel purchase ────────────────────────────────────────────────
  const handleCancel = useCallback(
    async (reason: string) => {
      setModalCancelReason(false);
      if (!editing) return;
      setSaving(true);
      try {
        await purchaseService.cancel(editing.id, reason);
        message.success(
          t("purchases.messages.cancelled", "Compra anulada correctamente."),
        );
        resetForm();
        setTab("listado");
        fetchList();
      } catch (err: unknown) {
        const e = err as ApiErrorLike;
        showSaveError(
            e?.response?.data?.message?.user ??
            e?.response?.data?.data?.errors?.[0] ??
            e?.message ??
            t("purchases.errors.cancelFailed", "Error al anular."),
        );
      }
      setSaving(false);
    },
    [editing, resetForm, fetchList, showSaveError, t],
  );

  // ── Discount ───────────────────────────────────────────────────────
  const handleApplyDiscount = useCallback(
    async (val: string) => {
      setModalDiscount(false);
      if (!editing) return;
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
    [editing, loadForEdit, showSaveError, t],
  );

  // ── Freight/Recalculate ────────────────────────────────────────────
  const handleAllocateFreight = useCallback(async () => {
    if (!editing) return;
    try {
      const r = await purchaseService.allocateFreight(editing.id);
      setEditing(r);
      await loadForEdit(editing.id);
      message.success(
        t("purchases.messages.freightAllocated", "Flete distribuido correctamente."),
      );
    } catch {
      showSaveError(
        t("purchases.errors.freightFailed", "Error al distribuir flete."),
      );
    }
  }, [editing, loadForEdit, showSaveError, t]);

  const handleRecalculate = useCallback(async () => {
    if (!editing) return;
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
  }, [editing, loadForEdit, showSaveError, t]);

  // PURCHASE-FREIGHT-DISTRIBUTION-MODAL-01 — aplica el prorrateo revisado por el usuario en el
  // modal "Distribuir flete/gasto" (suma amount solo entre includedLineIds). Retorna true/false
  // para que el modal sepa si debe cerrarse.
  const handleDistributeCost = useCallback(
    async (
      costType: "Freight" | "OtherCost",
      amount: number,
      includedLineIds: string[],
    ): Promise<boolean> => {
      if (!editing) return false;
      try {
        const r = await purchaseService.distributeCost(
          editing.id,
          costType,
          amount,
          includedLineIds,
        );
        setEditing(r);
        await loadForEdit(editing.id);
        message.success(
          t(
            "purchases.messages.costDistributed",
            "Valor distribuido correctamente.",
          ),
        );
        return true;
      } catch (err) {
        showSaveError(
          t("purchases.errors.distributeCostFailed", "Error al distribuir el valor."),
          readApiErrorMessages(err as ApiErrorLike),
        );
        return false;
      }
    },
    [editing, loadForEdit, showSaveError, t],
  );

  // PURCHASE-DISTRIBUTE-COST-BEFORE-SAVE-01 — misma fórmula/simulación que handleDistributeCost,
  // pero para una compra NUEVA sin guardar (sin PurchaseInvoice.Id todavía): no llama al backend,
  // aplica el prorrateo directo sobre el formulario (líneas + total documento) usando `_key` como
  // identificador estable de línea. Aditivo — respeta cualquier freightAllocated/otherCostsAllocated
  // ya aplicado en una vuelta previa del modal en esta misma sesión de edición.
  const handleDistributeCostToForm = useCallback(
    (
      costType: PurchaseCostDistributionType,
      amount: number,
      includedLineIds: string[],
    ): boolean => {
      const currentLines = getValues("lines");
      const sourceLines = buildCostDistributionInputFromFormLines(currentLines);
      const includedSet = new Set(includedLineIds);
      const preview = simulateCostDistribution(sourceLines, includedSet, amount);

      const field = costType === "Freight" ? "freightAllocated" : "otherCostsAllocated";
      const totalField = costType === "Freight" ? "freightCost" : "otherCosts";
      let totalAllocated = 0;

      preview.forEach((p) => {
        if (!p.included || p.allocatedAmount === 0) return;
        const idx = currentLines.findIndex((l) => String(l._key) === p.lineId);
        if (idx === -1) return;
        const prevAllocated =
          (currentLines[idx][field] as number | undefined) ?? 0;
        setValue(`lines.${idx}.${field}`, prevAllocated + p.allocatedAmount, {
          shouldDirty: true,
        });
        totalAllocated += p.allocatedAmount;
      });

      setValue(
        totalField,
        roundToTotalAmount((getValues(totalField) || 0) + totalAllocated),
        { shouldDirty: true },
      );

      message.success(
        t("purchases.messages.costDistributed", "Valor distribuido correctamente."),
      );
      return true;
    },
    [getValues, setValue, t],
  );

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

  // Otra compra → la decisión de retener no se arrastra.
  const editingId = editing?.id;
  useEffect(() => {
    setRetentionIntent(emptyPurchaseRetentionIntent(todayIso()));
  }, [editingId]);

  // Sin líneas propuestas (no elegible) la intención no puede quedar activa.
  const canApplyRetention = canApplyPurchaseRetention(whPreview);
  const wantsRetention = retentionIntent.appliesRetention;
  useEffect(() => {
    if (wantsRetention && !canApplyRetention && !whLoading)
      setRetentionIntent((prev) => ({ ...prev, appliesRetention: false }));
  }, [wantsRetention, canApplyRetention, whLoading]);

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

  // ── Anular retención (PURCHASES-RETENTIONS-CANCEL-05D) ──────────────
  // Reversa la CxP (AccountsPayable.ReverseRetention) y el asiento contable original — ambos vía
  // el mismo CancelRetentionCommand/RetentionCanceller transversal que ya usa Gastos.
  const handleCancelRetention = useCallback(
    async (reason: string) => {
      setModalRetentionCancel(false);
      if (!editing || !retention || whLoading) return;
      setWhLoading(true);
      try {
        const ret = await purchaseRetentionFacade.cancelForPurchase(editing.id, retention.id, reason);
        setRetention(ret);
        message.success(
          t("purchases.messages.retentionCancelled", "Retención anulada correctamente."),
        );
      } catch (err: unknown) {
        showSaveError(
          formatApiRequestError(err, {
            generic: t("purchases.errors.retentionCancelFailed", "Error al anular la retención."),
          }),
        );
      }
      setWhLoading(false);
    },
    [editing, retention, whLoading, showSaveError, t],
  );

  // ── Documento electrónico de la retención (XML/RIDE preview, registro manual) ────
  // PURCHASES-RETENTIONS-UI-MIGRATION-05C — nunca se genera/registra automáticamente al emitir;
  // son acciones explícitas del usuario sobre una retención ya Issued (mismos endpoints que ya usa
  // Gastos, RetentionsController — sin duplicar lógica ni crear pantalla propia de Retenciones).
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

  const handleRegisterRetentionElectronic = useCallback(async () => {
    if (!retention || !canRegisterElectronic) return;
    setElectronicPending(true);
    try {
      await purchaseRetentionFacade.registerElectronic(retention.id);
      message.success("Registro electrónico enviado correctamente.");
    } catch (err: unknown) {
      showSaveError(
        formatApiRequestError(err, {
          generic: "No se pudo registrar electrónicamente la retención.",
        }),
      );
    }
    setElectronicPending(false);
  }, [retention, canRegisterElectronic, showSaveError]);

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
  }, [ptInstallments, ptDaysBetween, localTotal, getValues]);

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
    fieldDisabled,
    isSupplierInactiveBlocking,
    distributeCostDisabledReason,
    supplierInactiveMessage: supplierProfile
      ? formatSupplierInactiveMessage(supplierProfile.name)
      : "",
    supplierInactiveDetail,
    lineReadinessByKey,
    lineReadinessBlockers,
    hasLineReadinessBlockers,
    lineReadinessBlockerDetails,
    lineReadinessBlockedTitle,
    localSummary,
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
    handleCancelRetention,
    canUpdatePurchase,
    electronicPending,
    canRegisterElectronic,
    handleViewRetentionXml,
    handleViewRetentionRidePdf,
    handleRegisterRetentionElectronic,

    // Actions
    fetchList,
    resetForm,
    loadForEdit,
    loadFromReception,
    handleSave,
    handleConfirm,
    handleCancel,
    handleApplyDiscount,
    handleAllocateFreight,
    handleRecalculate,
    handleDistributeCost,
    handleDistributeCostToForm,

    // Modals
    modalConfirm,
    setModalConfirm,
    modalDiscount,
    setModalDiscount,
    modalCancelReason,
    setModalCancelReason,
    modalRetentionCancel,
    setModalRetentionCancel,
    modalDistributeCost,
    setModalDistributeCost,
    modalPendingProducts,
    setModalPendingProducts,
    pendingProductSources,
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
