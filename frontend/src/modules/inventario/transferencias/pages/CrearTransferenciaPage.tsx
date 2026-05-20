import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage } from '../../../../components/PageShell';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useAsync } from '../../../../hooks/useAsync';
import { api } from '../../../lib/api';
import { ItemsTransferenciaGrid, itemRowsToRequest, type ItemRow } from '../components/ItemsTransferenciaGrid';
import { useBodegas, useTransferenciaAcciones } from '../hooks/useTransferencias';
import type { ApiResponse } from '../../../../types/api';

interface ProductoOpcion {
  id: string;
  shortName: string;
  tracksStock: boolean;
  isActive: boolean;
}

export function CrearTransferenciaPage() {
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const canCreate = hasPerm('inventory.transfers.create');

  const { data: bodegas, loading: loadingBodegas } = useBodegas();
  const { data: productos } = useAsync<ProductoOpcion[]>(async () => {
    const res = await api.get<ApiResponse<ProductoOpcion[]>>('/api/products');
    return (res.data.responseObject ?? []).filter((p) => p.isActive);
  });

  const [bodegaOrigenId,  setBodegaOrigenId]  = useState('');
  const [bodegaDestinoId, setBodegaDestinoId] = useState('');
  const [motivo,          setMotivo]          = useState('');
  const [observaciones,   setObservaciones]   = useState('');
  const [items,           setItems]           = useState<ItemRow[]>([]);
  const [localError,      setLocalError]      = useState<string | null>(null);

  const { loading, error, crear } = useTransferenciaAcciones(() => navigate('/inventario/transferencias'));

  const validar = () => {
    if (!bodegaOrigenId)  return 'Selecciona la bodega de origen.';
    if (!bodegaDestinoId) return 'Selecciona la bodega de destino.';
    if (bodegaOrigenId === bodegaDestinoId) return 'Las bodegas de origen y destino deben ser diferentes.';
    const rows = itemRowsToRequest(items);
    if (rows.length === 0) return 'Agrega al menos un ítem con producto y cantidad.';
    const sinStock = items.filter((r) => r.productoId && r.stockDisponible !== null && r.cantidad > r.stockDisponible);
    if (sinStock.length > 0) return `Stock insuficiente para: ${sinStock.map((r) => r.descripcion).join(', ')}`;
    return null;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const err = validar();
    if (err) { setLocalError(err); return; }
    setLocalError(null);
    await crear({
      sourceWarehouseId: bodegaOrigenId,
      targetWarehouseId: bodegaDestinoId,
      reason:  motivo.trim()        || null,
      notes:   observaciones.trim() || null,
      items:   itemRowsToRequest(items),
    });
  };

  if (!canCreate) return <NoAccessPage title="Nueva Transferencia" />;

  return (
    <div className="pg-page">

      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">Inventario</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Transferencias</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Nueva</span>
          </nav>
          <h1 className="pg-title">Nueva Transferencia</h1>
          <p className="pg-subtitle">Mueva stock entre bodegas de manera controlada.</p>
        </div>
      </div>

      {(error || localError) && (
        <ZHPageNotice variant="error" message="No se pudo crear la transferencia" detail={error ?? localError ?? ''} />
      )}

      <form onSubmit={(e) => void handleSubmit(e)}>

        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">swap_horiz</span>
              <span className="pg-section-label">Bodegas y Detalle</span>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label="Bodega origen" required>
                <select className="zh-input" value={bodegaOrigenId}
                  disabled={loading || loadingBodegas}
                  onChange={(e) => { setBodegaOrigenId(e.target.value); setItems([]); }}>
                  <option value="">— seleccionar —</option>
                  {(bodegas ?? []).map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
                </select>
              </ZHField>

              <ZHField label="Bodega destino" required>
                <select className="zh-input" value={bodegaDestinoId}
                  disabled={loading || loadingBodegas}
                  onChange={(e) => setBodegaDestinoId(e.target.value)}>
                  <option value="">— seleccionar —</option>
                  {(bodegas ?? []).filter((b) => b.id !== bodegaOrigenId)
                    .map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
                </select>
              </ZHField>

              <ZHField label="Motivo">
                <input className="zh-input" value={motivo} disabled={loading}
                  onChange={(e) => setMotivo(e.target.value)}
                  placeholder="Ej. Reposición, reubicación…" maxLength={500} />
              </ZHField>

              <ZHField label="Observaciones">
                <input className="zh-input" value={observaciones} disabled={loading}
                  onChange={(e) => setObservaciones(e.target.value)}
                  placeholder="Opcional" maxLength={1000} />
              </ZHField>
            </div>
          </div>
        </div>

        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">list_alt</span>
              <span className="pg-section-label">Ítems a Transferir</span>
            </div>
          </div>
          <div className="pg-section-body">
            <ItemsTransferenciaGrid
              sourceWarehouseId={bodegaOrigenId}
              items={items}
              onChange={setItems}
              disabled={loading || !bodegaOrigenId}
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
    </div>
  );
}
