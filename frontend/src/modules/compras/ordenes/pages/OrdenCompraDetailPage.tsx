import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage, PageShell, TableCard } from '../../../../components/PageShell';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useOrdenCompraDetalle, useOrdenCompraAcciones } from '../hooks/useOrdenesCompra';
import type { EstadoOrdenCompra } from '../api/ordenCompraService';

const ESTADO_COLORS: Record<EstadoOrdenCompra, string> = {
  Borrador:         'var(--zh-text-muted)',
  Enviada:          '#2563eb',
  Aprobada:         '#16a34a',
  RecibidaParcial:  '#ca8a04',
  Cerrada:          '#6d28d9',
  Cancelada:        '#dc2626',
};

function EstadoBadge({ estado }: { estado: EstadoOrdenCompra }) {
  return (
    <span style={{
      fontSize: '0.875rem',
      fontWeight: 700,
      padding: '4px 12px',
      borderRadius: '99px',
      background: ESTADO_COLORS[estado] + '20',
      color: ESTADO_COLORS[estado],
    }}>
      {estado}
    </span>
  );
}

export function OrdenCompraDetailPage() {
  const { id }    = useParams<{ id: string }>();
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const canView   = hasPerm('compras.ordenes.view');

  const { data, loading, error, refetch } = useOrdenCompraDetalle(id ?? null);
  const acciones = useOrdenCompraAcciones(refetch);

  const [vincularFacturaId, setVincularFacturaId] = useState('');
  const [showVincularModal, setShowVincularModal] = useState(false);

  if (!canView) return <NoAccessPage title="Orden de Compra" />;
  if (loading) return <PageShell kicker="Compras" title="Cargando…"><LoadingState /></PageShell>;
  if (error || !data) return (
    <PageShell kicker="Compras" title="Orden de Compra">
      <EmptyState message={error ?? 'Orden no encontrada.'} />
    </PageShell>
  );

  const isActive     = !['Cerrada', 'Cancelada'].includes(data.estado);
  const canEnviar    = data.estado === 'Borrador'   && hasPerm('compras.ordenes.send');
  const canAprobar   = ['Borrador', 'Enviada'].includes(data.estado) && hasPerm('compras.ordenes.approve');
  const canCancelar  = isActive                     && hasPerm('compras.ordenes.cancel');
  const canVincular  = ['Aprobada', 'RecibidaParcial'].includes(data.estado) && hasPerm('compras.ordenes.link-invoice');

  const handleVincular = async () => {
    if (!vincularFacturaId.trim()) return;
    const ok = await acciones.vincularFactura(data.id, vincularFacturaId.trim());
    if (ok) { setShowVincularModal(false); setVincularFacturaId(''); }
  };

  return (
    <PageShell
      kicker="Compras"
      title={data.numeroOrden}
      action={
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
          {canEnviar   && <ZHBtn variant="secondary"    size="md" disabled={acciones.loading} onClick={() => acciones.enviar(data.id)}>Enviar</ZHBtn>}
          {canAprobar  && <ZHBtn variant="primary"      size="md" disabled={acciones.loading} onClick={() => acciones.aprobar(data.id)}>Aprobar</ZHBtn>}
          {canVincular && <ZHBtn variant="secondary"    size="md" disabled={acciones.loading} onClick={() => setShowVincularModal(true)}>Vincular factura</ZHBtn>}
          {canCancelar && <ZHBtn variant="destructive"  size="md" disabled={acciones.loading} onClick={() => acciones.cancelar(data.id)}>Cancelar OC</ZHBtn>}
        </div>
      }
    >
      {acciones.error && (
        <ZHPageNotice variant="error" message="Error" detail={acciones.error} />
      )}

      {/* Vincular factura modal inline */}
      {showVincularModal && (
        <div style={{
          position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)',
          display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000,
        }}>
          <div style={{
            background: 'var(--zh-bg)', borderRadius: '12px', padding: '24px',
            width: '440px', boxShadow: '0 8px 32px rgba(0,0,0,0.24)',
          }}>
            <h3 style={{ marginBottom: '16px' }}>Vincular factura de compra</h3>
            <p style={{ color: 'var(--zh-text-muted)', marginBottom: '12px', fontSize: '0.875rem' }}>
              Ingresa el ID de la factura de compra aprobada a vincular con esta orden.
            </p>
            <input
              type="text"
              placeholder="ID de la factura (UUID)"
              value={vincularFacturaId}
              onChange={(e) => setVincularFacturaId(e.target.value)}
              style={{ width: '100%', marginBottom: '16px' }}
            />
            <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
              <ZHBtn variant="ghost" size="md" onClick={() => { setShowVincularModal(false); setVincularFacturaId(''); }}>
                Cancelar
              </ZHBtn>
              <ZHBtn variant="primary" size="md" disabled={acciones.loading || !vincularFacturaId.trim()} onClick={handleVincular}>
                {acciones.loading ? 'Vinculando…' : 'Vincular'}
              </ZHBtn>
            </div>
          </div>
        </div>
      )}

      <TableCard>
        {/* Encabezado */}
        <div style={{ display: 'flex', gap: '32px', flexWrap: 'wrap', marginBottom: '24px' }}>
          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--zh-text-muted)', marginBottom: '2px' }}>Estado</div>
            <EstadoBadge estado={data.estado} />
          </div>
          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--zh-text-muted)', marginBottom: '2px' }}>Proveedor</div>
            <div style={{ fontWeight: 600 }}>{data.proveedorNombre}</div>
          </div>
          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--zh-text-muted)', marginBottom: '2px' }}>Fecha emisión</div>
            <div>{new Date(data.fechaEmision).toLocaleDateString()}</div>
          </div>
          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--zh-text-muted)', marginBottom: '2px' }}>Fecha requerida</div>
            <div>{new Date(data.fechaRequerida).toLocaleDateString()}</div>
          </div>
          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--zh-text-muted)', marginBottom: '2px' }}>Total</div>
            <div style={{ fontSize: '1.25rem', fontWeight: 700 }}>${data.total.toFixed(2)}</div>
          </div>
        </div>

        {data.observaciones && (
          <div style={{ marginBottom: '16px', color: 'var(--zh-text-muted)', fontSize: '0.875rem' }}>
            <strong>Observaciones:</strong> {data.observaciones}
          </div>
        )}

        {/* Líneas */}
        <h4 style={{ marginBottom: '8px' }}>Líneas de la orden</h4>
        <table className="table" style={{ marginBottom: '24px' }}>
          <thead>
            <tr>
              <th>Descripción</th>
              <th>Cant. pedida</th>
              <th>Cant. facturada</th>
              <th>Pendiente</th>
              <th>Precio unit.</th>
              <th>Subtotal</th>
              <th>IVA</th>
              <th>Total</th>
            </tr>
          </thead>
          <tbody>
            {data.detalles.map((d) => (
              <tr key={d.id}>
                <td>{d.descripcion}</td>
                <td>{d.cantidadPedida}</td>
                <td>{d.cantidadFacturada}</td>
                <td style={{ color: d.pendienteFacturar > 0 ? '#ca8a04' : '#16a34a', fontWeight: 600 }}>
                  {d.pendienteFacturar}
                </td>
                <td>${d.precioUnitario.toFixed(4)}</td>
                <td>${d.subtotal.toFixed(2)}</td>
                <td>${d.impuesto.toFixed(2)}</td>
                <td>${d.total.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>

        {/* Facturas vinculadas */}
        {data.facturasVinculadas.length > 0 && (
          <>
            <h4 style={{ marginBottom: '8px' }}>Facturas vinculadas</h4>
            <table className="table">
              <thead>
                <tr>
                  <th>Número de factura</th>
                  <th>Fecha vinculación</th>
                </tr>
              </thead>
              <tbody>
                {data.facturasVinculadas.map((fv) => (
                  <tr key={fv.compraFacturaId}>
                    <td><strong>{fv.numeroFactura}</strong></td>
                    <td>{new Date(fv.fechaVinculacion).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
      </TableCard>

      <div style={{ marginTop: '12px' }}>
        <ZHBtn variant="ghost" size="sm" onClick={() => navigate('/compras/ordenes')}>
          ← Volver a la lista
        </ZHBtn>
      </div>
    </PageShell>
  );
}
