import type { ReactNode } from 'react';
import { EmptyState, LoadingState } from '../PageShell';

export interface ZHDataTableColumn<T> {
  key: string;
  header: ReactNode;
  render: (row: T) => ReactNode;
  align?: 'left' | 'center' | 'right';
}

interface ZHDataTableProps<T> {
  columns: ZHDataTableColumn<T>[];
  rows: T[];
  rowKey: (row: T) => string;
  onRowClick?: (row: T) => void;
  loading?: boolean;
  emptyMessage?: string;
  /** Paginación integrada — se omite el footer si no se provee. */
  page?: number;
  pageSize?: number;
  onPageChange?: (page: number) => void;
  /** Total de filas conocido por el backend (habilita deshabilitar "siguiente" con precisión) — si se omite, se infiere por `rows.length < pageSize`. */
  total?: number;
}

/**
 * Grilla de solo lectura reutilizable del Design System: columnas configurables, fila clicable,
 * estados de carga/vacío (reutiliza `LoadingState`/`EmptyState`), paginación opcional integrada.
 * Sin ordenamiento por columna — no lo requiere ningún consumidor todavía; se puede agregar
 * después sin romper la API pública.
 */
export function ZHDataTable<T>({
  columns, rows, rowKey, onRowClick, loading, emptyMessage,
  page, pageSize, onPageChange, total,
}: ZHDataTableProps<T>) {
  if (loading) return <div className="pg-pad-40"><LoadingState /></div>;
  if (rows.length === 0) return <div className="pg-pad-40"><EmptyState message={emptyMessage ?? 'Sin resultados.'} /></div>;

  const showPagination = page !== undefined && pageSize !== undefined && onPageChange !== undefined;
  const hasNextPage = total !== undefined ? page! * pageSize! < total : rows.length >= (pageSize ?? 0);

  return (
    <>
      <div className="pg-overflow-x">
        <table className="table">
          <thead>
            <tr>
              {columns.map((col) => (
                <th key={col.key} style={col.align ? { textAlign: col.align } : undefined}>{col.header}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={rowKey(row)} className={onRowClick ? 'zh-datatable-row--clickable' : undefined}
                onClick={onRowClick ? () => onRowClick(row) : undefined}>
                {columns.map((col) => (
                  <td key={col.key} style={col.align ? { textAlign: col.align } : undefined}>{col.render(row)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showPagination && (
        <div className="pg-table-footer">
          {total !== undefined && (
            <span className="zh-datatable-total">{total} {total === 1 ? 'registro' : 'registros'}</span>
          )}
          <div className="pg-pagination-controls">
            <button className="pg-pagination-btn" type="button" disabled={page! <= 1}
              onClick={() => onPageChange!(page! - 1)}>
              <span className="material-symbols-outlined">chevron_left</span>
            </button>
            <span style={{ padding: '0 10px', display: 'flex', alignItems: 'center' }}>{page}</span>
            <button className="pg-pagination-btn" type="button" disabled={!hasNextPage}
              onClick={() => onPageChange!(page! + 1)}>
              <span className="material-symbols-outlined">chevron_right</span>
            </button>
          </div>
        </div>
      )}
    </>
  );
}
