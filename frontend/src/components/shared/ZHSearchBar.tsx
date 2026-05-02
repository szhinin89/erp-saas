import type { ReactNode } from 'react';
import { useMemo } from 'react';
import { useI18n } from '../../i18n/i18n';
import './ZHSearchBar.css';

export interface ZHFilter {
  id: string;
  label: string;
  options: { value: string; label: string }[];
}

export interface ZHSortOption {
  value: string;
  label: string;
}

export interface ZHSearchBarProps {
  placeholder?: string;
  onSearch: (query: string) => void;
  /** Valor actual del texto de búsqueda (controlado; p. ej. `query` de `useZHSearch`). */
  searchQuery: string;
  filters?: ZHFilter[];
  onFilterChange?: (filterId: string, value: string) => void;
  /** Valores actuales de filtros (mismo shape que `filters` en `useZHSearch`). */
  filterValues?: Record<string, string>;
  resultCount: number;
  entityLabel: string;
  actionLabel?: string;
  onAction?: () => void;
  sortOptions?: ZHSortOption[];
  onSortChange?: (value: string) => void;
  sortValue?: string;
  showExport?: boolean;
  onExport?: () => void;
  loading?: boolean;
  /** Si se pasa, el botón de limpiar delega aquí (p. ej. `clearAll` de `useZHSearch`). */
  onClearAll?: () => void;
  /** Controles extra a la derecha (p. ej. «Actualizar») antes del botón de acción principal. */
  extraActions?: ReactNode;
}

function ZHSearchBar(props: ZHSearchBarProps) {
  const { t } = useI18n();
  const {
    placeholder,
    onSearch,
    searchQuery,
    filters,
    onFilterChange,
    filterValues = {},
    resultCount,
    entityLabel,
    actionLabel,
    onAction,
    sortOptions,
    onSortChange,
    sortValue = '',
    showExport = false,
    onExport,
    loading = false,
    onClearAll,
    extraActions,
  } = props;

  const hasActiveFilters = useMemo(() => {
    for (const v of Object.values(filterValues)) {
      if (String(v ?? '').trim() !== '') return true;
    }
    return false;
  }, [filterValues]);

  const showClearAll = searchQuery.trim() !== '' || hasActiveFilters;

  const handleClearAll = () => {
    if (onClearAll) {
      onClearAll();
      return;
    }
    onSearch('');
    filters?.forEach((f) => onFilterChange?.(f.id, ''));
  };

  const chipSearchPreview =
    searchQuery.length > 32 ? `${searchQuery.slice(0, 29)}…` : searchQuery;

  const inputPlaceholder = placeholder ?? t('common.zhSearchBar.defaultPlaceholder');
  const sortLabel = t('common.zhSearchBar.sort');

  return (
    <div className="zh-search-bar">
      <div className="zh-search-bar__zone1">
        <div className="zh-search-bar__row1">
          <div className="zh-search-bar__inputWrap">
            <span className="zh-search-bar__icon" aria-hidden />
            <input
              type="search"
              className="zh-search-bar__input"
              placeholder={inputPlaceholder}
              value={searchQuery}
              onChange={(e) => onSearch(e.target.value)}
              disabled={loading}
              autoComplete="off"
              spellCheck={false}
            />
          </div>
          {filters?.map((f) => (
            <select
              key={f.id}
              className="zh-search-bar__select"
              aria-label={f.label}
              value={filterValues[f.id] ?? ''}
              onChange={(e) => onFilterChange?.(f.id, e.target.value)}
              disabled={loading}
            >
              <option value="">{f.label}</option>
              {f.options.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          ))}
          {extraActions || (actionLabel && onAction) ? (
            <div className="zh-search-bar__tail">
              {extraActions}
              {actionLabel && onAction ? (
                <button type="button" className="zh-search-bar__btnPrimary" onClick={onAction}>
                  {actionLabel}
                </button>
              ) : null}
            </div>
          ) : null}
        </div>
      </div>

      <div className="zh-search-bar__zone2">
        <div className="zh-search-bar__zone2inner">
          <div className="zh-search-bar__count">
            {loading ? (
              <span className="zh-search-bar__skeleton" aria-hidden />
            ) : (
              <>
                <strong>{resultCount}</strong> {entityLabel}
              </>
            )}
          </div>

          <div className="zh-search-bar__chips">
            {searchQuery.trim() ? (
              <button type="button" className="zh-search-bar__chip" onClick={() => onSearch('')}>
                {chipSearchPreview} <span aria-hidden>✕</span>
              </button>
            ) : null}
            {filters?.map((f) => {
              const v = filterValues[f.id];
              if (!v || String(v).trim() === '') return null;
              const opt = f.options.find((o) => o.value === v);
              return (
                <button
                  key={f.id}
                  type="button"
                  className="zh-search-bar__chip"
                  onClick={() => onFilterChange?.(f.id, '')}
                >
                  {opt?.label ?? v} <span aria-hidden>✕</span>
                </button>
              );
            })}
          </div>

          {showClearAll ? (
            <button type="button" className="zh-search-bar__clearAll" onClick={handleClearAll}>
              {t('common.zhSearchBar.clearAll')}
            </button>
          ) : null}

          {sortOptions && sortOptions.length > 0 ? (
            <select
              className="zh-search-bar__selectSort"
              aria-label={sortLabel}
              value={sortValue}
              onChange={(e) => onSortChange?.(e.target.value)}
              disabled={loading}
            >
              <option value="">{sortLabel}</option>
              {sortOptions.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          ) : null}

          {showExport && onExport ? (
            <button type="button" className="zh-search-bar__export" onClick={onExport}>
              {t('common.zhSearchBar.export')}
            </button>
          ) : null}
        </div>
      </div>
    </div>
  );
}

export { ZHSearchBar };
export default ZHSearchBar;
