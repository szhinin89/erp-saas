import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHConfirmModal } from '../../../../components/zh/ZHConfirmModal';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { AjusteEstadoBadge, AjusteTipoBadge } from '../components/AjusteEstadoBadge';
import { useAjusteDetalle, useAjusteAcciones } from '../hooks/useAjustes';

function InfoItem({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="subtle" style={{ fontSize: 'var(--text-label-sm-size)', marginBottom: 2, textTransform: 'uppercase', letterSpacing: '0.04em' }}>{label}</p>
      <div style={{ fontWeight: 500 }}>{children}</div>
    </div>
  );
}

export function AjusteDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const hasPerm  = usePermissionsStore((s) => s.has);

  const canView    = hasPerm('inventario.ajustes.view');
  const canExecute = hasPerm('inventario.ajustes.execute');
  const canCancel  = hasPerm('inventario.ajustes.cancel');

  const { data: ajuste, loading, error, refetch } = useAjusteDetalle(id ?? null);
  const { loading: actLoading, error: actError, ejecutar, cancelar } = useAjusteAcciones(refetch);
  const [pendingAction, setPendingAction] = useState<'execute' | 'cancel' | null>(null);

  if (!canView) return <NoAccessPage title="Detalle de Ajuste" />;

  const esBorrador = ajuste?.status === 'Borrador';

  return (
    <div className="pg-page">

      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">Inventario</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item pg-breadcrumb-item--link" style={{ cursor: 'pointer' }}
              onClick={() => navigate('/inventario/ajustes')}>Ajustes</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">{ajuste?.adjustmentNumber ?? '…'}</span>
          </nav>
          <h1 className="pg-title">{loading ? 'Cargando…' : (ajuste?.adjustmentNumber ?? 'Ajuste')}</h1>
        </div>
        <div className="pg-header-right">
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
        </div>
      </div>

      {(error ?? actError) && (
        <ZHPageNotice variant="error" message="Error" detail={error ?? actError ?? ''} />
      )}

      {loading ? (
        <div style={{ padding: '40px' }}><LoadingState /></div>
      ) : !ajuste ? (
        <div style={{ padding: '40px' }}><EmptyState message="Ajuste no encontrado." /></div>
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
                <InfoItem label="Observaciones">{ajuste.notes}</InfoItem>
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
    </div>
  );
}
