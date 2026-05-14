import { transferenciaService } from '../api/transferenciaService';
import type { ItemTransferenciaRequest } from '../api/transferenciaService';
import './items-transferencia-grid.css';

export interface ItemRow {
  productoId: string;
  descripcion: string;
  cantidad: number;
  stockDisponible: number | null;
}

interface Props {
  bodegaOrigenId: string;
  items: ItemRow[];
  onChange: (items: ItemRow[]) => void;
  disabled?: boolean;
  productos: Array<{ id: string; shortName: string; tracksStock: boolean }>;
}

export function ItemsTransferenciaGrid({ bodegaOrigenId, items, onChange, disabled, productos }: Props) {
  const addRow = () => {
    onChange([...items, { productoId: '', descripcion: '', cantidad: 1, stockDisponible: null }]);
  };

  const removeRow = (idx: number) => {
    onChange(items.filter((_, i) => i !== idx));
  };

  const updateRow = async (idx: number, field: keyof ItemRow, value: string | number) => {
    const next = items.map((row, i) => (i === idx ? { ...row, [field]: value } : row));

    if (field === 'productoId' && value && bodegaOrigenId) {
      const prod = productos.find((p) => p.id === value);
      next[idx] = {
        ...next[idx],
        descripcion:      prod?.shortName ?? '',
        stockDisponible:  null,
      };
      onChange(next);

      if (prod?.tracksStock) {
        const disponible = await transferenciaService.getStockDisponible(bodegaOrigenId, String(value));
        onChange(
          next.map((row, i) => (i === idx ? { ...row, stockDisponible: disponible } : row))
        );
      }
      return;
    }

    onChange(next);
  };

  return (
    <div className="zh-mt-8">
      <table className="table table--compact">
        <thead>
          <tr>
            <th className="itg-col-producto">Producto</th>
            <th className="itg-col-cantidad">Cantidad</th>
            <th className="itg-col-stock">Stock disponible</th>
            <th className="itg-col-actions"></th>
          </tr>
        </thead>
        <tbody>
          {items.map((row, idx) => (
            <ItemRowComponent
              key={idx}
              row={row}
              idx={idx}
              productos={productos}
              disabled={disabled}
              onUpdate={updateRow}
              onRemove={removeRow}
            />
          ))}
          {items.length === 0 && (
            <tr>
              <td colSpan={4} className="itg-empty">
                Agrega al menos un ítem
              </td>
            </tr>
          )}
        </tbody>
      </table>

      {!disabled && (
        <button
          type="button"
          className="zh-btn zh-btn--ghost zh-btn--sm zh-mt-8"
          onClick={addRow}
        >
          + Agregar ítem
        </button>
      )}
    </div>
  );
}

interface RowProps {
  row: ItemRow;
  idx: number;
  productos: Array<{ id: string; shortName: string; tracksStock: boolean }>;
  disabled?: boolean;
  onUpdate: (idx: number, field: keyof ItemRow, value: string | number) => void;
  onRemove: (idx: number) => void;
}

function ItemRowComponent({ row, idx, productos, disabled, onUpdate, onRemove }: RowProps) {
  const stockOk = row.stockDisponible === null || row.cantidad <= row.stockDisponible;

  return (
    <tr>
      <td>
        <select
          value={row.productoId}
          disabled={disabled}
          onChange={(e) => void onUpdate(idx, 'productoId', e.target.value)}
          className="itg-control"
        >
          <option value="">— seleccionar —</option>
          {productos.map((p) => (
            <option key={p.id} value={p.id}>
              {p.shortName}
            </option>
          ))}
        </select>
      </td>
      <td>
        <input
          type="number"
          min={0.001}
          step={0.001}
          value={row.cantidad}
          disabled={disabled}
          onChange={(e) => void onUpdate(idx, 'cantidad', parseFloat(e.target.value) || 0)}
          className={`itg-control ${stockOk ? '' : 'itg-input-error'}`.trim()}
        />
      </td>
      <td className={stockOk ? 'itg-stock--ok' : 'itg-stock--warning'}>
        {row.stockDisponible === null
          ? '—'
          : `${row.stockDisponible} ${!stockOk ? '⚠ insuficiente' : ''}`}
      </td>
      <td>
        {!disabled && (
          <button
            type="button"
            className="zh-btn zh-btn--ghost zh-btn--sm"
            onClick={() => onRemove(idx)}
          >
            ✕
          </button>
        )}
      </td>
    </tr>
  );
}

export function itemRowsToRequest(rows: ItemRow[]): ItemTransferenciaRequest[] {
  return rows
    .filter((r) => r.productoId && r.cantidad > 0)
    .map((r) => ({ productoId: r.productoId, cantidad: r.cantidad }));
}
