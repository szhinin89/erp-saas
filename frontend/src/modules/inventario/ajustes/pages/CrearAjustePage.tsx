import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage, PageShell } from '../../../../components/PageShell';
import { Card } from '../../../../components/ui/Card';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useAjusteAcciones } from '../hooks/useAjustes';
import { useBodegas } from '../../transferencias/hooks/useTransferencias';
import { ajusteService, MOTIVOS_PREDEFINIDOS } from '../api/ajusteService';
import { useAsync } from '../../../../hooks/useAsync';
import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';
import './ajustes-pages.css';

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
  const canCreate = hasPerm('inventario.ajustes.create');

  const { data: bodegas, loading: loadingBodegas } = useBodegas();
  const { data: productos } = useAsync<ProductoOpcion[]>(async () => {
    const res = await api.get<ApiResponse<ProductoOpcion[]>>('/api/products');
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

  const { loading, error, crear } = useAjusteAcciones(
    () => navigate('/inventario/ajustes')
  );

  // Cargar stock disponible cuando cambian bodega o producto
  useEffect(() => {
    if (!bodegaId || !productoId) { setStock(null); return; }
    ajusteService.getStockDisponible(bodegaId, productoId)
      .then(setStock)
      .catch(() => setStock(null));
  }, [bodegaId, productoId]);

  const motivoFinal = motivo === 'Otro' ? motivoOtro : motivo;
  const cantidadNum = parseFloat(cantidad) || 0;
  const cantidadConSigno = signo === '-' ? -Math.abs(cantidadNum) : Math.abs(cantidadNum);

  const validar = () => {
    if (!bodegaId)   return 'Selecciona la bodega.';
    if (!productoId) return 'Selecciona el producto.';
    if (!cantidadNum || cantidadNum <= 0) return 'La cantidad debe ser mayor a cero.';
    if (!motivoFinal.trim()) return 'El motivo es obligatorio.';
    if (signo === '-' && stock !== null && cantidadNum > stock)
      return `Stock insuficiente: disponible ${stock}, solicitado ${cantidadNum}.`;
    return null;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const err = validar();
    if (err) { setLocalError(err); return; }
    setLocalError(null);
    await crear({
      warehouseId: bodegaId,
      productId: productoId,
      adjustmentQty: cantidadConSigno,
      reason: motivoFinal.trim(),
      notes: obs.trim() || null,
    });
  };

  if (!canCreate) return <NoAccessPage title="Nuevo Ajuste" />;

  const stockLabel = stock === null ? '—' : `${stock} disponibles`;
  const stockClassName = signo === '-' && stock !== null && cantidadNum > stock
    ? 'aj-stock-note--warning'
    : 'aj-stock-note--normal';

  return (
    <PageShell kicker="Inventario · Ajustes" title="Nuevo ajuste de inventario">
      <form onSubmit={(e) => void handleSubmit(e)}>
        <Card>
          {(error ?? localError) && (
            <ZHPageNotice variant="error" message="No se pudo crear el ajuste" detail={error ?? localError ?? ''} />
          )}

          <ZHFormSection title="Producto y bodega">
            <ZHGrid cols={2}>
              <ZHField label="Bodega" required>
                <select value={bodegaId} disabled={loading || loadingBodegas}
                  onChange={(e) => setBodegaId(e.target.value)}>
                  <option value="">— seleccionar —</option>
                  {(bodegas ?? []).map((b) => (
                    <option key={b.id} value={b.id}>{b.name}</option>
                  ))}
                </select>
              </ZHField>

              <ZHField label="Producto" required>
                <select value={productoId} disabled={loading}
                  onChange={(e) => setProductoId(e.target.value)}>
                  <option value="">— seleccionar —</option>
                  {(productos ?? []).map((p) => (
                    <option key={p.id} value={p.id}>{p.shortName}</option>
                  ))}
                </select>
              </ZHField>
            </ZHGrid>

            {(bodegaId && productoId) && (
              <p className={`aj-stock-note ${stockClassName}`}>
                Stock actual en bodega seleccionada: <strong>{stockLabel}</strong>
              </p>
            )}
          </ZHFormSection>

          <ZHFormSection title="Ajuste">
            <ZHGrid cols={3}>
              <ZHField label="Tipo" required>
                <select value={signo} disabled={loading}
                  onChange={(e) => setSigno(e.target.value as '+' | '-')}>
                  <option value="+">Incremento (agregar stock)</option>
                  <option value="-">Disminución (quitar stock)</option>
                </select>
              </ZHField>

              <ZHField label="Cantidad" required>
                <input
                  type="number" min={0.001} step={0.001}
                  value={cantidad} disabled={loading}
                  onChange={(e) => setCantidad(e.target.value)}
                  placeholder="0"
                  className={signo === '-' && stock !== null && cantidadNum > stock ? 'aj-input-error' : undefined}
                />
              </ZHField>

              <ZHField label="Motivo" required>
                <select value={motivo} disabled={loading}
                  onChange={(e) => setMotivo(e.target.value)}>
                  <option value="">— seleccionar —</option>
                  {MOTIVOS_PREDEFINIDOS.map((m) => (
                    <option key={m} value={m}>{m}</option>
                  ))}
                </select>
              </ZHField>

              {motivo === 'Otro' && (
                <ZHField label="Especificar motivo" required>
                  <input
                    value={motivoOtro} disabled={loading}
                    onChange={(e) => setMotivoOtro(e.target.value)}
                    maxLength={200}
                    placeholder="Describa el motivo"
                  />
                </ZHField>
              )}

              <ZHField label="Observaciones">
                <input
                  value={obs} disabled={loading}
                  onChange={(e) => setObs(e.target.value)}
                  maxLength={1000}
                  placeholder="Opcional"
                />
              </ZHField>
            </ZHGrid>
          </ZHFormSection>

          <div className="aj-create-actions">
            <ZHBtn variant="ghost" size="md" type="button" disabled={loading}
              onClick={() => navigate('/inventario/ajustes')}>
              Cancelar
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="submit" disabled={loading}>
              {loading ? 'Guardando…' : 'Crear ajuste'}
            </ZHBtn>
          </div>
        </Card>
      </form>
    </PageShell>
  );
}
