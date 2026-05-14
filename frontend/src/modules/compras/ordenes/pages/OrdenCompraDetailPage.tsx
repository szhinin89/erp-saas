import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage, PageShell } from '../../../../components/PageShell';
import { Card } from '../../../../components/ui/Card';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useOrdenCompraDetalle, useOrdenCompraAcciones } from '../hooks/useOrdenesCompra';
import type { EstadoOrdenCompra } from '../api/ordenCompraService';
import './ordenes-compra-pages.css';

function estadoClass(estado: EstadoOrdenCompra) {
  return `oc-status-badge--${estado.toLowerCase()}`;
}

function EstadoBadge({ estado }: { estado: EstadoOrdenCompra }) {
  return (
    <span className={`oc-status-badge oc-status-badge--md ${estadoClass(estado)}`}>
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
        <div className="oc-detail-actions">
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
        <div className="oc-modal-overlay">
          <div className="oc-modal-card">
            <h3 className="oc-modal-title">Vincular factura de compra</h3>
            <p className="oc-modal-help">
              Ingresa el ID de la factura de compra aprobada a vincular con esta orden.
            </p>
            <input
              type="text"
              placeholder="ID de la factura (UUID)"
              value={vincularFacturaId}
              onChange={(e) => setVincularFacturaId(e.target.value)}
              className="oc-modal-input"
            />
            <div className="oc-modal-actions">
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

      <Card>
        {/* Encabezado */}
        <div className="oc-detail-header">
          <div>
            <div className="oc-detail-label">Estado</div>
            <EstadoBadge estado={data.estado} />
          </div>
          <div>
            <div className="oc-detail-label">Proveedor</div>
            <div className="oc-detail-strong">{data.proveedorNombre}</div>
          </div>
          <div>
            <div className="oc-detail-label">Fecha emisión</div>
            <div>{new Date(data.fechaEmision).toLocaleDateString()}</div>
          </div>
          <div>
            <div className="oc-detail-label">Fecha requerida</div>
            <div>{new Date(data.fechaRequerida).toLocaleDateString()}</div>
          </div>
          <div>
            <div className="oc-detail-label">Total</div>
            <div className="oc-detail-total">${data.total.toFixed(2)}</div>
          </div>
        </div>

        {data.observaciones && (
          <div className="oc-detail-observations">
            <strong>Observaciones:</strong> {data.observaciones}
          </div>
        )}

        {/* Líneas */}
        <h4 className="oc-section-title">Líneas de la orden</h4>
        <table className="table oc-section-table oc-responsive-table">
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
                <td data-label="Descripción">{d.descripcion}</td>
                <td data-label="Cant. pedida">{d.cantidadPedida}</td>
                <td data-label="Cant. facturada">{d.cantidadFacturada}</td>
                <td data-label="Pendiente" className={d.pendienteFacturar > 0 ? 'oc-pending--warning' : 'oc-pending--ok'}>
                  {d.pendienteFacturar}
                </td>
                <td data-label="Precio unit.">${d.precioUnitario.toFixed(4)}</td>
                <td data-label="Subtotal">${d.subtotal.toFixed(2)}</td>
                <td data-label="IVA">${d.impuesto.toFixed(2)}</td>
                <td data-label="Total">${d.total.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>

        {/* Facturas vinculadas */}
        {data.facturasVinculadas.length > 0 && (
          <>
            <h4 className="oc-section-title">Facturas vinculadas</h4>
            <table className="table oc-responsive-table">
              <thead>
                <tr>
                  <th>Número de factura</th>
                  <th>Fecha vinculación</th>
                </tr>
              </thead>
              <tbody>
                {data.facturasVinculadas.map((fv) => (
                  <tr key={fv.compraFacturaId}>
                    <td data-label="Número de factura"><strong>{fv.numeroFactura}</strong></td>
                    <td data-label="Fecha vinculación">{new Date(fv.fechaVinculacion).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
      </Card>

      <div className="oc-back-wrap">
        <ZHBtn variant="ghost" size="sm" onClick={() => navigate('/compras/ordenes')}>
          ← Volver a la lista
        </ZHBtn>
      </div>
    </PageShell>
  );
}
