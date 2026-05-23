import { useCallback, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { useI18n } from '../../../../i18n/i18n';
import { LoadingState } from '../../../../components/PageShell';
import type { WarehouseDto } from '../api/warehouseService';
import { WarehouseConfirmModal } from './WarehouseConfirmModal';

interface Props {
  warehouses: WarehouseDto[];
  loading: boolean;
  toggling: boolean;
  canUpdate: boolean;
  canDelete: boolean;
  branchName: (id: string) => string;
  onEdit: (row: WarehouseDto) => Promise<void>;
  onToggle: (row: WarehouseDto) => Promise<void>;
  searchInputRef?: React.RefObject<HTMLInputElement | null>;
}

/** Highlight matching segments */
function highlight(text: string, query: string): React.ReactNode {
  if (!query.trim()) return text;
  const re = new RegExp(`(${query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')})`, 'gi');
  const parts = text.split(re);
  return parts.map((p, i) =>
    re.test(p) ? <mark key={i} className="prd-highlight">{p}</mark> : p,
  );
}

export function WarehouseListadoTab({
  warehouses,
  loading,
  toggling,
  canUpdate,
  canDelete,
  branchName,
  onEdit,
  onToggle,
  searchInputRef,
}: Props) {
  const { t } = useI18n();
  const localRef     = useRef<HTMLInputElement>(null);
  const inputRef     = searchInputRef ?? localRef;
  const [query, setQuery]             = useState('');
  const [confirmRow, setConfirmRow]   = useState<WarehouseDto | null>(null);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return warehouses;
    return warehouses.filter((w) =>
      `${w.name} ${w.code ?? ''} ${w.manager ?? ''} ${w.address ?? ''}`.toLowerCase().includes(q),
    );
  }, [query, warehouses]);

  const handleClear = useCallback(() => {
    setQuery('');
    inputRef.current?.focus();
  }, [inputRef]);

  const handleKeyDown = useCallback((e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Escape') handleClear();
  }, [handleClear]);

  const handleToggleClick = useCallback((row: WarehouseDto) => {
    setConfirmRow(row);
  }, []);

  const handleConfirmToggle = useCallback(async () => {
    if (!confirmRow) return;
    await onToggle(confirmRow);
    setConfirmRow(null);
  }, [confirmRow, onToggle]);

  return (
    <div className="bod-listado prd-fadein">

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
            placeholder={t('warehouses.search.placeholder', 'Buscar por nombre, código o encargado...')}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={handleKeyDown}
            aria-label={t('warehouses.search.placeholder', 'Buscar bodegas')}
            autoComplete="off"
          />
          {query && (
            <button
              type="button"
              className="prd-search-clear"
              onClick={handleClear}
              aria-label={t('warehouses.search.clear', 'Limpiar búsqueda')}
            >
              <span className="material-symbols-outlined" style={{ fontSize: 18 }}>close</span>
              {t('warehouses.search.clear', 'Limpiar')}
            </button>
          )}
        </div>
        <div className="prd-search-meta">
          <span>
            {t('warehouses.search.showing', 'Mostrando')} <strong>{filtered.length}</strong>{' '}
            {t('warehouses.search.of', 'de')} <strong>{warehouses.length}</strong>{' '}
            {t('warehouses.table.results', 'bodegas')}
          </span>
          <span className="prd-search-shortcut">
            <kbd>Ctrl</kbd>+<kbd>K</kbd>
          </span>
        </div>
      </div>

      {/* ── Content ────────────────────────────────────────── */}
      {loading ? (
        <div className="pg-pad-40"><LoadingState /></div>
      ) : filtered.length === 0 ? (
        <div className="prd-empty-search prd-fadein">
          <span className="material-symbols-outlined prd-empty-search__icon">search_off</span>
          <p className="prd-empty-search__title">{t('warehouses.search.noResults.title', 'Sin resultados')}</p>
          {query ? (
            <>
              <p className="prd-empty-search__desc">
                {t('warehouses.search.noResults.desc', 'No se encontraron bodegas para')}{' '}
                <strong>"{query}"</strong>
              </p>
              <button type="button" className="zh-btn zh-btn--ghost zh-btn--md" onClick={handleClear}>
                {t('warehouses.search.showAll', 'Mostrar todas')}
              </button>
            </>
          ) : (
            <p className="prd-empty-search__desc">{t('warehouses.search.noWarehouses', 'No hay bodegas registradas aún.')}</p>
          )}
        </div>
      ) : (
        <>
          <div className="prd-table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>{t('warehouses.table.code', 'Código')}</th>
                  <th>{t('warehouses.table.name', 'Bodega')}</th>
                  <th>{t('warehouses.table.branch', 'Sucursal')}</th>
                  <th>{t('warehouses.table.manager', 'Encargado')}</th>
                  <th>{t('warehouses.table.capacity', 'Capacidad')}</th>
                  <th>{t('warehouses.table.status', 'Estado')}</th>
                  {(canUpdate || canDelete) && (
                    <th className="pg-th-right">{t('warehouses.table.actions', 'Acciones')}</th>
                  )}
                </tr>
              </thead>
              <tbody>
                {filtered.map((row) => (
                  <tr key={row.id} className={row.isActive ? undefined : 'prd-row--inactive'}>
                    <td>
                      <span className="badge badge--gray badge--md mono">{row.code ?? '—'}</span>
                    </td>
                    <td>
                      <div className="bod-list-name">{highlight(row.name, query)}</div>
                      {row.storageType && (
                        <div className="bod-list-sub">{row.storageType}</div>
                      )}
                    </td>
                    <td className="bod-list-branch">{branchName(row.branchId)}</td>
                    <td>
                      {row.manager
                        ? highlight(row.manager, query)
                        : <span className="subtle">—</span>}
                    </td>
                    <td>
                      {row.capacity
                        ? <span className="mono subtle">{row.capacity.toLocaleString('es')} m³</span>
                        : <span className="subtle">—</span>}
                    </td>
                    <td>
                      <span className={`prd-status-dot ${row.isActive ? 'prd-status-dot--active' : 'prd-status-dot--inactive'}`}>
                        <span className="prd-status-dot__bullet" aria-hidden />
                        {row.isActive ? t('common.active', 'Activo') : t('common.inactive', 'Inactivo')}
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
                              disabled={toggling || !row.isActive}
                              onClick={() => void onEdit(row)}
                              aria-label={`${t('common.edit', 'Editar')} ${row.name}`}
                            >
                              <span className="material-symbols-outlined">edit</span>
                            </button>
                          )}
                          {(row.isActive ? canDelete : canUpdate) && (
                            <button
                              type="button"
                              className={`zh-btn zh-btn--ghost zh-btn--sm ${row.isActive ? 'prd-btn-mute' : 'prd-btn-activate'}`}
                              title={row.isActive ? t('common.disable', 'Desactivar') : t('common.enable', 'Activar')}
                              disabled={toggling}
                              onClick={() => handleToggleClick(row)}
                              aria-label={row.isActive
                                ? `${t('common.disable', 'Desactivar')} ${row.name}`
                                : `${t('common.enable', 'Activar')} ${row.name}`}
                            >
                              <span className="material-symbols-outlined">
                                {row.isActive ? 'block' : 'check_circle'}
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
            <p className="subtle bod-list-footer-note">
              {filtered.length} {t('warehouses.table.results', 'bodegas')}
            </p>
            <p className="pg-table-timestamp">
              {t('warehouses.list.lastLoad', 'Última carga:')} {new Date().toLocaleTimeString('es')}
            </p>
          </div>
        </>
      )}

      {/* ── Confirm modal ──────────────────────────────────── */}
      <WarehouseConfirmModal
        open={!!confirmRow}
        title={confirmRow?.isActive
          ? t('warehouses.toggle.disable.title', 'Desactivar bodega')
          : t('warehouses.toggle.activate.title', 'Activar bodega')}
        message={confirmRow?.isActive
          ? <>{t('warehouses.toggle.disable.warning', 'Dejará de estar disponible para operaciones de inventario.')} <strong>{confirmRow?.name}</strong></>
          : <>¿Deseas activar la bodega <strong>{confirmRow?.name}</strong>?</>}
        confirmLabel={confirmRow?.isActive
          ? t('warehouses.toggle.disable.confirm', 'Sí, desactivar')
          : t('warehouses.toggle.activate.confirm', 'Sí, activar')}
        cancelLabel={t('common.cancel', 'Cancelar')}
        variant={confirmRow?.isActive ? 'danger' : 'default'}
        onConfirm={handleConfirmToggle}
        onCancel={() => setConfirmRow(null)}
      />
    </div>
  );
}
