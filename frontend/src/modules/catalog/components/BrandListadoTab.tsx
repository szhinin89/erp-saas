import { useCallback, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { useI18n } from '../../../i18n/i18n';
import type { BrandItem } from '../api/catalogService';
import { useBrandUiStore } from '../store/brandUiStore';
import { BrandConfirmModal } from './BrandConfirmModal';

interface Props {
  brands: BrandItem[];
  loading: boolean;
  canUpdate: boolean;
  canDelete: boolean;
  onToggle: (item: BrandItem) => Promise<void>;
  searchInputRef?: React.RefObject<HTMLInputElement | null>;
}

function highlight(text: string, query: string): React.ReactNode {
  if (!query.trim()) return text;
  const re = new RegExp(`(${query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')})`, 'gi');
  return text.split(re).map((p, i) =>
    re.test(p) ? <mark key={i} className="prd-highlight">{p}</mark> : p,
  );
}

export function BrandListadoTab({ brands, loading, canUpdate, canDelete, onToggle, searchInputRef }: Props) {
  const { t } = useI18n();
  const startEdit   = useBrandUiStore((s) => s.startEdit);
  const localRef    = useRef<HTMLInputElement>(null);
  const inputRef    = searchInputRef ?? localRef;
  const [query, setQuery]           = useState('');
  const [confirmItem, setConfirmItem] = useState<BrandItem | null>(null);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return brands;
    return brands.filter((b) =>
      `${b.code} ${b.name} ${b.manufacturer ?? ''} ${b.countryOfOrigin ?? ''}`.toLowerCase().includes(q),
    );
  }, [query, brands]);

  const handleClear = useCallback(() => {
    setQuery('');
    inputRef.current?.focus();
  }, [inputRef]);

  const handleKeyDown = useCallback((e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Escape') handleClear();
  }, [handleClear]);

  const handleConfirm = useCallback(async () => {
    if (!confirmItem) return;
    await onToggle(confirmItem);
    setConfirmItem(null);
  }, [confirmItem, onToggle]);

  return (
    <div className="cat-brand-listado prd-fadein">

      {/* Search bar */}
      <div className="prd-search-wrap">
        <div className="prd-search-box">
          <span className={`material-symbols-outlined prd-search-icon ${query ? 'prd-search-icon--active' : ''}`}>
            search
          </span>
          <input
            ref={inputRef as React.RefObject<HTMLInputElement>}
            type="search"
            className="prd-search-input"
            placeholder={t('brands.search.placeholder', 'Nombre, código, fabricante…')}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={handleKeyDown}
            aria-label={t('brands.search.placeholder', 'Buscar marcas')}
            autoComplete="off"
          />
          {query && (
            <button type="button" className="prd-search-clear" onClick={handleClear}
              aria-label={t('brands.search.clear', 'Limpiar')}>
              <span className="material-symbols-outlined" style={{ fontSize: 18 }}>close</span>
              {t('brands.search.clear', 'Limpiar')}
            </button>
          )}
        </div>
        <div className="prd-search-meta">
          <span>
            {t('brands.search.showing', 'Mostrando')} <strong>{filtered.length}</strong>{' '}
            {t('brands.search.of', 'de')} <strong>{brands.length}</strong>{' '}
            {t('brands.table.results', 'marcas')}
          </span>
          <span className="prd-search-shortcut"><kbd>Ctrl</kbd>+<kbd>K</kbd></span>
        </div>
      </div>

      {/* Content */}
      {loading ? (
        <p className="subtle pg-state-pad-24">{t('common.loading', 'Cargando...')}</p>
      ) : filtered.length === 0 ? (
        <div className="prd-empty-search prd-fadein">
          <span className="material-symbols-outlined prd-empty-search__icon">search_off</span>
          <p className="prd-empty-search__title">{t('brands.search.noResults.title', 'Sin resultados')}</p>
          {query ? (
            <>
              <p className="prd-empty-search__desc">
                {t('brands.search.noResults.desc', 'No se encontraron marcas para')}{' '}
                <strong>"{query}"</strong>
              </p>
              <button type="button" className="zh-btn zh-btn--ghost zh-btn--md" onClick={handleClear}>
                {t('brands.search.showAll', 'Mostrar todas')}
              </button>
            </>
          ) : (
            <p className="prd-empty-search__desc">{t('brands.search.noItems', 'No hay marcas registradas aún.')}</p>
          )}
        </div>
      ) : (
        <>
          <div className="prd-table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>{t('brands.table.brand', 'Marca')}</th>
                  <th>{t('brands.table.manufacturer', 'Fabricante')}</th>
                  <th>{t('brands.table.country', 'País de origen')}</th>
                  <th>{t('brands.table.status', 'Estado')}</th>
                  {(canUpdate || canDelete) && (
                    <th className="pg-th-right">{t('brands.table.actions', 'Acciones')}</th>
                  )}
                </tr>
              </thead>
              <tbody>
                {filtered.map((brand) => (
                  <tr key={brand.id} className={brand.isActive ? undefined : 'prd-row--inactive'}>
                    <td>
                      <div className="prd-product-cell">
                        <div className="zh-avatar zh-avatar--square prd-thumb" aria-hidden>
                          <span className="material-symbols-outlined prd-thumb-icon">sell</span>
                        </div>
                        <div>
                          <div className="prd-name">{highlight(brand.name, query)}</div>
                          <div className="subtle mono" style={{ fontSize: 11 }}>{brand.code}</div>
                        </div>
                      </div>
                    </td>
                    <td>
                      {brand.manufacturer
                        ? highlight(brand.manufacturer, query)
                        : <span className="subtle">—</span>}
                    </td>
                    <td>
                      {brand.countryOfOrigin ? (
                        <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                          <span className="material-symbols-outlined" style={{ fontSize: 14, color: 'var(--color-text-secondary)' }}>public</span>
                          <span style={{ fontSize: 13 }}>{highlight(brand.countryOfOrigin, query)}</span>
                        </div>
                      ) : <span className="subtle">—</span>}
                    </td>
                    <td>
                      <span className={`prd-status-dot ${brand.isActive ? 'prd-status-dot--active' : 'prd-status-dot--inactive'}`}>
                        <span className="prd-status-dot__bullet" aria-hidden />
                        {brand.isActive ? t('common.active', 'Activo') : t('common.inactive', 'Inactivo')}
                      </span>
                    </td>
                    {(canUpdate || canDelete) && (
                      <td className="pg-th-right">
                        <div className="prd-actions-cell">
                          {canUpdate && (
                            <button
                              type="button"
                              className="zh-btn zh-btn--ghost zh-btn--sm"
                              title={t('common.edit', 'Editar')}
                              disabled={!brand.isActive}
                              onClick={() => startEdit(brand)}
                              aria-label={`${t('common.edit', 'Editar')} ${brand.name}`}
                            >
                              <span className="material-symbols-outlined">edit</span>
                            </button>
                          )}
                          {(brand.isActive ? canDelete : canUpdate) && (
                            <button
                              type="button"
                              className={`zh-btn zh-btn--ghost zh-btn--sm ${brand.isActive ? 'prd-btn-mute' : 'prd-btn-activate'}`}
                              title={brand.isActive ? t('common.disable', 'Desactivar') : t('common.enable', 'Activar')}
                              onClick={() => setConfirmItem(brand)}
                              aria-label={`${brand.isActive ? t('common.disable') : t('common.enable')} ${brand.name}`}
                            >
                              <span className="material-symbols-outlined">
                                {brand.isActive ? 'visibility_off' : 'visibility'}
                              </span>
                            </button>
                          )}
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="pg-table-footer">
            <p className="subtle prd-footer-count">
              {filtered.length} {t('brands.table.results', 'marcas')}
            </p>
            <p className="pg-table-timestamp">{t('products.list.lastUpdated', 'Actualizado en esta sesión')}</p>
          </div>
        </>
      )}

      <BrandConfirmModal
        open={!!confirmItem}
        title={confirmItem?.isActive
          ? t('brands.toggle.disable.title', 'Desactivar marca')
          : t('brands.toggle.activate.title', 'Activar marca')}
        message={confirmItem?.isActive
          ? <>{t('brands.toggle.disable.warning', 'Los productos asociados podrían verse afectados.')} <strong>{confirmItem?.name}</strong></>
          : <>¿Deseas activar la marca <strong>{confirmItem?.name}</strong>?</>}
        confirmLabel={confirmItem?.isActive
          ? t('brands.toggle.disable.confirm', 'Sí, desactivar')
          : t('brands.toggle.activate.confirm', 'Sí, activar')}
        cancelLabel={t('common.cancel', 'Cancelar')}
        variant={confirmItem?.isActive ? 'danger' : 'default'}
        onConfirm={handleConfirm}
        onCancel={() => setConfirmItem(null)}
      />
    </div>
  );
}
