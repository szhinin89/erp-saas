import { useNavigate } from "react-router-dom";
import { LoadingState } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs";
import { useI18n } from "../../../i18n/i18n";
import type {
  BusinessPartnerStatusFilter,
  BusinessPartnerSummaryDto,
} from "../types/businessPartner.types";
import type { PartnerUiState } from "../store/masterDataPartnerUiStore";
import type { StoreApi, UseBoundStore } from "zustand";

type UiStore = UseBoundStore<StoreApi<PartnerUiState>>;
type Role = "customer" | "supplier";

export interface MasterDataPartnerListTabProps {
  role: Role;
  store: UiStore;
  canCreate: boolean;
  canUpdate: boolean;
  canDisable: boolean;
  canConfigure: boolean;
  loading: boolean;
  saving: boolean;
  partners: BusinessPartnerSummaryDto[];
  totalCount: number;
  search: string;
  setSearch: (v: string) => void;
  statusFilter: BusinessPartnerStatusFilter;
  setStatusFilter: (v: BusinessPartnerStatusFilter) => void;
  page: number;
  totalPages: number;
  setPage: (n: number) => void;
  searchInputRef?: React.RefObject<HTMLInputElement | null>;
  onSettings?: (bp: BusinessPartnerSummaryDto) => void;
  onSupplierProfile?: (bp: BusinessPartnerSummaryDto) => void;
  onCustomerConfig?: (bp: BusinessPartnerSummaryDto) => void;
  onSupplierClassification?: (bp: BusinessPartnerSummaryDto) => void;
  onAddAsSupplier?: (id: string) => void;
  onAddAsCustomer?: (id: string) => void;
  onActivate: (id: string) => void;
  onDisable: (id: string) => void;
}

export function MasterDataPartnerListTab({
  role,
  store,
  canUpdate,
  canDisable,
  canConfigure,
  loading,
  saving,
  partners,
  totalCount,
  search,
  setSearch,
  statusFilter,
  setStatusFilter,
  page,
  totalPages,
  setPage,
  searchInputRef,
  onSettings,
  onSupplierProfile,
  onCustomerConfig,
  onSupplierClassification,
  onAddAsSupplier,
  onAddAsCustomer,
  onActivate,
  onDisable,
}: MasterDataPartnerListTabProps) {
  const { t } = useI18n();
  const navigate = useNavigate();
  const startEdit = store((s) => s.startEdit);
  const prefix =
    role === "customer" ? "masterdata.customers" : "masterdata.suppliers";

  const handleClearSearch = () => {
    setSearch("");
    searchInputRef?.current?.focus();
  };

  return (
    <div className="prd-listado prd-fadein">
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
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>{t(`${prefix}.col.identification`, "Identificación")}</th>
                  <th>{t(`${prefix}.col.legalName`, "Razón social")}</th>
                  <th>{t("common.status", "Estado")}</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {partners.map((bp) => (
                  <tr
                    key={bp.id}
                    className={!bp.isActive ? "prd-row--inactive" : ""}
                  >
                    <td className="mono">{bp.identificationNumber}</td>
                    <td>
                      <span>{bp.tradeName?.trim() || bp.legalName}</span>
                      {bp.tradeName?.trim() && (
                        <span className="prd-sub-text">{bp.legalName}</span>
                      )}
                    </td>
                    <td>
                      <span
                        className={`prd-status-dot ${bp.isActive ? "prd-status-dot--active" : "prd-status-dot--inactive"}`}
                      >
                        <span className="prd-status-dot__bullet" />
                        {bp.isActive
                          ? t("common.active", "Activo")
                          : t("common.inactive", "Inactivo")}
                      </span>
                    </td>
                    <td className="md-actions prd-actions-cell">
                      <ZHBtn
                        variant="ghost"
                        size="sm"
                        onClick={() =>
                          navigate(`/masterdata/business-partners/${bp.id}`)
                        }
                      >
                        {t("common.view", "Ver")}
                      </ZHBtn>
                      {canUpdate && (
                        <ZHBtn
                          variant="ghost"
                          size="sm"
                          onClick={() => startEdit(bp)}
                        >
                          {t("common.edit", "Editar")}
                        </ZHBtn>
                      )}
                      {canConfigure && onSettings && (
                        <ZHBtn
                          variant="ghost"
                          size="sm"
                          onClick={() => void onSettings(bp)}
                        >
                          {t(`${prefix}.action.company`, "Condiciones")}
                        </ZHBtn>
                      )}
                      {role === "supplier" &&
                        canUpdate &&
                        onSupplierProfile && (
                          <ZHBtn
                            variant="ghost"
                            size="sm"
                            onClick={() => onSupplierProfile(bp)}
                          >
                            {t(`${prefix}.action.sri`, "Config SRI")}
                          </ZHBtn>
                        )}
                      {role === "customer" &&
                        canUpdate &&
                        onCustomerConfig && (
                          <ZHBtn
                            variant="ghost"
                            size="sm"
                            onClick={() => onCustomerConfig(bp)}
                          >
                            {t(`${prefix}.action.classification`, "Clasificación")}
                          </ZHBtn>
                        )}
                      {role === "supplier" &&
                        canUpdate &&
                        onSupplierClassification && (
                          <ZHBtn
                            variant="ghost"
                            size="sm"
                            onClick={() => onSupplierClassification(bp)}
                          >
                            {t(`${prefix}.action.classification`, "Clasificación")}
                          </ZHBtn>
                        )}
                      {canUpdate && !bp.isActive && (
                        <ZHBtn
                          variant="ghost"
                          size="sm"
                          disabled={saving}
                          onClick={() => void onActivate(bp.id)}
                        >
                          {role === "supplier"
                            ? t(
                                "masterdata.suppliers.action.activate",
                                "Activar proveedor",
                              )
                            : t("common.enable", "Activar")}
                        </ZHBtn>
                      )}
                      {role === "customer" && canUpdate && onAddAsSupplier && (
                        <ZHBtn
                          variant="ghost"
                          size="sm"
                          disabled={saving}
                          onClick={() => void onAddAsSupplier(bp.id)}
                          title={t(
                            `${prefix}.action.addSupplier`,
                            "Agregar como proveedor",
                          )}
                        >
                          +{t(`${prefix}.action.addSupplierShort`, "Prov.")}
                        </ZHBtn>
                      )}
                      {role === "supplier" && canUpdate && onAddAsCustomer && (
                        <ZHBtn
                          variant="ghost"
                          size="sm"
                          disabled={saving}
                          onClick={() => void onAddAsCustomer(bp.id)}
                          title={t(
                            `${prefix}.action.addCustomer`,
                            "Agregar como cliente",
                          )}
                        >
                          +{t(`${prefix}.action.addCustomerShort`, "Cli.")}
                        </ZHBtn>
                      )}
                      {canDisable && bp.isActive && (
                        <ZHBtn
                          variant="ghost"
                          size="sm"
                          disabled={saving}
                          onClick={() => void onDisable(bp.id)}
                        >
                          {role === "supplier"
                            ? t(
                                "masterdata.suppliers.action.deactivate",
                                "Desactivar proveedor",
                              )
                            : t("common.disable", "Desactivar")}
                        </ZHBtn>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

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
