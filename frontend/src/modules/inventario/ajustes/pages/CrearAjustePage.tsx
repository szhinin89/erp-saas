import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage } from '../../../../components/PageShell';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useAjusteAcciones } from '../hooks/useAjustes';
import { useBodegas } from '../../transferencias/hooks/useTransferencias';
import { ajusteService, MOTIVOS_PREDEFINIDOS } from '../api/ajusteService';
import { useAsync } from '../../../../hooks/useAsync';
import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

interface ProductoOpcion {
  id: string;
  shortName: string;
  isActive: boolean;
  isService: boolean;
  tracksStock: boolean;
}

export function CrearAjustePage() {
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const canCreate = hasPerm('inventory.adjustments.create');

  const { data: bodegas, loading: loadingBodegas } = useBodegas();
  const { data: productos } = useAsync<ProductoOpcion[]>(async () => {
    const res = await api.get<ApiResponse<ProductoOpcion[]>>('/api/inventory/products');
    return (res.data.responseObject ?? []).filter((p) => p.isActive && !p.isService && p.tracksStock);
  });

  const [bodegaId,   setBodegaId]   = useState('');
  const [productoId, setProductoId] = useState('');
  const [signo,      setSigno]      = useState<'+' | '-'>('+');
  const [cantidad,   setCantidad]   = useState('');
  const [motivo,     setMotivo]     = useState('');
  const [motivoOtro, setMotivoOtro] = useState('');
  const [obs,        setObs]        = useState('');
  const [stock,      setStock]      = useState<number | null>(null);
  const [localError, setLocalError] = useState<string | null>(null);

  const { loading, error, crear } = useAjusteAcciones(() => navigate('/inventario/ajustes'));

  useEffect(() => {
    if (!bodegaId || !productoId) { setStock(null); return; }
    ajusteService.getStockDisponible(bodegaId, productoId).then(setStock).catch(() => setStock(null));
  }, [bodegaId, productoId]);

  const motivoFinal       = motivo === 'Otro' ? motivoOtro : motivo;
  const cantidadNum       = parseFloat(cantidad) || 0;
  const cantidadConSigno  = signo === '-' ? -Math.abs(cantidadNum) : Math.abs(cantidadNum);
  const stockInsuficiente = signo === '-' && stock !== null && cantidadNum > stock;
  const stockLabel        = stock === null ? '—' : `${stock} disponibles`;

  const validar = () => {
    if (!bodegaId)   return 'Selecciona la bodega.';
    if (!productoId) return 'Selecciona el producto.';
    if (!cantidadNum || cantidadNum <= 0) return 'La cantidad debe ser mayor a cero.';
    if (!motivoFinal.trim()) return 'El motivo es obligatorio.';
    if (stockInsuficiente) return `Stock insuficiente: disponible ${stock}, solicitado ${cantidadNum}.`;
    return null;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const err = validar();
    if (err) { setLocalError(err); return; }
    setLocalError(null);
    await crear({ warehouseId: bodegaId, productId: productoId,
      adjustmentQty: cantidadConSigno, reason: motivoFinal.trim(), notes: obs.trim() || null });
  };

  if (!canCreate) return <NoAccessPage title="Nuevo Ajuste" />;

  return (
    <div className="pg-page">

      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">Inventario</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Ajustes</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Nuevo</span>
          </nav>
          <h1 className="pg-title">Nuevo Ajuste de Inventario</h1>
          <p className="pg-subtitle">Registre una entrada o salida manual de stock.</p>
        </div>
      </div>

      {(error || localError) && (
        <ZHPageNotice variant="error" message="No se pudo crear el ajuste" detail={error ?? localError ?? ''} />
      )}

      <form onSubmit={(e) => void handleSubmit(e)}>

        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">inventory_2</span>
              <span className="pg-section-label">Producto y Bodega</span>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label="Bodega" required>
                <select className="zh-input" value={bodegaId}
                  disabled={loading || loadingBodegas}
                  onChange={(e) => setBodegaId(e.target.value)}>
                  <option value="">— seleccionar —</option>
                  {(bodegas ?? []).map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
                </select>
              </ZHField>

              <ZHField label="Producto" required>
                <select className="zh-input" value={productoId} disabled={loading}
                  onChange={(e) => setProductoId(e.target.value)}>
                  <option value="">— seleccionar —</option>
                  {(productos ?? []).map((p) => <option key={p.id} value={p.id}>{p.shortName}</option>)}
                </select>
              </ZHField>
            </div>

            {bodegaId && productoId && (
              <div style={{
                marginTop: 'var(--space-3)', padding: 'var(--space-3) var(--space-4)',
                background: stockInsuficiente ? 'var(--color-error-surface, #fdecea)' : 'var(--color-surface-container)',
                borderRadius: 'var(--radius-md)', fontSize: 'var(--text-body-sm-size)',
                color: stockInsuficiente ? 'var(--color-error)' : 'var(--color-text-secondary)',
              }}>
                Stock actual en bodega seleccionada: <strong>{stockLabel}</strong>
              </div>
            )}
          </div>
        </div>

        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">tune</span>
              <span className="pg-section-label">Ajuste</span>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--3">
              <ZHField label="Tipo" required>
                <select className="zh-input" value={signo} disabled={loading}
                  onChange={(e) => setSigno(e.target.value as '+' | '-')}>
                  <option value="+">Incremento (agregar stock)</option>
                  <option value="-">Disminución (quitar stock)</option>
                </select>
              </ZHField>

              <ZHField label="Cantidad" required>
                <input className={`zh-input${stockInsuficiente ? ' zh-input--error' : ''}`}
                  type="number" min={0.001} step={0.001}
                  value={cantidad} disabled={loading}
                  onChange={(e) => setCantidad(e.target.value)} placeholder="0" />
              </ZHField>

              <ZHField label="Motivo" required>
                <select className="zh-input" value={motivo} disabled={loading}
                  onChange={(e) => setMotivo(e.target.value)}>
                  <option value="">— seleccionar —</option>
                  {MOTIVOS_PREDEFINIDOS.map((m) => <option key={m} value={m}>{m}</option>)}
                </select>
              </ZHField>

              {motivo === 'Otro' && (
                <ZHField label="Especificar motivo" required>
                  <input className="zh-input" value={motivoOtro} disabled={loading}
                    onChange={(e) => setMotivoOtro(e.target.value)}
                    maxLength={200} placeholder="Describa el motivo" />
                </ZHField>
              )}

              <ZHField label="Observaciones">
                <input className="zh-input" value={obs} disabled={loading}
                  onChange={(e) => setObs(e.target.value)}
                  maxLength={1000} placeholder="Opcional" />
              </ZHField>
            </div>
          </div>
        </div>

        <div className="pg-actions-bar">
          <div className="pg-actions-info" />
          <div className="pg-actions-buttons">
            <ZHBtn variant="ghost" size="md" type="button" disabled={loading}
              onClick={() => navigate('/inventario/ajustes')}>
              Cancelar
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="submit" disabled={loading}>
              <span className="material-symbols-outlined">save</span>
              {loading ? 'Guardando…' : 'Crear Ajuste'}
            </ZHBtn>
          </div>
        </div>
      </form>
    </div>
  );
}
