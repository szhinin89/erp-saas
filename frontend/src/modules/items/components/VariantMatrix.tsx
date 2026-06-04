import { useState, useMemo } from 'react';
import { ZHBtn } from '../../../components/zh/ZHForm';

/**
 * VariantMatrix — genera automáticamente el producto cartesiano de ejes de variante.
 *
 * Ejes de ejemplo:
 *   Color:  [Red, Blue, Green]
 *   Talla:  [S, M, L, XL]
 *
 * Genera: Red/S, Red/M, Red/L, Red/XL, Blue/S, ...
 * Cada celda permite overridear SKU, código de barras y activar/desactivar.
 */

export interface VariantAxis {
  attributeDefinitionId: string;
  name: string;
  values: string[];
}

export interface MatrixCell {
  axisValues: Record<string, string>;  // { COLOR: "Red", SIZE: "L" }
  skuOverride: string;
  barcode: string;
  isActive: boolean;
}

type Props = {
  axes:       VariantAxis[];
  itemSku:    string;
  t:          (key: string, fallback?: string) => string;
  onAxesChange: (axes: VariantAxis[]) => void;
  onChange:   (cells: MatrixCell[]) => void;
};

function cartesian(arrays: string[][]): string[][] {
  return arrays.reduce<string[][]>(
    (acc, arr) => acc.flatMap(prefix => arr.map(v => [...prefix, v])),
    [[]]
  );
}

function buildAutoSku(itemSku: string, axisValues: Record<string, string>): string {
  const suffix = Object.values(axisValues).map(v => v.toUpperCase().replace(/\s+/g, '-')).join('-');
  return `${itemSku}-${suffix}`;
}

export function VariantMatrix({ axes, itemSku, t, onAxesChange, onChange }: Props) {
  const [cells, setCells] = useState<MatrixCell[]>([]);
  const [newAxisName, setNewAxisName] = useState('');
  const [newAxisValues, setNewAxisValues] = useState<Record<string, string>>({});
  const [valueInputs, setValueInputs] = useState<Record<string, string>>({});

  // Regenerate matrix cells when axes change
  const generatedCells = useMemo<MatrixCell[]>(() => {
    if (axes.length === 0) return [];
    const nonEmpty = axes.filter(a => a.values.length > 0);
    if (nonEmpty.length === 0) return [];

    const combos = cartesian(nonEmpty.map(a => a.values));
    return combos.map(combo => {
      const axisValues: Record<string, string> = {};
      nonEmpty.forEach((a, i) => { axisValues[a.attributeDefinitionId] = combo[i]; });

      // Preserve existing overrides if cell already exists
      const key = Object.values(axisValues).join('|');
      const existing = cells.find(c => Object.values(c.axisValues).join('|') === key);

      return existing ?? {
        axisValues,
        skuOverride: buildAutoSku(itemSku, axisValues),
        barcode: '',
        isActive: true,
      };
    });
  }, [axes, itemSku]);

  // Sync cells with parent
  const handleCellChange = (index: number, field: keyof MatrixCell, value: string | boolean) => {
    const updated = [...generatedCells];
    updated[index] = { ...updated[index], [field]: value } as MatrixCell;
    setCells(updated);
    onChange(updated);
  };

  const addAxisValue = (axisIndex: number) => {
    const val = (valueInputs[axisIndex] ?? '').trim();
    if (!val) return;

    const updatedAxes = axes.map((a, i) =>
      i === axisIndex
        ? { ...a, values: a.values.includes(val) ? a.values : [...a.values, val] }
        : a
    );
    onAxesChange(updatedAxes);
    setValueInputs(prev => ({ ...prev, [axisIndex]: '' }));
  };

  const removeAxisValue = (axisIndex: number, value: string) => {
    const updatedAxes = axes.map((a, i) =>
      i === axisIndex ? { ...a, values: a.values.filter(v => v !== value) } : a
    );
    onAxesChange(updatedAxes);
  };

  const addAxis = () => {
    const name = newAxisName.trim();
    if (!name) return;
    onAxesChange([...axes, { attributeDefinitionId: `axis-${Date.now()}`, name, values: [] }]);
    setNewAxisName('');
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      {/* Axis builder */}
      <div>
        <h4 style={{ marginBottom: 12, fontWeight: 600 }}>
          {t('items.variants.axes', 'Ejes de variante')}
        </h4>

        {axes.map((axis, axisIndex) => (
          <div key={axis.attributeDefinitionId} style={{ marginBottom: 16, padding: 16, border: '1px solid var(--color-border)', borderRadius: 8 }}>
            <div style={{ fontWeight: 600, marginBottom: 8 }}>{axis.name}</div>

            {/* Current values */}
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginBottom: 8 }}>
              {axis.values.map(v => (
                <span key={v} style={{
                  display: 'inline-flex', alignItems: 'center', gap: 4,
                  padding: '4px 10px', background: 'var(--color-bg-subtle)',
                  border: '1px solid var(--color-border)', borderRadius: 16, fontSize: 13
                }}>
                  {v}
                  <button
                    type="button"
                    onClick={() => removeAxisValue(axisIndex, v)}
                    style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--color-text-tertiary)', lineHeight: 1 }}
                  >
                    ×
                  </button>
                </span>
              ))}
            </div>

            {/* Add value */}
            <div style={{ display: 'flex', gap: 8 }}>
              <input
                className="zh-input"
                placeholder={t('items.variants.addValue', 'Agregar valor (ej: Rojo, L, 42)')}
                value={valueInputs[axisIndex] ?? ''}
                onChange={e => setValueInputs(prev => ({ ...prev, [axisIndex]: e.target.value }))}
                onKeyDown={e => e.key === 'Enter' && (e.preventDefault(), addAxisValue(axisIndex))}
                style={{ flex: 1 }}
              />
              <ZHBtn type="button" variant="secondary" size="sm" onClick={() => addAxisValue(axisIndex)}>
                +
              </ZHBtn>
            </div>
          </div>
        ))}

        {/* Add axis */}
        <div style={{ display: 'flex', gap: 8 }}>
          <input
            className="zh-input"
            placeholder={t('items.variants.newAxis', 'Nombre del eje (ej: Color, Talla, Tamaño)')}
            value={newAxisName}
            onChange={e => setNewAxisName(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && (e.preventDefault(), addAxis())}
            style={{ flex: 1 }}
          />
          <ZHBtn type="button" variant="secondary" size="md" onClick={addAxis}>
            {t('items.variants.addAxis', '+ Eje')}
          </ZHBtn>
        </div>
      </div>

      {/* Matrix grid */}
      {generatedCells.length > 0 && (
        <div>
          <h4 style={{ marginBottom: 12, fontWeight: 600 }}>
            {t('items.variants.matrix', 'Matriz de variantes')}
            <span style={{ marginLeft: 8, fontSize: 13, color: 'var(--color-text-secondary)', fontWeight: 400 }}>
              ({generatedCells.length} {t('items.variants.combinations', 'combinaciones')})
            </span>
          </h4>

          <div style={{ overflowX: 'auto' }}>
            <table className="zh-table">
              <thead>
                <tr>
                  {axes.filter(a => a.values.length > 0).map(a => (
                    <th key={a.attributeDefinitionId}>{a.name}</th>
                  ))}
                  <th>{t('items.variants.col.sku', 'SKU')}</th>
                  <th>{t('items.variants.col.barcode', 'Código de barras')}</th>
                  <th>{t('common.active', 'Activo')}</th>
                </tr>
              </thead>
              <tbody>
                {generatedCells.map((cell, i) => (
                  <tr key={i}>
                    {axes.filter(a => a.values.length > 0).map(a => (
                      <td key={a.attributeDefinitionId}>
                        <strong>{cell.axisValues[a.attributeDefinitionId]}</strong>
                      </td>
                    ))}
                    <td>
                      <input
                        className="zh-input"
                        value={cell.skuOverride}
                        onChange={e => handleCellChange(i, 'skuOverride', e.target.value)}
                        style={{ minWidth: 180, fontFamily: 'monospace', fontSize: 13 }}
                      />
                    </td>
                    <td>
                      <input
                        className="zh-input"
                        value={cell.barcode}
                        onChange={e => handleCellChange(i, 'barcode', e.target.value)}
                        placeholder="EAN / código"
                        style={{ minWidth: 150 }}
                      />
                    </td>
                    <td style={{ textAlign: 'center' }}>
                      <input
                        type="checkbox"
                        checked={cell.isActive}
                        onChange={e => handleCellChange(i, 'isActive', e.target.checked)}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {axes.length > 0 && generatedCells.length === 0 && (
        <p style={{ color: 'var(--color-text-secondary)' }}>
          {t('items.variants.addValuesToGenerate', 'Agrega valores a los ejes para generar la matriz de variantes.')}
        </p>
      )}
    </div>
  );
}
