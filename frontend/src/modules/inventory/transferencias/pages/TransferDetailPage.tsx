import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHConfirmModal } from '../../../../components/zh/ZHConfirmModal';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { TransferenciaEstadoBadge } from '../components/TransferStatusBadge';
import { useTransferDetail, useTransferActions } from '../hooks/useTransfers';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';

function InfoItem({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="subtle pg-info-item-label">{label}</p>
      <div className="pg-info-item-value">{children}</div>
    </div>
  );
}

export function TransferenciaDetailPage() {
  const { canShow } = usePermissionsUi();
  const { id }    = useParams<{ id: string }>();
  const navigate  = useNavigate();

  const canView    = canShow('inventory.transfers.view');
  const canConfirm = canShow('inventory.transfers.confirm');
  const canCancel  = canShow('inventory.transfers.cancel');

  const { data: transferencia, loading, error, refetch } = useTransferDetail(id ?? null);
  const { loading: actLoading, error: actError, confirmar, cancelar } = useTransferActions(refetch);
  const [cancelConfirmOpen, setCancelConfirmOpen] = useState(false);

  if (!canView) return <NoAccessPage title="Detalle de Transferencia" />;

  const esBorrador = transferencia?.status === 'Borrador';

  return (
    <ErpPageTemplate
      kicker="Inventario"
      title={loading ? 'Cargando…' : (transferencia?.transferNumber ?? 'Transferencia')}
      action={
        <>
          {esBorrador && (
            <>
              {canConfirm && (
                <ZHBtn variant="primary" size="md" disabled={actLoading}
                  onClick={() => void confirmar(id!)}>
                  {actLoading ? 'Procesando…' : 'Confirmar'}
                </ZHBtn>
              )}
              {canCancel && (
                <ZHBtn variant="destructive" size="md" disabled={actLoading}
                  onClick={() => setCancelConfirmOpen(true)}>
                  Cancelar
                </ZHBtn>
              )}
            </>
          )}
          <ZHBtn variant="ghost" size="md" onClick={() => navigate('/inventario/transferencias')}>← Volver</ZHBtn>
        </>
      }
    >
      {(error ?? actError) && (
        <ZHPageNotice variant="error" message="Error" detail={error ?? actError ?? ''} />
      )}

      {loading ? (
        <div className="pg-pad-40"><LoadingState /></div>
      ) : !transferencia ? (
        <div className="pg-pad-40"><EmptyState message="Transferencia no encontrada." /></div>
      ) : (
        <>
          <div className="pg-section pg-section--mb-4">
            <div className="pg-section-body">
              <div className="pg-form-grid pg-form-grid--3">
                <InfoItem label="Número">{transferencia.transferNumber}</InfoItem>
                <InfoItem label="Estado"><TransferenciaEstadoBadge estado={transferencia.status} /></InfoItem>
                <InfoItem label="Fecha">{new Date(transferencia.transferDate).toLocaleString('es')}</InfoItem>
                <InfoItem label="Bodega origen">{transferencia.sourceWarehouseName}</InfoItem>
                <InfoItem label="Bodega destino">{transferencia.destinationWarehouseName}</InfoItem>
                {transferencia.reason && <InfoItem label="Motivo">{transferencia.reason}</InfoItem>}
                {transferencia.notes  && <InfoItem label="notes">{transferencia.notes}</InfoItem>}
                {transferencia.confirmationDate && (
                  <InfoItem label="Confirmado el">
                    {new Date(transferencia.confirmationDate).toLocaleString('es')}
                  </InfoItem>
                )}
              </div>
            </div>
          </div>

          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">list_alt</span>
                <span className="pg-section-label">Ítems Transferidos</span>
              </div>
            </div>
            <div className="pg-overflow-x">
              <table className="table">
                <thead>
                  <tr>
                    <th>Producto</th>
                    <th className="pg-th-right">Cantidad</th>
                  </tr>
                </thead>
                <tbody>
                  {transferencia.detalles.map((d) => (
                    <tr key={d.id}>
                      <td>{d.description}</td>
                      <td className="mono pg-td-right">{d.quantity}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}

      {cancelConfirmOpen && (
        <ZHConfirmModal
          title="Cancelar transferencia"
          message="¿Cancelar esta transferencia?"
          confirmLabel="Cancelar transferencia"
          cancelLabel="Volver"
          variant="destructive"
          loading={actLoading}
          onCancel={() => setCancelConfirmOpen(false)}
          onConfirm={async () => {
            if (!id) return;
            await cancelar(id);
            setCancelConfirmOpen(false);
          }}
        />
      )}
    </ErpPageTemplate>
  );
}
