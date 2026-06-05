import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHConfirmModal } from '../../../../components/zh/ZHConfirmModal';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { AjusteEstadoBadge, AjusteTipoBadge } from ../components/AdjustmentStatusBadge';
import { useAdjustmentDetail, useAdjustmentActions } from '../hooks/useAdjustments';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';

function InfoItem({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="subtle pg-info-item-label">{label}</p>
      <div className="pg-info-item-value">{children}</div>
    </div>
  );
}

export function AjusteDetailPage() {
  const { canShow } = usePermissionsUi();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const canView    = canShow('inventory.adjustments.view');
  const canExecute = canShow('inventory.adjustments.execute');
  const canCancel  = canShow('inventory.adjustments.cancel');

  const { data: ajuste, loading, error, refetch } = useAdjustmentDetail(id ?? null);
  const { loading: actLoading, error: actError, ejecutar, cancelar } = useAdjustmentActions(refetch);
  const [pendingAction, setPendingAction] = useState<'execute' | 'cancel' | null>(null);

  if (!canView) return <NoAccessPage title="Detalle de Ajuste" />;

  const esBorrador = ajuste?.status === 'Borrador';

  return (
    <ErpPageTemplate
      kicker="Inventario"
      title={loading ? 'Cargando…' : (ajuste?.adjustmentNumber ?? 'Ajuste')}
      action={
        <>
          {esBorrador && (
            <>
              {canExecute && (
                <ZHBtn variant="primary" size="md" disabled={actLoading}
                  onClick={() => setPendingAction('execute')}>
                  {actLoading ? 'Procesando…' : 'Ejecutar'}
                </ZHBtn>
              )}
              {canCancel && (
                <ZHBtn variant="destructive" size="md" disabled={actLoading}
                  onClick={() => setPendingAction('cancel')}>
                  Cancelar
                </ZHBtn>
              )}
            </>
          )}
          <ZHBtn variant="ghost" size="md" onClick={() => navigate('/inventario/ajustes')}>← Volver</ZHBtn>
        </>
      }
    >
      {(error ?? actError) && (
        <ZHPageNotice variant="error" message="Error" detail={error ?? actError ?? ''} />
      )}

      {loading ? (
        <div className="pg-pad-40"><LoadingState /></div>
      ) : !ajuste ? (
        <div className="pg-pad-40"><EmptyState message="Ajuste no encontrado." /></div>
      ) : (
        <div className="pg-section">
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--3">
              <InfoItem label="Número">{ajuste.adjustmentNumber}</InfoItem>
              <InfoItem label="Estado"><AjusteEstadoBadge estado={ajuste.status} /></InfoItem>
              <InfoItem label="Tipo">
                <AjusteTipoBadge tipo={ajuste.adjustmentType} cantidad={Math.abs(ajuste.adjustmentQuantity)} />
              </InfoItem>
              <InfoItem label="Producto">{ajuste.productName}</InfoItem>
              <InfoItem label="Bodega">{ajuste.warehouseName}</InfoItem>
              <InfoItem label="Motivo">{ajuste.reason}</InfoItem>
              <InfoItem label="Fecha">{new Date(ajuste.adjustmentDate).toLocaleString('es')}</InfoItem>
              {ajuste.notes && (
                <InfoItem label="notes">{ajuste.notes}</InfoItem>
              )}
              {ajuste.executionDate && (
                <InfoItem label="Ejecutado el">{new Date(ajuste.executionDate).toLocaleString('es')}</InfoItem>
              )}
            </div>
          </div>
        </div>
      )}

      {pendingAction && (
        <ZHConfirmModal
          title={pendingAction === 'execute' ? 'Ejecutar ajuste' : 'Cancelar ajuste'}
          message={
            pendingAction === 'execute'
              ? '¿Ejecutar este ajuste? El stock se actualizará inmediatamente.'
              : '¿Cancelar este ajuste?'
          }
          variant={pendingAction === 'execute' ? 'primary' : 'destructive'}
          confirmLabel={pendingAction === 'execute' ? 'Ejecutar' : 'Cancelar ajuste'}
          cancelLabel="Volver"
          loading={actLoading}
          onCancel={() => setPendingAction(null)}
          onConfirm={async () => {
            if (!id) return;
            if (pendingAction === 'execute') await ejecutar(id);
            else                             await cancelar(id);
            setPendingAction(null);
          }}
        />
      )}
    </ErpPageTemplate>
  );
}
