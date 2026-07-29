import { useCallback, useEffect, useState } from "react";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { formatApiError } from "../../../lib/formatApiError";
import { todayIso } from "../../../../lib/formatters/dateFormatters";
import {
  electronicDocumentsMonitorService,
  type ElectronicDocumentDetailDto,
  type ElectronicDocumentsDashboardDto,
  type ElectronicDocumentsListResponse,
  type ElectronicDocumentXmlVariant,
} from "../api/electronicDocumentsMonitorService";

const PAGE_SIZE = 25;

export function useElectronicDocumentsMonitor() {
  const { canShow } = usePermissionsUi();
  const canView = canShow("electronic-documents.view");
  const canViewDetail = canShow("electronic-documents.detail");
  const canRetry = canShow("electronic-documents.retry");

  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [state, setState] = useState("");
  const [documentType, setDocumentType] = useState("");
  const [environment, setEnvironment] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  const [list, setList] = useState<ElectronicDocumentsListResponse | null>(
    null,
  );
  const [listLoading, setListLoading] = useState(true);
  const [listError, setListError] = useState<string | null>(null);

  const [dashboard, setDashboard] =
    useState<ElectronicDocumentsDashboardDto | null>(null);
  const [dashboardLoading, setDashboardLoading] = useState(true);
  const [dashboardError, setDashboardError] = useState<string | null>(null);

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

  const loadList = useCallback(async () => {
    setListLoading(true);
    setListError(null);
    try {
      const data = await electronicDocumentsMonitorService.getList({
        dateFrom: dateFrom || undefined,
        dateTo: dateTo || undefined,
        state: state || undefined,
        documentType: documentType || undefined,
        environment: environment || undefined,
        search: search || undefined,
        pageNumber: page,
        pageSize: PAGE_SIZE,
      });
      setList(data);
    } catch (e) {
      setListError(formatApiError(e));
      setList(null);
    } finally {
      setListLoading(false);
    }
  }, [dateFrom, dateTo, state, documentType, environment, search, page]);

  const loadDashboard = useCallback(async () => {
    setDashboardLoading(true);
    setDashboardError(null);
    try {
      const data = await electronicDocumentsMonitorService.getDashboard();
      setDashboard(data);
    } catch (e) {
      setDashboardError(formatApiError(e));
      setDashboard(null);
    } finally {
      setDashboardLoading(false);
    }
  }, []);

  useEffect(() => {
    if (canView) void loadList();
  }, [canView, loadList]);

  useEffect(() => {
    if (canView) void loadDashboard();
  }, [canView, loadDashboard]);

  const refresh = useCallback(() => {
    void loadList();
    void loadDashboard();
  }, [loadList, loadDashboard]);

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
      void loadList();
      void loadDashboard();
    } finally {
      setRetryLoading(false);
    }
  }, [detail, loadList, loadDashboard]);

  const resetFiltersAndReload = useCallback(() => {
    setDateFrom("");
    setDateTo("");
    setState("");
    setDocumentType("");
    setEnvironment("");
    setSearch("");
    setPage(1);
  }, []);

  /** Usado por las tarjetas KPI: aplica (o quita, si ya estaba activo) un filtro de estado y vuelve a la página 1. */
  const toggleStateFilter = useCallback((value: string) => {
    setState((current) => (current === value ? "" : value));
    setPage(1);
  }, []);

  const isTodayFilterActive = dateFrom === todayIso() && dateTo === todayIso();

  /** Usado por la tarjeta "Total emitidos hoy": aplica (o quita) el rango de fecha de hoy. */
  const toggleTodayFilter = useCallback(() => {
    const today = todayIso();
    setDateFrom((current) => (current === today ? "" : today));
    setDateTo((current) => (current === today ? "" : today));
    setPage(1);
  }, []);

  return {
    canView,
    canViewDetail,
    canRetry,
    filters: { dateFrom, dateTo, state, documentType, environment, search },
    setDateFrom,
    setDateTo,
    setState,
    setDocumentType,
    setEnvironment,
    setSearch,
    toggleStateFilter,
    isTodayFilterActive,
    toggleTodayFilter,
    page,
    setPage,
    pageSize: PAGE_SIZE,
    list,
    listLoading,
    listError,
    dashboard,
    dashboardLoading,
    dashboardError,
    selectedId,
    detail,
    detailLoading,
    detailError,
    xmlContent,
    xmlVariant,
    xmlLoading,
    xmlError,
    retryLoading,
    refresh,
    openDetail,
    closeDetail,
    viewXml,
    retryNow,
    resetFiltersAndReload,
  };
}
