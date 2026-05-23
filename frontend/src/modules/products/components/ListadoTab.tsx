import {
  useCallback, useMemo, useRef, useState,
  type KeyboardEvent,
} from 'react';
import { useI18n } from '../../../i18n/i18n';
import type { Product } from '../../../types/product';
import type { ProductCatalogs } from '../hooks/useProducts';
import { useProductUiStore } from '../store/productUiStore';
import { ConfirmModal } from './ConfirmModal';
import { LoadingState } from '../../../components/PageShell';

interface Props {
  products: Product[];
  catalogs: ProductCatalogs | null | undefined;
  loading: boolean;
  toggling: boolean;
  onToggle: (id: string, enable: boolean) => Promise<void>;
  searchInputRef?: React.RefObject<HTMLInputElement | null>;
}

/** Wrap matched segments in <mark> */
function highlight(text: string, query: string): React.ReactNode {
  if (!query.trim()) return text;
  const re = new RegExp(`(${query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')})`, 'gi');
  const parts = text.split(re);
  return parts.map((p, i) =>
    re.test(p) ? <mark key={i} className="prd-highlight">{p}</mark> : p
  );
}

interface ToggleTarget {
  id: string;
  name: string;
  enable: boolean;
}

export function ListadoTab({
  products,
  catalogs,
  loading,
  toggling,
  onToggle,
  searchInputRef,
}: Props) {
  const { t } = useI18n();
  const startEdit   = useProductUiStore((s) => s.startEdit);
  const [query, setQuery] = useState('');
  const [confirmTarget, setConfirmTarget] = useState<ToggleTarget | null>(null);
  const localRef = useRef<HTMLInputElement>(null);
  const inputRef = searchInputRef ?? localRef;

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return products;
    return products.filter((p) =>
      `${p.saleCode} ${p.shortName} ${p.description}`.toLowerCase().includes(q)
    );
  }, [query, products]);

  const handleClearSearch = useCallback(() => {
    setQuery('');
    inputRef.current?.focus();
  }, [inputRef]);

  const handleKeyDown = useCallback((e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Escape') handleClearSearch();
  }, [handleClearSearch]);

  const handleToggleClick = useCallback((p: Product) => {
    setConfirmTarget({ id: p.id, name: p.shortName, enable: !p.isActive });
  }, []);

  const handleConfirmToggle = useCallback(async () => {
    if (!confirmTarget) return;
    await onToggle(confirmTarget.id, confirmTarget.enable);
    setConfirmTarget(null);
  }, [confirmTarget, onToggle]);

  const categoryName = useCallback(
    (id: string) => catalogs?.categories.find((c) => c.id === id)?.name ?? '—',
    [catalogs]
  );

  return (
    <div className="prd-listado prd-fadein">

      {/* ── Search bar ─────────────────────────────────────── */}
      <div className="prd-search-wrap">
        <div className="prd-search-box">
          <span className={`material-symbols-outlined prd-search-icon ${query ? 'prd-search-icon--active' : ''}`}>
            search
          </span>
          <input
            ref={inputRef as React.RefObject<HTMLInputElement>}
            type="search"
            className="prd-search-input"
            placeholder={t('products.search.placeholder', 'Buscar por SKU, nombre o categoría...')}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={handleKeyDown}
            aria-label="Buscar productos"
            autoComplete="off"
          />
          {query && (
            <button
              type="button"
              className="prd-search-clear"
              onClick={handleClearSearch}
              aria-label="Limpiar búsqueda"
            >
              <span className="material-symbols-outlined" style={{ fontSize: 18 }}>close</span>
              {t('products.search.clear', 'Limpiar')}
            </button>
          )}
        </div>
        <div className="prd-search-meta">
          <span>
            {t('products.search.showing', 'Mostrando')} <strong>{filtered.length}</strong> {t('products.search.of', 'de')} <strong>{products.length}</strong> {t('products.list.results', 'resultados')}
          </span>
          <span className="prd-search-shortcut">
            <kbd>Ctrl</kbd>+<kbd>K</kbd>
          </span>
        </div>
      </div>

      {/* ── Table ──────────────────────────────────────────── */}
      {loading ? (
        <div className="pg-pad-40"><LoadingState /></div>
      ) : filtered.length === 0 ? (
        <div className="prd-empty-search prd-fadein">
          <span className="material-symbols-outlined prd-empty-search__icon">search_off</span>
          <p className="prd-empty-search__title">{t('products.search.noResults.title', 'Sin resultados')}</p>
          {query ? (
            <>
              <p className="prd-empty-search__desc">
                {t('products.search.noResults.desc', 'No se encontraron productos para')} <strong>"{query}"</strong>
              </p>
              <button
                type="button"
                className="zh-btn zh-btn--ghost zh-btn--md"
                onClick={handleClearSearch}
              >
                {t('products.search.showAll', 'Mostrar todos')}
              </button>
            </>
          ) : (
            <p className="prd-empty-search__desc">{t('products.search.noProducts', 'No hay productos registrados aún.')}</p>
          )}
        </div>
      ) : (
        <>
          <div className="prd-table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>SKU</th>
                  <th>Producto</th>
                  <th>Categoría</th>
                  <th>Estado</th>
                  <th>Creado</th>
                  <th className="pg-th-right">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((p) => (
                  <tr
                    key={p.id}
                    className={p.isActive ? undefined : 'prd-row--inactive'}
                  >
                    <td>
                      <span className="mono prd-sku">{highlight(p.saleCode, query)}</span>
                    </td>
                    <td>
                      <div className="prd-product-cell">
                        <div className="zh-avatar zh-avatar--square prd-thumb" aria-hidden>
                          <span className="material-symbols-outlined prd-thumb-icon">inventory_2</span>
                        </div>
                        <div className="prd-product-info">
                          <div className="prd-name">{highlight(p.shortName, query)}</div>
                          <div className="subtle prd-desc-subtle">{highlight(p.description, query)}</div>
                        </div>
                      </div>
                    </td>
                    <td>
                      <span className="badge badge--gray">{categoryName(p.categoryId)}</span>
                    </td>
                    <td>
                      <span className={`prd-status-dot ${p.isActive ? 'prd-status-dot--active' : 'prd-status-dot--inactive'}`}>
                        <span className="prd-status-dot__bullet" aria-hidden />
                        {p.isActive ? 'Activo' : 'Inactivo'}
                      </span>
                    </td>
                    <td className="subtle mono prd-date-cell">
                      {new Date(p.createdAt).toLocaleDateString()}
                    </td>
                    <td className="pg-th-right">
                      <div className="prd-actions-cell">
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          title="Editar"
                          disabled={toggling || !p.isActive}
                          onClick={() => startEdit(p)}
                          aria-label={`Editar ${p.shortName}`}
                        >
                          <span className="material-symbols-outlined">edit</span>
                        </button>
                        <button
                          type="button"
                          className={`zh-btn zh-btn--ghost zh-btn--sm ${
                            p.isActive ? 'prd-btn-mute' : 'prd-btn-activate'
                          }`}
                          title={p.isActive ? 'Desactivar' : 'Activar'}
                          disabled={toggling}
                          onClick={() => handleToggleClick(p)}
                          aria-label={p.isActive ? `Desactivar ${p.shortName}` : `Activar ${p.shortName}`}
                        >
                          <span className="material-symbols-outlined">
                            {p.isActive ? 'visibility_off' : 'visibility'}
                          </span>
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="pg-table-footer">
            <p className="subtle prd-footer-count">
              {filtered.length} resultados
            </p>
            <p className="pg-table-timestamp">Actualizado en esta sesión</p>
          </div>
        </>
      )}

      {/* ── Confirm toggle modal ──────────────────────────── */}
      <ConfirmModal
        open={!!confirmTarget}
        title={confirmTarget?.enable
          ? t('products.toggle.activate.title', 'Activar producto')
          : t('products.toggle.disable.title', 'Desactivar producto')}
        message={
          confirmTarget?.enable
            ? <>¿Deseas activar el producto <strong>{confirmTarget.name}</strong>?</>
            : <>¿Deseas desactivar el producto <strong>{confirmTarget?.name}</strong>? {t('products.toggle.disable.warning', 'Dejará de aparecer en el catálogo de ventas.')}</>
        }
        confirmLabel={confirmTarget?.enable
          ? t('products.toggle.activate.confirm', 'Sí, activar')
          : t('products.toggle.disable.confirm', 'Sí, desactivar')}
        variant={confirmTarget?.enable ? 'default' : 'danger'}
        onConfirm={handleConfirmToggle}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  );
}
