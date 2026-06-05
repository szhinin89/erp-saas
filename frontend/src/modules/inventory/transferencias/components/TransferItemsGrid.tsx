import { transferService } from '../api/transferService';
import type { TransferItemRequest } from '../api/transferService';
import { ZhDecimalInput } from '../../../../components/zh/inputs';
import './items-transferencia-grid.css';

export interface ItemRow {
  productId: string;
  description: string;
  quantity: number;
  AvailableStock: number | null;
}

interface Props {
  sourceWarehouseId: string;
  items: ItemRow[];
  onChange: (items: ItemRow[]) => void;
  disabled?: boolean;
  productos: Array<{ id: string; shortName: string; tracksStock: boolean }>;
}

export function ItemsTransferenciaGrid({ sourceWarehouseId, items, onChange, disabled, productos }: Props) {
  const addRow = () => {
    onChange([...items, { productId: '', description: '', quantity: 1, AvailableStock: null }]);
  };

  const removeRow = (idx: number) => {
    onChange(items.filter((_, i) => i !== idx));
  };

  const updateRow = async (idx: number, field: keyof ItemRow, value: string | number) => {
    const next = items.map((row, i) => (i === idx ? { ...row, [field]: value } : row));

    if (field === 'productId' && value && sourceWarehouseId) {
      const prod = productos.find((p) => p.id === value);
      next[idx] = {
        ...next[idx],
        description:      prod?.shortName ?? '',
        AvailableStock:  null,
      };
      onChange(next);

      if (prod?.tracksStock) {
        const disponible = await transferService.getStockDisponible(sourceWarehouseId, String(value));
        onChange(
          next.map((row, i) => (i === idx ? { ...row, AvailableStock: disponible } : row))
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
  const stockOk = row.AvailableStock === null || row.cantidad <= row.AvailableStock;

  return (
    <tr>
      <td>
        <select
          value={row.productId}
          disabled={disabled}
          onChange={(e) => void onUpdate(idx, 'productId', e.target.value)}
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
        <ZhDecimalInput
          decimals={4}
          positiveOnly
          value={row.cantidad}
          disabled={disabled}
          onChange={(e) => void onUpdate(idx, 'cantidad', parseFloat(e.target.value) || 0)}
          className={`itg-control ${stockOk ? '' : 'itg-input-error'}`.trim()}
        />
      </td>
      <td className={stockOk ? 'itg-stock--ok' : 'itg-stock--warning'}>
        {row.AvailableStock === null
          ? '—'
          : `${row.AvailableStock} ${!stockOk ? '⚠ insuficiente' : ''}`}
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

export function itemRowsToRequest(rows: ItemRow[]): TransferItemRequest[] {
  return rows
    .filter((r) => r.productId && r.cantidad > 0)
    .map((r) => ({ productId: r.productId, quantity: r.cantidad }));
}
