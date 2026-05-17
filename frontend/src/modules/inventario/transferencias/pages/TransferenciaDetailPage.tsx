import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHConfirmModal } from '../../../../components/zh/ZHConfirmModal';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { TransferenciaEstadoBadge } from '../components/TransferenciaEstadoBadge';
import { useTransferenciaDetalle, useTransferenciaAcciones } from '../hooks/useTransferencias';

function InfoItem({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="subtle" style={{ fontSize: 'var(--text-label-sm-size)', marginBottom: 2, textTransform: 'uppercase', letterSpacing: '0.04em' }}>{label}</p>
      <div style={{ fontWeight: 500 }}>{children}</div>
    </div>
  );
}

export function TransferenciaDetailPage() {
  const { id }    = useParams<{ id: string }>();
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);

  const canView    = hasPerm('inventario.transferencias.view');
  const canConfirm = hasPerm('inventario.transferencias.confirm');
  const canCancel  = hasPerm('inventario.transferencias.cancel');

  const { data: transferencia, loading, error, refetch } = useTransferenciaDetalle(id ?? null);
  const { loading: actLoading, error: actError, confirmar, cancelar } = useTransferenciaAcciones(refetch);
  const [cancelConfirmOpen, setCancelConfirmOpen] = useState(false);

  if (!canView) return <NoAccessPage title="Detalle de Transferencia" />;

  const esBorrador = transferencia?.status === 'Borrador';

  return (
    <div className="pg-page">

      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">Inventario</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item pg-breadcrumb-item--link" style={{ cursor: 'pointer' }}
              onClick={() => navigate('/inventario/transferencias')}>Transferencias</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">{transferencia?.transferNumber ?? '…'}</span>
          </nav>
          <h1 className="pg-title">{loading ? 'Cargando…' : (transferencia?.transferNumber ?? 'Transferencia')}</h1>
        </div>
        <div className="pg-header-right">
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
        </div>
      </div>

      {(error ?? actError) && (
        <ZHPageNotice variant="error" message="Error" detail={error ?? actError ?? ''} />
      )}

      {loading ? (
        <div style={{ padding: '40px' }}><LoadingState /></div>
      ) : !transferencia ? (
        <div style={{ padding: '40px' }}><EmptyState message="Transferencia no encontrada." /></div>
      ) : (
        <>
          <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
            <div className="pg-section-body">
              <div className="pg-form-grid pg-form-grid--3">
                <InfoItem label="Número">{transferencia.transferNumber}</InfoItem>
                <InfoItem label="Estado"><TransferenciaEstadoBadge estado={transferencia.status} /></InfoItem>
                <InfoItem label="Fecha">{new Date(transferencia.transferDate).toLocaleString('es')}</InfoItem>
                <InfoItem label="Bodega origen">{transferencia.sourceWarehouseName}</InfoItem>
                <InfoItem label="Bodega destino">{transferencia.destinationWarehouseName}</InfoItem>
                {transferencia.reason && <InfoItem label="Motivo">{transferencia.reason}</InfoItem>}
                {transferencia.notes  && <InfoItem label="Observaciones">{transferencia.notes}</InfoItem>}
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
            <div style={{ overflowX: 'auto' }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>Producto</th>
                    <th style={{ textAlign: 'right' }}>Cantidad</th>
                  </tr>
                </thead>
                <tbody>
                  {transferencia.detalles.map((d) => (
                    <tr key={d.id}>
                      <td>{d.description}</td>
                      <td style={{ textAlign: 'right' }} className="mono">{d.quantity}</td>
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
    </div>
  );
}
