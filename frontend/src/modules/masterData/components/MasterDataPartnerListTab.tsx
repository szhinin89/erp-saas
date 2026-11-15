import { MasterDataCustomerListActions } from "./MasterDataCustomerListActions";
import { MasterDataSupplierListActions } from "./MasterDataSupplierListActions";
import { LoadingState } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { useI18n } from "../../../i18n/i18n";
import { useSriIdTypes } from "../api/useSriIdTypes";
import type {
  BusinessPartnerStatusFilter,
  BusinessPartnerSummaryDto,
} from "../types/businessPartner.types";
type Role = "customer" | "supplier";

export interface MasterDataPartnerListTabProps {
  role: Role;
  canCreate: boolean;
  canUpdate: boolean;
  canDisable: boolean;
  loading: boolean;
  saving: boolean;
  partners: BusinessPartnerSummaryDto[];
  totalCount: number;
  search: string;
  setSearch: (v: string) => void;
  statusFilter: BusinessPartnerStatusFilter;
  setStatusFilter: (v: BusinessPartnerStatusFilter) => void;
  page: number;
  pageSize: number;
  totalPages: number;
  setPage: (n: number) => void;
  searchInputRef?: React.RefObject<HTMLInputElement | null>;
  onAddAsSupplier?: (id: string) => void;
  onAddAsCustomer?: (id: string) => void;
  onRevokeRole?: (id: string) => void;
  onActivate: (id: string) => void;
  onDisable: (id: string) => void;
}

export function MasterDataPartnerListTab({
  role,
  canUpdate,
  canDisable,
  loading,
  saving,
  partners,
  totalCount,
  search,
  setSearch,
  statusFilter,
  setStatusFilter,
  page,
  pageSize,
  totalPages,
  setPage,
  searchInputRef,
  onAddAsSupplier,
  onAddAsCustomer,
  onRevokeRole,
  onActivate,
  onDisable,
}: MasterDataPartnerListTabProps) {
  const { t } = useI18n();
  const { options: idTypes } = useSriIdTypes();
  // Abreviatura oficial (catálogo sri_id_type) para el espacio compacto; sin catálogo, solo el número.
  const formatIdentification = (bp: BusinessPartnerSummaryDto) => {
    const shortName = idTypes.find((o) => o.code === bp.identificationType)?.shortName;
    return shortName ? `${shortName} ${bp.identificationNumber}` : bp.identificationNumber;
  };
  const prefix =
    role === "customer" ? "masterdata.customers" : "masterdata.suppliers";

  const handleClearSearch = () => {
    setSearch("");
    searchInputRef?.current?.focus();
  };

  const partnerColumns: ZHDataTableColumn<BusinessPartnerSummaryDto>[] = [
    { key: "identification", header: t(`${prefix}.col.identification`, "Identificación"), render: (bp) => <span className="mono">{formatIdentification(bp)}</span>,
      cellClassName: role === "customer" ? "md-customer-col-id" : undefined },
    {
      key: "legalName",
      header: t(`${prefix}.col.legalName`, "Razón social"),
      render: (bp) => role === "supplier" ? (
        <div className="md-supplier-identity">
          <span className="md-supplier-identity__id mono">{formatIdentification(bp)}</span>
          <span className="md-supplier-identity__legal">{bp.legalName}</span>
          {bp.tradeName?.trim() && bp.tradeName.trim() !== bp.legalName.trim() && (
            <span className="md-supplier-identity__trade">{bp.tradeName.trim()}</span>
          )}
          <span className={`md-supplier-identity__status prd-status-dot ${bp.isActive ? "prd-status-dot--active" : "prd-status-dot--inactive"}`}>
            <span className="prd-status-dot__bullet" />
            {bp.isActive ? t("common.active") : t("common.inactive")}
          </span>
        </div>
      ) : (
        <div className="md-customer-identity">
          <span className="md-customer-identity__name">{bp.tradeName?.trim() || bp.legalName}</span>
          {bp.tradeName?.trim() && <span className="md-customer-identity__legal prd-sub-text">{bp.legalName}</span>}
          <span className="md-customer-identity__id mono">{formatIdentification(bp)}</span>
          <span className={`md-customer-identity__status prd-status-dot ${bp.isActive ? "prd-status-dot--active" : "prd-status-dot--inactive"}`}>
            <span className="prd-status-dot__bullet" />
            {bp.isActive ? t("common.active") : t("common.inactive")}
          </span>
        </div>
      ),
      cellClassName: role === "customer" ? "md-customer-col-identity" : undefined,
    },
    {
      key: "status",
      header: t("common.status", "Estado"),
      render: (bp) => (
        <span className={`prd-status-dot ${bp.isActive ? "prd-status-dot--active" : "prd-status-dot--inactive"}`}>
          <span className="prd-status-dot__bullet" />
          {bp.isActive ? t("common.active", "Activo") : t("common.inactive", "Inactivo")}
        </span>
      ),
      cellClassName: role === "customer" ? "md-customer-col-status" : undefined,
    },
    {
      key: "actions",
      header: "",
      render: (bp) => role === "supplier" ? (
        <MasterDataSupplierListActions partner={bp} canUpdate={canUpdate} canDisable={canDisable}
          saving={saving} onAddAsCustomer={onAddAsCustomer} onRevokeRole={onRevokeRole} onActivate={onActivate} onDisable={onDisable} />
      ) : (
        <MasterDataCustomerListActions partner={bp} canUpdate={canUpdate} canDisable={canDisable}
          saving={saving} onAddAsSupplier={onAddAsSupplier} onRevokeRole={onRevokeRole} onActivate={onActivate} onDisable={onDisable} />
      ),
      cellClassName: role === "customer" ? "md-customer-col-actions" : undefined,
    },
  ];

  return (
    <div className={`prd-listado prd-fadein ${role === "supplier" ? "md-supplier-list" : "md-customer-list"}`}>
      {/* ── Search bar ──────────────────────────────────────────────────── */}
      <div className="prd-search-wrap">
        <div className="prd-search-box">
          <span
            className={`material-symbols-outlined prd-search-icon ${search ? "prd-search-icon--active" : ""}`}
          >
            search
          </span>
          <input
            ref={searchInputRef as React.RefObject<HTMLInputElement>}
            type="search"
            className="prd-search-input"
            placeholder={t(
              `${prefix}.list.searchPlaceholder`,
              "Buscar por RUC, cédula o nombre…",
            )}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Escape") handleClearSearch();
            }}
            autoComplete="off"
          />
          {search && (
            <button
              type="button"
              className="prd-search-clear"
              onClick={handleClearSearch}
            >
              <span className="material-symbols-outlined zh-icon-lg">
                close
              </span>
              {t("masterdata.list.clear", "Limpiar")}
            </button>
          )}
        </div>
        <div className="prd-search-meta">
          <span>
            {t("masterdata.list.showing", "Mostrando")}{" "}
            <strong>{partners.length}</strong> {t("masterdata.list.of", "de")}{" "}
            <strong>{totalCount}</strong>
          </span>
          <label className="md-status-filter">
            <span className="md-status-filter__label">
              {t("masterdata.list.statusFilter", "Estado")}
            </span>
            <ZhSelect
              value={statusFilter}
              onChange={(e) =>
                setStatusFilter(e.target.value as BusinessPartnerStatusFilter)
              }
              aria-label={t("masterdata.list.statusFilter", "Estado")}
            >
              <option value="all">
                {t("masterdata.list.status.all", "Todos")}
              </option>
              <option value="active">
                {t("masterdata.list.status.active", "Activos")}
              </option>
              <option value="inactive">
                {t("masterdata.list.status.inactive", "Inactivos")}
              </option>
            </ZhSelect>
          </label>
        </div>
      </div>

      {/* ── Table ───────────────────────────────────────────────────────── */}
      {loading ? (
        <div className="pg-pad-40">
          <LoadingState />
        </div>
      ) : partners.length === 0 ? (
        <div className="prd-empty-search prd-fadein">
          <span className="material-symbols-outlined prd-empty-search__icon">
            search_off
          </span>
          <p className="prd-empty-search__title">
            {t("masterdata.list.empty.title", "Sin resultados")}
          </p>
          <p className="prd-empty-search__desc">
            {search
              ? t(
                  "masterdata.list.empty.withQuery",
                  "No hay registros para tu búsqueda.",
                )
              : t("common.noData", "Sin datos.")}
          </p>
        </div>
      ) : (
        <>
          <ZHDataTable
            columns={partnerColumns}
            rows={partners}
            rowKey={(bp) => bp.id}
            showRowNumber
            rowNumberOffset={(page - 1) * pageSize}
            rowClassName={(bp) => (!bp.isActive ? "prd-row--inactive" : undefined)}
          />

          {totalPages > 1 && (
            <div className="md-pagination">
              <ZHBtn
                variant="ghost"
                size="sm"
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
              >
                ‹ {t("common.prev", "Anterior")}
              </ZHBtn>
              <span className="md-pagination-info">
                {t("masterdata.common.pagination", {
                  page: String(page),
                  total: String(totalPages),
                  count: String(totalCount),
                })}
              </span>
              <ZHBtn
                variant="ghost"
                size="sm"
                disabled={page >= totalPages}
                onClick={() => setPage(page + 1)}
              >
                {t("common.next", "Siguiente")} ›
              </ZHBtn>
            </div>
          )}
        </>
      )}
    </div>
  );
}
