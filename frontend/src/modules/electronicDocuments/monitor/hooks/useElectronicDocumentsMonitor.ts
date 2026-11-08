import { useCallback, useEffect, useRef, useState } from "react";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { formatApiError } from "../../../lib/formatApiError";
import { todayIso } from "../../../../lib/formatters/dateFormatters";
import { useDebounce } from "../../../../hooks/useDebounce";
import { useRideActions } from "../../../ride/facades/rideGenerationFacade";
import {
  selectElectronicEmissionPaused,
  useElectronicInvoicingStatusStore,
} from "../../../../store/electronicInvoicingStatusStore";
import {
  electronicDocumentsMonitorService,
  type ElectronicDocumentDetailDto,
  type ElectronicDocumentsDashboardDto,
  type ElectronicDocumentsListResponse,
  type ElectronicDocumentXmlVariant,
} from "../api/electronicDocumentsMonitorService";
import { ELECTRONIC_DOCUMENT_STATE_GROUPS } from "../utils/enumOptions";

const PAGE_SIZE = 25;
/** Documentos que requieren atención en el Resumen: solo los más recientes (misma API de listado). */
export const ATTENTION_PAGE_SIZE = 5;
/** Espera tras la última tecla antes de consultar la búsqueda (el campo nunca se deshabilita). */
export const SEARCH_DEBOUNCE_MS = 350;

export type ElectronicDocumentsMonitorTab = "summary" | "documents" | "services";

export function useElectronicDocumentsMonitor() {
  const { canShow } = usePermissionsUi();
  const canView = canShow("electronic-documents.view");
  const canViewDetail = canShow("electronic-documents.detail");
  const canRetry = canShow("electronic-documents.retry");
  const canReconcile = canShow("electronic-documents.reconcile");
  const canRetransmitCorrected = canShow(
    "electronic-documents.retransmit-corrected",
  );
  // Mismo permiso que exige GET /api/v1/ride — sin él la acción no se muestra (evita un 403).
  const canDownloadRide = canShow("ride.view");
  // SRI-TOGGLE-06 — pausa de la empresa (no es un permiso): las acciones que llaman al SRI se deshabilitan.
  const sriPaused = useElectronicInvoicingStatusStore(selectElectronicEmissionPaused);
  const refreshHealth = useElectronicInvoicingStatusStore((s) => s.refreshHealth);

  const [activeTab, setActiveTab] =
    useState<ElectronicDocumentsMonitorTab>("summary");

  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [state, setState] = useState("");
  const [documentType, setDocumentType] = useState("");
  const [environment, setEnvironment] = useState("");
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebounce(search, SEARCH_DEBOUNCE_MS);
  const [page, setPage] = useState(1);

  const [list, setList] = useState<ElectronicDocumentsListResponse | null>(
    null,
  );
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);

  const [dashboard, setDashboard] =
    useState<ElectronicDocumentsDashboardDto | null>(null);
  const [dashboardLoading, setDashboardLoading] = useState(false);
  const [dashboardError, setDashboardError] = useState<string | null>(null);

  const [attention, setAttention] =
    useState<ElectronicDocumentsListResponse | null>(null);
  const [attentionLoading, setAttentionLoading] = useState(false);
  const [attentionError, setAttentionError] = useState<string | null>(null);

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<ElectronicDocumentDetailDto | null>(
    null,
  );
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  const [xmlContent, setXmlContent] = useState<string | null>(null);
  const [xmlVariant, setXmlVariant] =
    useState<ElectronicDocumentXmlVariant | null>(null);
  const [xmlLoading, setXmlLoading] = useState(false);
  const [xmlError, setXmlError] = useState<string | null>(null);

  const [retryLoading, setRetryLoading] = useState(false);

  // Protección contra respuestas obsoletas: solo la última solicitud de cada consulta actualiza el estado.
  const listRequest = useRef(0);
  const dashboardRequest = useRef(0);
  const attentionRequest = useRef(0);

  const { ridePending: rideDownloading, handleDownloadRide } = useRideActions(
    detail?.sourceModule ?? "",
  );

  const loadList = useCallback(async () => {
    const request = ++listRequest.current;
    setListLoading(true);
    setListError(null);
    try {
      const data = await electronicDocumentsMonitorService.getList({
        dateFrom: dateFrom || undefined,
        dateTo: dateTo || undefined,
        state: state || undefined,
        documentType: documentType || undefined,
        environment: environment || undefined,
        search: debouncedSearch || undefined,
        pageNumber: page,
        pageSize: PAGE_SIZE,
      });
      if (request !== listRequest.current) return;
      setList(data);
    } catch (e) {
      if (request !== listRequest.current) return;
      setListError(formatApiError(e));
      setList(null);
    } finally {
      if (request === listRequest.current) setListLoading(false);
    }
  }, [dateFrom, dateTo, state, documentType, environment, debouncedSearch, page]);

  const loadDashboard = useCallback(async () => {
    const request = ++dashboardRequest.current;
    setDashboardLoading(true);
    setDashboardError(null);
    try {
      const data = await electronicDocumentsMonitorService.getDashboard();
      if (request !== dashboardRequest.current) return;
      setDashboard(data);
    } catch (e) {
      if (request !== dashboardRequest.current) return;
      setDashboardError(formatApiError(e));
      setDashboard(null);
    } finally {
      if (request === dashboardRequest.current) setDashboardLoading(false);
    }
  }, []);

  const loadAttention = useCallback(async () => {
    const request = ++attentionRequest.current;
    setAttentionLoading(true);
    setAttentionError(null);
    try {
      const data = await electronicDocumentsMonitorService.getList({
        state: ELECTRONIC_DOCUMENT_STATE_GROUPS.attention.value,
        pageNumber: 1,
        pageSize: ATTENTION_PAGE_SIZE,
      });
      if (request !== attentionRequest.current) return;
      setAttention(data);
    } catch (e) {
      if (request !== attentionRequest.current) return;
      setAttentionError(formatApiError(e));
      setAttention(null);
    } finally {
      if (request === attentionRequest.current) setAttentionLoading(false);
    }
  }, []);

  // Cada pestaña consulta solo lo que muestra: el listado en Documentos; indicadores y documentos
  // que requieren atención en Resumen. Servicios SRI lee el store de salud (ZHSriServiceHealth).
  const documentsVisible = canView && activeTab === "documents";
  const summaryVisible = canView && activeTab === "summary";

  useEffect(() => {
    if (documentsVisible) void loadList();
  }, [documentsVisible, loadList]);

  useEffect(() => {
    if (!summaryVisible) return;
    void loadDashboard();
    void loadAttention();
  }, [summaryVisible, loadDashboard, loadAttention]);

  /** Recarga los datos de la pestaña visible (tras una acción del detalle o con Actualizar). */
  const reloadVisible = useCallback(() => {
    if (documentsVisible) void loadList();
    if (summaryVisible) {
      void loadDashboard();
      void loadAttention();
    }
  }, [documentsVisible, summaryVisible, loadList, loadDashboard, loadAttention]);

  const refresh = useCallback(() => {
    reloadVisible();
    void refreshHealth({ force: true });
  }, [reloadVisible, refreshHealth]);

  const openDetail = useCallback(
    async (id: string) => {
      setSelectedId(id);
      setDetail(null);
      setDetailError(null);
      setXmlContent(null);
      setXmlVariant(null);
      setXmlError(null);
      if (!canViewDetail) return;
      setDetailLoading(true);
      try {
        const data = await electronicDocumentsMonitorService.getDetail(id);
        setDetail(data);
      } catch (e) {
        setDetailError(formatApiError(e));
      } finally {
        setDetailLoading(false);
      }
    },
    [canViewDetail],
  );

  const closeDetail = useCallback(() => {
    setSelectedId(null);
    setDetail(null);
    setDetailError(null);
    setXmlContent(null);
    setXmlVariant(null);
    setXmlError(null);
  }, []);

  const viewXml = useCallback(
    async (variant: ElectronicDocumentXmlVariant) => {
      if (!detail) return;
      setXmlLoading(true);
      setXmlError(null);
      setXmlContent(null);
      setXmlVariant(variant);
      try {
        const xml = await electronicDocumentsMonitorService.getXml(
          detail.sourceModule,
          detail.sourceEntityId,
          variant,
        );
        setXmlContent(xml);
      } catch (e) {
        setXmlError(formatApiError(e));
      } finally {
        setXmlLoading(false);
      }
    },
    [detail],
  );

  const retryNow = useCallback(async () => {
    if (!detail) return;
    setRetryLoading(true);
    try {
      const updated = await electronicDocumentsMonitorService.retryNow(
        detail.id,
      );
      setDetail(updated);
      reloadVisible();
    } finally {
      setRetryLoading(false);
    }
  }, [detail, reloadVisible]);

  const reconcile = useCallback(
    async (values: { reference: string; observation: string }) => {
      if (!detail) return;
      const updated = await electronicDocumentsMonitorService.reconcile(
        detail.id,
        values,
      );
      setDetail(updated);
      reloadVisible();
    },
    [detail, reloadVisible],
  );

  const retransmitCorrected = useCallback(
    async (values: { reason: string; idempotencyKey: string }) => {
      if (!detail) return;
      const updated = await electronicDocumentsMonitorService.retransmitCorrected(
        detail.id,
        values,
      );
      setDetail(updated);
      reloadVisible();
    },
    [detail, reloadVisible],
  );

  const downloadRide = useCallback(async () => {
    if (!detail) return;
    await handleDownloadRide(
      detail.sourceEntityId,
      detail.documentNumber ?? undefined,
    );
  }, [detail, handleDownloadRide]);

  const resetFiltersAndReload = useCallback(() => {
    setDateFrom("");
    setDateTo("");
    setState("");
    setDocumentType("");
    setEnvironment("");
    setSearch("");
    setPage(1);
  }, []);

  /**
   * Indicadores del Resumen: aplican su filtro de estado (los demás filtros se conservan, como antes)
   * y abren Documentos en la página 1.
   */
  const applyStateFilter = useCallback((value: string) => {
    setState(value);
    setPage(1);
    setActiveTab("documents");
  }, []);

  const isTodayFilterActive = dateFrom === todayIso() && dateTo === todayIso();

  /** Indicador "Registrados hoy": aplica el rango de fecha de hoy y abre Documentos. */
  const applyTodayFilter = useCallback(() => {
    const today = todayIso();
    setDateFrom(today);
    setDateTo(today);
    setPage(1);
    setActiveTab("documents");
  }, []);

  /**
   * "Ver todos" de Documentos que requieren atención: muestra exactamente esa consulta (solo el
   * filtro de estados de atención, sin otros filtros previos).
   */
  const showAllAttention = useCallback(() => {
    setDateFrom("");
    setDateTo("");
    setDocumentType("");
    setEnvironment("");
    setSearch("");
    setState(ELECTRONIC_DOCUMENT_STATE_GROUPS.attention.value);
    setPage(1);
    setActiveTab("documents");
  }, []);

  return {
    canView,
    canViewDetail,
    canRetry,
    canReconcile,
    canRetransmitCorrected,
    canDownloadRide,
    sriPaused,
    activeTab,
    setActiveTab,
    filters: { dateFrom, dateTo, state, documentType, environment, search },
    setDateFrom,
    setDateTo,
    setState,
    setDocumentType,
    setEnvironment,
    setSearch,
    applyStateFilter,
    isTodayFilterActive,
    applyTodayFilter,
    showAllAttention,
    page,
    setPage,
    pageSize: PAGE_SIZE,
    list,
    listLoading,
    listError,
    dashboard,
    dashboardLoading,
    dashboardError,
    attention,
    attentionLoading,
    attentionError,
    selectedId,
    detail,
    detailLoading,
    detailError,
    xmlContent,
    xmlVariant,
    xmlLoading,
    xmlError,
    retryLoading,
    rideDownloading,
    refresh,
    openDetail,
    closeDetail,
    viewXml,
    retryNow,
    reconcile,
    retransmitCorrected,
    downloadRide,
    resetFiltersAndReload,
  };
}
