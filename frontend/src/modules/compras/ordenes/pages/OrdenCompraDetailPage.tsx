import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useOrdenCompraDetalle, useOrdenCompraAcciones } from '../hooks/useOrdenesCompra';
import type { EstadoOrdenCompra } from '../api/ordenCompraService';

function estadoBadgeClass(estado: EstadoOrdenCompra): string {
  const map: Record<string, string> = {
    borrador: 'badge--gray', enviada: 'badge--orange', aprobada: 'badge--green',
    recibidaparcial: 'badge--blue', cerrada: 'badge--gray', cancelada: 'badge--red',
  };
  return `badge badge--md ${map[estado.toLowerCase()] ?? 'badge--gray'}`;
}

function InfoItem({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="subtle" style={{ fontSize: 'var(--text-label-sm-size)', marginBottom: 2, textTransform: 'uppercase', letterSpacing: '0.04em' }}>{label}</p>
      <div style={{ fontWeight: 500 }}>{children}</div>
    </div>
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

  const isActive    = !['Cerrada', 'Cancelada'].includes(data?.estado ?? '');
  const canEnviar   = data?.estado === 'Borrador' && hasPerm('compras.ordenes.send');
  const canAprobar  = ['Borrador', 'Enviada'].includes(data?.estado ?? '') && hasPerm('compras.ordenes.approve');
  const canCancelar = isActive && hasPerm('compras.ordenes.cancel');
  const canVincular = ['Aprobada', 'RecibidaParcial'].includes(data?.estado ?? '') && hasPerm('compras.ordenes.link-invoice');

  const handleVincular = async () => {
    if (!vincularFacturaId.trim()) return;
    const ok = await acciones.vincularFactura(data!.id, vincularFacturaId.trim());
    if (ok) { setShowVincularModal(false); setVincularFacturaId(''); }
  };

  return (
    <div className="pg-page">

      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">Compras</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item pg-breadcrumb-item--link" style={{ cursor: 'pointer' }}
              onClick={() => navigate('/compras/ordenes')}>Órdenes de Compra</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">{data?.numeroOrden ?? '…'}</span>
          </nav>
          <h1 className="pg-title">{loading ? 'Cargando…' : (data?.numeroOrden ?? 'Orden de Compra')}</h1>
        </div>
        <div className="pg-header-right">
          {data && (
            <>
              {canEnviar   && <ZHBtn variant="secondary"   size="md" disabled={acciones.loading} onClick={() => acciones.enviar(data.id)}>Enviar</ZHBtn>}
              {canAprobar  && <ZHBtn variant="primary"     size="md" disabled={acciones.loading} onClick={() => acciones.aprobar(data.id)}>Aprobar</ZHBtn>}
              {canVincular && <ZHBtn variant="secondary"   size="md" disabled={acciones.loading} onClick={() => setShowVincularModal(true)}>Vincular factura</ZHBtn>}
              {canCancelar && <ZHBtn variant="destructive" size="md" disabled={acciones.loading} onClick={() => acciones.cancelar(data.id)}>Cancelar OC</ZHBtn>}
            </>
          )}
          <ZHBtn variant="ghost" size="md" onClick={() => navigate('/compras/ordenes')}>← Volver</ZHBtn>
        </div>
      </div>

      {acciones.error && <ZHPageNotice variant="error" message="Error" detail={acciones.error} />}
      {error          && <ZHPageNotice variant="error" message="Error al cargar" detail={error} />}

      {loading ? (
        <div style={{ padding: '40px' }}><LoadingState /></div>
      ) : !data ? (
        <div style={{ padding: '40px' }}><EmptyState message="Orden no encontrada." /></div>
      ) : (
        <>
          {/* Summary */}
          <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
            <div className="pg-section-body">
              <div className="pg-form-grid pg-form-grid--4">
                <InfoItem label="Estado">
                  <span className={estadoBadgeClass(data.estado)}>{data.estado}</span>
                </InfoItem>
                <InfoItem label="Proveedor">{data.proveedorNombre}</InfoItem>
                <InfoItem label="Fecha emisión">{new Date(data.fechaEmision).toLocaleDateString('es')}</InfoItem>
                <InfoItem label="Fecha requerida">{new Date(data.fechaRequerida).toLocaleDateString('es')}</InfoItem>
                <InfoItem label="Total">
                  <span className="mono" style={{ fontSize: 18, fontWeight: 700, color: 'var(--color-primary)' }}>
                    ${data.total.toFixed(2)}
                  </span>
                </InfoItem>
                {data.observaciones && (
                  <div style={{ gridColumn: '1 / -1' }}>
                    <p className="subtle" style={{ fontSize: 'var(--text-label-sm-size)', marginBottom: 2, textTransform: 'uppercase' }}>Observaciones</p>
                    <p>{data.observaciones}</p>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Lines */}
          <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">list_alt</span>
                <span className="pg-section-label">Líneas de la Orden</span>
              </div>
            </div>
            <div style={{ overflowX: 'auto' }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>Descripción</th>
                    <th style={{ textAlign: 'right' }}>Cant. pedida</th>
                    <th style={{ textAlign: 'right' }}>Cant. facturada</th>
                    <th style={{ textAlign: 'right' }}>Pendiente</th>
                    <th style={{ textAlign: 'right' }}>Precio unit.</th>
                    <th style={{ textAlign: 'right' }}>Subtotal</th>
                    <th style={{ textAlign: 'right' }}>IVA</th>
                    <th style={{ textAlign: 'right' }}>Total</th>
                  </tr>
                </thead>
                <tbody>
                  {data.detalles.map((d) => (
                    <tr key={d.id}>
                      <td>{d.descripcion}</td>
                      <td style={{ textAlign: 'right' }} className="mono">{d.cantidadPedida}</td>
                      <td style={{ textAlign: 'right' }} className="mono">{d.cantidadFacturada}</td>
                      <td style={{ textAlign: 'right', color: d.pendienteFacturar > 0 ? 'var(--color-warning)' : 'var(--color-success)' }}
                        className="mono">{d.pendienteFacturar}</td>
                      <td style={{ textAlign: 'right' }} className="mono">${d.precioUnitario.toFixed(4)}</td>
                      <td style={{ textAlign: 'right' }} className="mono">${d.subtotal.toFixed(2)}</td>
                      <td style={{ textAlign: 'right' }} className="mono">${d.impuesto.toFixed(2)}</td>
                      <td style={{ textAlign: 'right', fontWeight: 600 }} className="mono">${d.total.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Linked invoices */}
          {data.facturasVinculadas.length > 0 && (
            <div className="pg-section">
              <div className="pg-section-header">
                <div className="pg-section-header-left">
                  <span className="material-symbols-outlined pg-section-icon">receipt_long</span>
                  <span className="pg-section-label">Facturas Vinculadas</span>
                </div>
              </div>
              <div style={{ overflowX: 'auto' }}>
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
                        <td>{new Date(fv.fechaVinculacion).toLocaleDateString('es')}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </>
      )}

      {/* Vincular factura modal */}
      {showVincularModal && (
        <div
          className="zh-modal-overlay"
          role="dialog"
          aria-modal="true"
          onClick={(e) => { if (e.target === e.currentTarget) setShowVincularModal(false); }}
        >
          <div className="zh-modal" style={{ maxWidth: 480 }}>
            <div className="zh-modal-header">
              <h2 className="zh-modal-title">Vincular factura de compra</h2>
              <button type="button" className="zh-modal-close"
                onClick={() => setShowVincularModal(false)} aria-label="Cerrar">✕</button>
            </div>
            <div className="zh-modal-body">
              <p style={{ fontSize: 'var(--text-body-md-size)', color: 'var(--color-text-secondary)', marginBottom: 'var(--space-4)' }}>
                Ingresa el ID de la factura de compra aprobada a vincular con esta orden.
              </p>
              <input
                className="zh-input"
                type="text"
                placeholder="ID de la factura (UUID)"
                value={vincularFacturaId}
                onChange={(e) => setVincularFacturaId(e.target.value)}
              />
            </div>
            <div className="pg-actions-bar">
              <div className="pg-actions-info" />
              <div className="pg-actions-buttons">
                <ZHBtn variant="ghost" size="md" onClick={() => { setShowVincularModal(false); setVincularFacturaId(''); }}>
                  Cancelar
                </ZHBtn>
                <ZHBtn variant="primary" size="md"
                  disabled={acciones.loading || !vincularFacturaId.trim()}
                  onClick={() => void handleVincular()}>
                  {acciones.loading ? 'Vinculando…' : 'Vincular'}
                </ZHBtn>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
