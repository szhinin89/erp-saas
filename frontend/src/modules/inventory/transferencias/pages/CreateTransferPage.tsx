import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { useAsync } from '../../../../hooks/useAsync';
import { api } from '../../../lib/api';
import { ItemsTransferenciaGrid, itemRowsToRequest, type ItemRow } from ../components/TransferItemsGrid';
import { useBodegas, useTransferActions } from '../hooks/useTransfers';
import type { ApiResponse } from '../../../../types/api';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';

interface ProductoOpcion {
  id: string;
  shortName: string;
  tracksStock: boolean;
  isActive: boolean;
}

export function CrearTransferenciaPage() {
  const { canShow } = usePermissionsUi();
  const navigate  = useNavigate();
  const canCreate = canShow('inventory.transfers.create');

  const { data: bodegas, loading: loadingBodegas } = useBodegas();
  const { data: productos } = useAsync<ProductoOpcion[]>(async () => {
    const res = await api.get<ApiResponse<ProductoOpcion[]>>('/api/inventory/products');
    return (res.data.responseObject ?? []).filter((p) => p.isActive);
  });

  const [sourceWarehouseId,  setBodegaOrigenId]  = useState('');
  const [targetWarehouseId, setBodegaDestinoId] = useState('');
  const [motivo,          setMotivo]          = useState('');
  const [notes,   setObservaciones]   = useState('');
  const [items,           setItems]           = useState<ItemRow[]>([]);
  const [localError,      setLocalError]      = useState<string | null>(null);

  const { loading, error, crear } = useTransferActions(() => navigate('/inventario/transferencias'));

  const validar = () => {
    if (!sourceWarehouseId)  return 'Selecciona la bodega de origen.';
    if (!targetWarehouseId) return 'Selecciona la bodega de destino.';
    if (sourceWarehouseId === targetWarehouseId) return 'Las bodegas de origen y destino deben ser diferentes.';
    const rows = itemRowsToRequest(items);
    if (rows.length === 0) return 'Agrega al menos un ítem con producto y cantidad.';
    const sinStock = items.filter((r) => r.productId && r.AvailableStock !== null && r.cantidad > r.AvailableStock);
    if (sinStock.length > 0) return `Stock insuficiente para: ${sinStock.map((r) => r.description).join(', ')}`;
    return null;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const err = validar();
    if (err) { setLocalError(err); return; }
    setLocalError(null);
    await crear({
      sourceWarehouseId: sourceWarehouseId,
      targetWarehouseId: targetWarehouseId,
      reason:  motivo.trim()        || null,
      notes:   notes.trim() || null,
      items:   itemRowsToRequest(items),
    });
  };

  if (!canCreate) return <NoAccessPage title="Nueva Transferencia" />;

  return (
    <ErpPageTemplate
      kicker="Inventario"
      title="Nueva Transferencia"
      subtitle="Mueva stock entre bodegas de manera controlada."
    >
      {(error || localError) && (
        <ZHPageNotice variant="error" message="No se pudo crear la transferencia" detail={error ?? localError ?? ''} />
      )}

      <form onSubmit={(e) => void handleSubmit(e)}>

        <div className="pg-section pg-section--mb-4">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">swap_horiz</span>
              <span className="pg-section-label">Bodegas y Detalle</span>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label="Bodega origen" required>
                <select className="zh-input" value={sourceWarehouseId}
                  disabled={loading || loadingBodegas}
                  onChange={(e) => { setBodegaOrigenId(e.target.value); setItems([]); }}>
                  <option value="">— seleccionar —</option>
                  {(bodegas ?? []).map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
                </select>
              </ZHField>

              <ZHField label="Bodega destino" required>
                <select className="zh-input" value={targetWarehouseId}
                  disabled={loading || loadingBodegas}
                  onChange={(e) => setBodegaDestinoId(e.target.value)}>
                  <option value="">— seleccionar —</option>
                  {(bodegas ?? []).filter((b) => b.id !== sourceWarehouseId)
                    .map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
                </select>
              </ZHField>

              <ZHField label="Motivo">
                <input className="zh-input" value={motivo} disabled={loading}
                  onChange={(e) => setMotivo(e.target.value)}
                  placeholder="Ej. Reposición, reubicación…" maxLength={500} />
              </ZHField>

              <ZHField label="notes">
                <input className="zh-input" value={notes} disabled={loading}
                  onChange={(e) => setObservaciones(e.target.value)}
                  placeholder="Opcional" maxLength={1000} />
              </ZHField>
            </div>
          </div>
        </div>

        <div className="pg-section pg-section--mb-4">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">list_alt</span>
              <span className="pg-section-label">Ítems a Transferir</span>
            </div>
          </div>
          <div className="pg-section-body">
            <ItemsTransferenciaGrid
              sourceWarehouseId={sourceWarehouseId}
              items={items}
              onChange={setItems}
              disabled={loading || !sourceWarehouseId}
              productos={productos ?? []}
            />
          </div>
        </div>

        <div className="pg-actions-bar">
          <div className="pg-actions-info" />
          <div className="pg-actions-buttons">
            <ZHBtn variant="ghost" size="md" type="button" disabled={loading}
              onClick={() => navigate('/inventario/transferencias')}>
              Cancelar
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="submit" disabled={loading}>
              <span className="material-symbols-outlined">save</span>
              {loading ? 'Guardando…' : 'Crear en Borrador'}
            </ZHBtn>
          </div>
        </div>
      </form>
    </ErpPageTemplate>
  );
}
