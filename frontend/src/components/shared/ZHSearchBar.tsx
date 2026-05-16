import type { ReactNode, RefObject } from 'react';
import { useMemo, useCallback, forwardRef, useImperativeHandle, useRef } from 'react';
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

export interface ZHSearchBarRef {
  focusInput: () => void;
  resetAndFocus: () => void;
}

const ZHSearchBar = forwardRef<ZHSearchBarRef, ZHSearchBarProps>((props, ref) => {
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

  const inputRef = useRef<HTMLInputElement>(null);

  // Exponer métodos útiles al padre
  useImperativeHandle(ref, () => ({
    focusInput: () => {
      inputRef.current?.focus();
    },
    resetAndFocus: () => {
      onSearch('');
      filters?.forEach((f) => onFilterChange?.(f.id, ''));
      inputRef.current?.focus();
    },
  }));

  const hasActiveFilters = useMemo(() => {
    for (const v of Object.values(filterValues)) {
      if (String(v ?? '').trim() !== '') return true;
    }
    return false;
  }, [filterValues]);

  const showClearAll = searchQuery.trim() !== '' || hasActiveFilters;

  // Handler unificado de limpieza
  const handleClearAll = useCallback(() => {
    if (onClearAll) {
      onClearAll();
    } else {
      onSearch('');
      filters?.forEach((f) => onFilterChange?.(f.id, ''));
    }
    // Devolver foco al input después de limpiar
    setTimeout(() => inputRef.current?.focus(), 0);
  }, [onClearAll, onSearch, filters, onFilterChange]);

  // Truncado para chip de búsqueda
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
              ref={inputRef}
              type="search"
              className="zh-search-bar__input"
              placeholder={inputPlaceholder}
              value={searchQuery}
              onChange={(e) => onSearch(e.target.value)}
              disabled={loading}
              autoComplete="off"
              spellCheck={false}
              aria-label={t('common.zhSearchBar.searchInputLabel')}
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
          {(extraActions || (actionLabel && onAction)) && (
            <div className="zh-search-bar__tail">
              {extraActions}
              {actionLabel && onAction && (
                <button
                  type="button"
                  className="zh-search-bar__btnPrimary"
                  onClick={onAction}
                  disabled={loading}
                  aria-label={actionLabel}
                >
                  {actionLabel}
                </button>
              )}
            </div>
          )}
        </div>
      </div>

      <div className="zh-search-bar__zone2">
        <div className="zh-search-bar__zone2inner">
          <div className="zh-search-bar__count" aria-live="polite" aria-atomic="true">
            {loading ? (
              <span className="zh-search-bar__skeleton" aria-hidden />
            ) : (
              <>
                <strong>{resultCount}</strong> {entityLabel}
              </>
            )}
          </div>

          <div className="zh-search-bar__chips">
            {searchQuery.trim() && (
              <button
                type="button"
                className="zh-search-bar__chip"
                onClick={() => onSearch('')}
                disabled={loading}
                aria-label={t('common.zhSearchBar.removeSearchTerm', { term: searchQuery })}
                title={searchQuery} // tooltip con el texto completo
              >
                {chipSearchPreview} <span aria-hidden>✕</span>
              </button>
            )}
            {filters?.map((f) => {
              const v = filterValues[f.id];
              if (!v || String(v).trim() === '') return null;
              const opt = f.options.find((o) => o.value === v);
              const displayLabel = opt?.label ?? v;
              return (
                <button
                  key={f.id}
                  type="button"
                  className="zh-search-bar__chip"
                  onClick={() => onFilterChange?.(f.id, '')}
                  disabled={loading}
                  aria-label={t('common.zhSearchBar.removeFilter', { filter: displayLabel })}
                >
                  {displayLabel} <span aria-hidden>✕</span>
                </button>
              );
            })}
          </div>

          {showClearAll && (
            <button
              type="button"
              className="zh-search-bar__clearAll"
              onClick={handleClearAll}
              disabled={loading}
              aria-label={t('common.zhSearchBar.clearAllFiltersAndSearch')}
            >
              {t('common.zhSearchBar.clearAll')}
            </button>
          )}

          {sortOptions && sortOptions.length > 0 && (
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
          )}

          {showExport && onExport && (
            <button
              type="button"
              className="zh-search-bar__export"
              onClick={onExport}
              disabled={loading}
              aria-label={t('common.zhSearchBar.export')}
            >
              {t('common.zhSearchBar.export')}
            </button>
          )}
        </div>
      </div>
    </div>
  );
});

ZHSearchBar.displayName = 'ZHSearchBar';

export { ZHSearchBar };
export default ZHSearchBar;