import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage, PageShell } from '../../../../components/PageShell';
import { Card } from '../../../../components/ui/Card';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useAsync } from '../../../../hooks/useAsync';
import { api } from '../../../lib/api';
import { ItemsTransferenciaGrid, itemRowsToRequest, type ItemRow } from '../components/ItemsTransferenciaGrid';
import { useBodegas, useTransferenciaAcciones } from '../hooks/useTransferencias';
import type { ApiResponse } from '../../../../types/api';
import './transferencias-pages.css';

interface ProductoOpcion {
  id: string;
  shortName: string;
  tracksStock: boolean;
  isActive: boolean;
}

export function CrearTransferenciaPage() {
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const canCreate = hasPerm('inventario.transferencias.create');

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

  const { loading, error, crear } = useTransferenciaAcciones(() => navigate('/inventario/transferencias'));

  const validar = () => {
    if (!bodegaOrigenId)   return 'Selecciona la bodega de origen.';
    if (!bodegaDestinoId)  return 'Selecciona la bodega de destino.';
    if (bodegaOrigenId === bodegaDestinoId) return 'Las bodegas de origen y destino deben ser diferentes.';
    const rows = itemRowsToRequest(items);
    if (rows.length === 0) return 'Agrega al menos un ítem con producto y cantidad.';
    const conStockInsuf = items.filter(
      (r) => r.productoId && r.stockDisponible !== null && r.cantidad > r.stockDisponible
    );
    if (conStockInsuf.length > 0)
      return `Stock insuficiente para: ${conStockInsuf.map((r) => r.descripcion).join(', ')}`;
    return null;
  };

  const [localError, setLocalError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const err = validar();
    if (err) { setLocalError(err); return; }
    setLocalError(null);
    await crear({
      bodegaOrigenId,
      bodegaDestinoId,
      motivo:       motivo.trim() || null,
      observaciones: observaciones.trim() || null,
      items: itemRowsToRequest(items),
    });
  };

  if (!canCreate) return <NoAccessPage title="Nueva Transferencia" />;

  return (
    <PageShell kicker="Inventario · Transferencias" title="Nueva Transferencia">
      <form onSubmit={(e) => void handleSubmit(e)}>
        <Card>
          {(error ?? localError) && (
            <ZHPageNotice variant="error" message="No se pudo crear la transferencia" detail={error ?? localError ?? ''} />
          )}

          <ZHFormSection title="Bodegas">
            <ZHGrid cols={2}>
              <ZHField label="Bodega origen" required>
                <select
                  value={bodegaOrigenId}
                  disabled={loading || loadingBodegas}
                  onChange={(e) => { setBodegaOrigenId(e.target.value); setItems([]); }}
                >
                  <option value="">— seleccionar —</option>
                  {(bodegas ?? []).map((b) => (
                    <option key={b.id} value={b.id}>{b.nombre}</option>
                  ))}
                </select>
              </ZHField>

              <ZHField label="Bodega destino" required>
                <select
                  value={bodegaDestinoId}
                  disabled={loading || loadingBodegas}
                  onChange={(e) => setBodegaDestinoId(e.target.value)}
                >
                  <option value="">— seleccionar —</option>
                  {(bodegas ?? [])
                    .filter((b) => b.id !== bodegaOrigenId)
                    .map((b) => (
                      <option key={b.id} value={b.id}>{b.nombre}</option>
                    ))}
                </select>
              </ZHField>

              <ZHField label="Motivo">
                <input
                  value={motivo}
                  disabled={loading}
                  onChange={(e) => setMotivo(e.target.value)}
                  placeholder="Ej. Reposición, reubicación…"
                  maxLength={500}
                />
              </ZHField>

              <ZHField label="Observaciones">
                <input
                  value={observaciones}
                  disabled={loading}
                  onChange={(e) => setObservaciones(e.target.value)}
                  placeholder="Opcional"
                  maxLength={1000}
                />
              </ZHField>
            </ZHGrid>
          </ZHFormSection>

          <ZHFormSection title="Ítems a transferir">
            <ItemsTransferenciaGrid
              bodegaOrigenId={bodegaOrigenId}
              items={items}
              onChange={setItems}
              disabled={loading || !bodegaOrigenId}
              productos={productos ?? []}
            />
          </ZHFormSection>

          <div className="trf-create-actions">
            <ZHBtn
              variant="ghost" size="md" type="button"
              disabled={loading}
              onClick={() => navigate('/inventario/transferencias')}
            >
              Cancelar
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="submit" disabled={loading}>
              {loading ? 'Guardando…' : 'Crear en Borrador'}
            </ZHBtn>
          </div>
        </Card>
      </form>
    </PageShell>
  );
}
