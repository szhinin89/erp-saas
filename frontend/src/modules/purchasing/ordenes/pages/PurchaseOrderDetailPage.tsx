import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePurchaseOrderDetail, usePurchaseOrderActions } from '../hooks/usePurchaseOrders';
import type { PurchaseOrderStatus } from '../api/purchaseOrderService';
import './orden-compra-page.css';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import { formatDate } from '../../../../lib/formatters/dateFormatters';

function estadoBadgeClass(estado: PurchaseOrderStatus): string {
  const map: Record<string, string> = {
    borrador: 'badge--gray', enviada: 'badge--orange', aprobada: 'badge--green',
    recibidaparcial: 'badge--blue', cerrada: 'badge--gray', cancelada: 'badge--red',
  };
  return `badge badge--md ${map[estado.toLowerCase()] ?? 'badge--gray'}`;
}

function InfoItem({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="subtle pg-info-item-label">{label}</p>
      <div className="pg-info-item-value">{children}</div>
    </div>
  );
}

export function OrdenCompraDetailPage() {
  const { canShow } = usePermissionsUi();
  const { id }    = useParams<{ id: string }>();
  const navigate  = useNavigate();
  const canView   = canShow('purchases.orders.view');

  const { data, loading, error, refetch } = usePurchaseOrderDetail(id ?? null);
  const acciones = usePurchaseOrderActions(refetch);

  const [vincularFacturaId, setVincularFacturaId] = useState('');
  const [showVincularModal, setShowVincularModal] = useState(false);

  if (!canView) return <NoAccessPage title="Orden de Compra" />;

  const isActive    = !['Cerrada', 'Cancelada'].includes(data?.status ?? '');
  const canEnviar   = data?.status === 'Borrador' && canShow('purchases.orders.send');
  const canAprobar  = ['Borrador', 'Enviada'].includes(data?.status ?? '') && canShow('purchases.orders.approve');
  const canCancelar = isActive && canShow('purchases.orders.cancel');
  const canVincular = ['Aprobada', 'RecibidaParcial'].includes(data?.status ?? '') && canShow('purchases.orders.link-invoice');

  const handleVincular = async () => {
    if (!vincularFacturaId.trim()) return;
    const ok = await acciones.linkInvoice(data!.id, vincularFacturaId.trim());
    if (ok) { setShowVincularModal(false); setVincularFacturaId(''); }
  };

  return (
    <ErpPageTemplate
      kicker="Compras"
      title={loading ? 'Cargando…' : (data?.orderNumber ?? 'Orden de Compra')}
      action={
        <>
          {data && (
            <>
              {canEnviar   && <ZHBtn variant="secondary"   size="md" disabled={acciones.loading} onClick={() => acciones.send(data.id)}>Enviar</ZHBtn>}
              {canAprobar  && <ZHBtn variant="primary"     size="md" disabled={acciones.loading} onClick={() => acciones.approve(data.id)}>Aprobar</ZHBtn>}
              {canVincular && <ZHBtn variant="secondary"   size="md" disabled={acciones.loading} onClick={() => setShowVincularModal(true)}>Vincular factura</ZHBtn>}
              {canCancelar && <ZHBtn variant="destructive" size="md" disabled={acciones.loading} onClick={() => acciones.cancel(data.id)}>Cancelar OC</ZHBtn>}
            </>
          )}
          <ZHBtn variant="ghost" size="md" onClick={() => navigate('/compras/ordenes')}>← Volver</ZHBtn>
        </>
      }
    >
      {acciones.error && <ZHPageNotice variant="error" message="Error" detail={acciones.error} />}
      {error          && <ZHPageNotice variant="error" message="Error al cargar" detail={error} />}

      {loading ? (
        <div className="pg-pad-40"><LoadingState /></div>
      ) : !data ? (
        <div className="pg-pad-40"><EmptyState message="Orden no encontrada." /></div>
      ) : (
        <>
          {/* Summary */}
          <div className="pg-section pg-section--mb-4">
            <div className="pg-section-body">
              <div className="pg-form-grid pg-form-grid--4">
                <InfoItem label="Estado">
                  <span className={estadoBadgeClass(data.status)}>{data.status}</span>
                </InfoItem>
                <InfoItem label="Proveedor">{data.supplierName}</InfoItem>
                <InfoItem label="Fecha emisión">{formatDate(data.issueDate)}</InfoItem>
                <InfoItem label="Fecha requerida">{formatDate(data.requiredDate)}</InfoItem>
                <InfoItem label="Total">
                  <span className="mono pg-doc-hero-mono">
                    ${data.total.toFixed(2)}
                  </span>
                </InfoItem>
                {data.notes && (
                  <div className="oc-doc-notes">
                    <p className="subtle pg-doc-notes-label">notes</p>
                    <p>{data.notes}</p>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Lines */}
          <div className="pg-section pg-section--mb-4">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">list_alt</span>
                <span className="pg-section-label">Líneas de la Orden</span>
              </div>
            </div>
            <div className="pg-overflow-x">
              <table className="table">
                <thead>
                  <tr>
                    <th>Descripción</th>
                    <th className="pg-th-right">Cant. pedida</th>
                    <th className="pg-th-right">Cant. facturada</th>
                    <th className="pg-th-right">Pendiente</th>
                    <th className="pg-th-right">Precio unit.</th>
                    <th className="pg-th-right">Subtotal</th>
                    <th className="pg-th-right">IVA</th>
                    <th className="pg-th-right">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {data.detalles.map((d) => (
                    <tr key={d.id}>
                      <td>{d.description}</td>
                      <td className="mono pg-td-right">{d.orderedQuantity}</td>
                      <td className="mono pg-td-right">{d.invoicedQuantity}</td>
                      <td className={`mono pg-td-right ${d.pendingBillingQuantity > 0 ? 'pg-cell-warn' : 'pg-cell-success'}`}>{d.pendingBillingQuantity}</td>
                      <td className="mono pg-td-right">${d.unitPrice.toFixed(4)}</td>
                      <td className="mono pg-td-right">${d.subtotal.toFixed(2)}</td>
                      <td className="mono pg-td-right">${d.impuesto.toFixed(2)}</td>
                      <td className="mono pg-td-right pg-cell-strong">${d.total.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Linked invoices */}
          {data.linkedInvoices.length > 0 && (
            <div className="pg-section">
              <div className="pg-section-header">
                <div className="pg-section-header-left">
                  <span className="material-symbols-outlined pg-section-icon">receipt_long</span>
                  <span className="pg-section-label">Facturas Vinculadas</span>
                </div>
              </div>
              <div className="pg-overflow-x">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Número de factura</th>
                      <th>Fecha vinculación</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.linkedInvoices.map((fv) => (
                      <tr key={fv.purchBillId}>
                        <td><strong>{fv.invoiceNumber}</strong></td>
                        <td>{formatDate(fv.linkedDate)}</td>
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
          <div className="zh-modal pg-modal--480">
            <div className="zh-modal-header">
              <h2 className="zh-modal-title">Vincular factura de compra</h2>
              <button type="button" className="zh-modal-close"
                onClick={() => setShowVincularModal(false)} aria-label="Cerrar">✕</button>
            </div>
            <div className="zh-modal-body">
              <p className="pg-modal-hint">
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
    </ErpPageTemplate>
  );
}
