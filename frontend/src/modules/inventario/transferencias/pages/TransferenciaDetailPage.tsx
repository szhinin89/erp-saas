import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage, PageShell } from '../../../../components/PageShell';
import { Card } from '../../../../components/ui/Card';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHConfirmModal } from '../../../../components/zh/ZHConfirmModal';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { TransferenciaEstadoBadge } from '../components/TransferenciaEstadoBadge';
import { useTransferenciaDetalle, useTransferenciaAcciones } from '../hooks/useTransferencias';
import './transferencias-pages.css';

export function TransferenciaDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);

  const canView     = hasPerm('inventario.transferencias.view');
  const canConfirm  = hasPerm('inventario.transferencias.confirm');
  const canCancel   = hasPerm('inventario.transferencias.cancel');

  const { data: transferencia, loading, error, refetch } = useTransferenciaDetalle(id ?? null);

  const { loading: actLoading, error: actError, confirmar, cancelar } =
    useTransferenciaAcciones(refetch);
  const [cancelConfirmOpen, setCancelConfirmOpen] = useState(false);

  if (!canView) return <NoAccessPage title="Detalle de Transferencia" />;

  const esBorrador = transferencia?.status === 'Borrador';

  return (
    <PageShell
      kicker="Inventario · Transferencias"
      title={transferencia?.numeroTransferencia ?? 'Cargando…'}
      action={
        esBorrador ? (
          <div className="trf-action-row">
            {canConfirm && (
              <ZHBtn
                variant="primary" size="md"
                disabled={actLoading}
                onClick={() => void confirmar(id!)}
              >
                {actLoading ? 'Procesando…' : 'Confirmar'}
              </ZHBtn>
            )}
            {canCancel && (
              <ZHBtn
                variant="destructive" size="md"
                disabled={actLoading}
                onClick={() => {
                  setCancelConfirmOpen(true);
                }}
              >
                Cancelar
              </ZHBtn>
            )}
          </div>
        ) : undefined
      }
    >
      <Card>
        {(error ?? actError) && (
          <ZHPageNotice variant="error" message="Error" detail={error ?? actError ?? ''} />
        )}

        {loading ? (
          <LoadingState />
        ) : !transferencia ? (
          <EmptyState message="Transferencia no encontrada." />
        ) : (
          <>
            {/* Encabezado */}
            <div className="trf-detail-grid">
              <Dato label="Número"  valor={transferencia.numeroTransferencia} />
              <Dato label="Estado"  valor={<TransferenciaEstadoBadge estado={transferencia.status} />} />
              <Dato label="Fecha"   valor={new Date(transferencia.fechaTransferencia).toLocaleString()} />
              <Dato label="Bodega origen"  valor={transferencia.bodegaOrigenNombre} />
              <Dato label="Bodega destino" valor={transferencia.bodegaDestinoNombre} />
              {transferencia.reason && <Dato label="Motivo" valor={transferencia.reason} />}
              {transferencia.notes && <Dato label="Observaciones" valor={transferencia.notes} />}
              {transferencia.fechaConfirmacion && (
                <Dato
                  label="Confirmado el"
                  valor={new Date(transferencia.fechaConfirmacion).toLocaleString()}
                />
              )}
            </div>

            {/* Ítems */}
            <h3 className="trf-detail-items-title">Ítems</h3>
            <table className="table trf-responsive-table">
              <thead>
                <tr>
                  <th>Producto</th>
                  <th className="trf-text-right">Cantidad</th>
                </tr>
              </thead>
              <tbody>
                {transferencia.detalles.map((d) => (
                  <tr key={d.id}>
                    <td data-label="Producto">{d.description}</td>
                    <td data-label="Cantidad" className="trf-text-right">{d.quantity}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            <div className="trf-detail-footer">
              <ZHBtn variant="ghost" size="md" onClick={() => navigate('/inventario/transferencias')}>
                ← Volver al listado
              </ZHBtn>
            </div>
          </>
        )}
      </Card>
      {cancelConfirmOpen ? (
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
      ) : null}
    </PageShell>
  );
}

function Dato({ label, valor }: { label: string; valor: React.ReactNode }) {
  return (
    <div>
      <div className="trf-field-label">{label}</div>
      <div>{valor}</div>
    </div>
  );
}
