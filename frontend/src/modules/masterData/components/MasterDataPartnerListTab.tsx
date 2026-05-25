import { useNavigate } from 'react-router-dom';
import { LoadingState } from '../../../components/PageShell';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { useI18n } from '../../../i18n/i18n';
import type { BusinessPartnerDto } from '../types/businessPartner.types';
import type { PartnerUiState } from '../store/masterDataPartnerUiStore';
import type { StoreApi, UseBoundStore } from 'zustand';

type UiStore = UseBoundStore<StoreApi<PartnerUiState>>;

type Role = 'customer' | 'supplier';

export interface MasterDataPartnerListTabProps {
  role: Role;
  store: UiStore;
  canCreate: boolean;
  canUpdate: boolean;
  canDisable: boolean;
  canConfigure: boolean;
  loading: boolean;
  saving: boolean;
  partners: BusinessPartnerDto[];
  totalCount: number;
  search: string;
  setSearch: (v: string) => void;
  showInactive: boolean;
  setShowInactive: (v: boolean) => void;
  page: number;
  totalPages: number;
  setPage: (n: number) => void;
  searchInputRef?: React.RefObject<HTMLInputElement | null>;
  onSettings?: (bp: BusinessPartnerDto) => void;
  onNotes?: (bp: BusinessPartnerDto) => void;
  onSupplierProfile?: (bp: BusinessPartnerDto) => void;
  onAddAsSupplier?: (id: string) => void;
  onAddAsCustomer?: (id: string) => void;
  onActivate: (id: string) => void;
  onDisable: (id: string) => void;
}

export function MasterDataPartnerListTab({
  role,
  store,
  canCreate: _canCreate,
  canUpdate,
  canDisable,
  canConfigure,
  loading,
  saving,
  partners,
  totalCount,
  search,
  setSearch,
  showInactive,
  setShowInactive,
  page,
  totalPages,
  setPage,
  searchInputRef,
  onSettings,
  onNotes,
  onSupplierProfile,
  onAddAsSupplier,
  onAddAsCustomer,
  onActivate,
  onDisable,
}: MasterDataPartnerListTabProps) {
  const { t } = useI18n();
  const navigate = useNavigate();
  const startEdit = store((s) => s.startEdit);

  const prefix = role === 'customer' ? 'masterdata.customers' : 'masterdata.suppliers';

  const handleClearSearch = () => {
    setSearch('');
    searchInputRef?.current?.focus();
  };

  return (
    <div className="prd-listado prd-fadein">
      <div className="prd-search-wrap">
        <div className="prd-search-box">
          <span className={`material-symbols-outlined prd-search-icon ${search ? 'prd-search-icon--active' : ''}`}>
            search
          </span>
          <input
            ref={searchInputRef as React.RefObject<HTMLInputElement>}
            type="search"
            className="prd-search-input"
            placeholder={t(`${prefix}.list.searchPlaceholder`)}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Escape') handleClearSearch();
            }}
            aria-label={t(`${prefix}.list.searchAria`, 'Buscar')}
            autoComplete="off"
          />
          {search && (
            <button
              type="button"
              className="prd-search-clear"
              onClick={handleClearSearch}
              aria-label={t('masterdata.list.clearSearch', 'Limpiar búsqueda')}
            >
              <span className="material-symbols-outlined" style={{ fontSize: 18 }}>close</span>
              {t('masterdata.list.clear', 'Limpiar')}
            </button>
          )}
        </div>
        <div className="prd-search-meta">
          <span>
            {t('masterdata.list.showing', 'Mostrando')}{' '}
            <strong>{partners.length}</strong> {t('masterdata.list.of', 'de')}{' '}
            <strong>{totalCount}</strong> {t(`${prefix}.list.entityLabel`)}
          </span>
          <label className="md-page-check prd-search-meta__filter">
            <input
              type="checkbox"
              checked={showInactive}
              onChange={(e) => setShowInactive(e.target.checked)}
            />
            {t(`${prefix}.list.includeInactive`)}
          </label>
        </div>
      </div>

      {loading ? (
        <div className="pg-pad-40"><LoadingState /></div>
      ) : partners.length === 0 ? (
        <div className="prd-empty-search prd-fadein">
          <span className="material-symbols-outlined prd-empty-search__icon">search_off</span>
          <p className="prd-empty-search__title">{t('masterdata.list.empty.title', 'Sin resultados')}</p>
          <p className="prd-empty-search__desc">
            {search
              ? t('masterdata.list.empty.withQuery', 'No hay registros para tu búsqueda.')
              : t('common.noData')}
          </p>
        </div>
      ) : (
        <>
          <div className="md-table-wrap prd-table-wrap">
            <table className="md-table">
              <thead>
                <tr>
                  <th>{t(`${prefix}.col.identification`)}</th>
                  <th>{t(`${prefix}.col.legalName`)}</th>
                  {role === 'customer' && <th>{t(`${prefix}.col.legacyLink`)}</th>}
                  <th>{t('common.status')}</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {partners.map((bp) => (
                  <tr key={bp.id} className={!bp.isActive ? 'prd-row--inactive' : ''}>
                    <td className="mono">{bp.identificationNumber}</td>
                    <td>{bp.tradeName?.trim() || bp.legalName}</td>
                    {role === 'customer' && (
                      <td>
                        {bp.legacyCustomerId ? (
                          <span className="md-badge md-badge--ok">{t(`${prefix}.legacyLinked`)}</span>
                        ) : (
                          <span className="md-badge md-badge--warn" title={t(`${prefix}.legacyMissingHint`)}>
                            {t(`${prefix}.legacyMissing`)}
                          </span>
                        )}
                      </td>
                    )}
                    <td>
                      <span className={`prd-status-dot ${bp.isActive ? 'prd-status-dot--active' : 'prd-status-dot--inactive'}`}>
                        <span className="prd-status-dot__bullet" />
                        {bp.isActive ? t('common.active') : t('common.inactive')}
                      </span>
                    </td>
                    <td className="md-actions prd-actions-cell">
                      <ZHBtn variant="ghost" size="sm" onClick={() => navigate(`/masterdata/business-partners/${bp.id}`)}>
                        {t('common.view')}
                      </ZHBtn>
                      {canUpdate && (
                        <ZHBtn variant="ghost" size="sm" onClick={() => startEdit(bp)}>
                          {t('common.edit')}
                        </ZHBtn>
                      )}
                      {role === 'customer' && canUpdate && onNotes && (
                        <ZHBtn variant="ghost" size="sm" onClick={() => onNotes(bp)}>
                          {t(`${prefix}.action.notes`)}
                        </ZHBtn>
                      )}
                      {canConfigure && onSettings && (
                        <ZHBtn variant="ghost" size="sm" onClick={() => void onSettings(bp)}>
                          {t(`${prefix}.action.company`)}
                        </ZHBtn>
                      )}
                      {role === 'supplier' && canUpdate && onSupplierProfile && (
                        <ZHBtn variant="ghost" size="sm" onClick={() => onSupplierProfile(bp)}>
                          {t(`${prefix}.action.sri`)}
                        </ZHBtn>
                      )}
                      {canUpdate && !bp.isActive && (
                        <ZHBtn variant="ghost" size="sm" disabled={saving} onClick={() => void onActivate(bp.id)}>
                          {t('common.enable')}
                        </ZHBtn>
                      )}
                      {role === 'customer' && canUpdate && onAddAsSupplier && !bp.isSupplier && (
                        <ZHBtn
                          variant="ghost"
                          size="sm"
                          disabled={saving}
                          onClick={() => void onAddAsSupplier(bp.id)}
                          title={t(`${prefix}.action.addSupplier`)}
                        >
                          {t(`${prefix}.action.addSupplierShort`)}
                        </ZHBtn>
                      )}
                      {role === 'supplier' && canUpdate && onAddAsCustomer && !bp.isCustomer && (
                        <ZHBtn
                          variant="ghost"
                          size="sm"
                          disabled={saving}
                          onClick={() => void onAddAsCustomer(bp.id)}
                          title={t(`${prefix}.action.addCustomer`)}
                        >
                          {t(`${prefix}.action.addCustomerShort`)}
                        </ZHBtn>
                      )}
                      {canDisable && bp.isActive && (
                        <ZHBtn variant="ghost" size="sm" disabled={saving} onClick={() => void onDisable(bp.id)}>
                          {t('common.disable')}
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
              <ZHBtn variant="ghost" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                ‹ {t('common.prev')}
              </ZHBtn>
              <span className="md-pagination-info">
                {t('masterdata.common.pagination', {
                  page: String(page),
                  total: String(totalPages),
                  count: String(totalCount),
                })}
              </span>
              <ZHBtn variant="ghost" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
                {t('common.next')} ›
              </ZHBtn>
            </div>
          )}
        </>
      )}
    </div>
  );
}
