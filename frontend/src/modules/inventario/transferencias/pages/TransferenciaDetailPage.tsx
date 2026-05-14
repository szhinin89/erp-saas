import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage, PageShell, TableCard } from '../../../../components/PageShell';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHConfirmModal } from '../../../../components/zh/ZHConfirmModal';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { TransferenciaEstadoBadge } from '../components/TransferenciaEstadoBadge';
import { useTransferenciaDetalle, useTransferenciaAcciones } from '../hooks/useTransferencias';

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

  const esBorrador = transferencia?.estado === 'Borrador';

  return (
    <PageShell
      kicker="Inventario · Transferencias"
      title={transferencia?.numeroTransferencia ?? 'Cargando…'}
      action={
        esBorrador ? (
          <div style={{ display: 'flex', gap: '8px' }}>
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
      <TableCard>
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
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '24px' }}>
              <Dato label="Número"  valor={transferencia.numeroTransferencia} />
              <Dato label="Estado"  valor={<TransferenciaEstadoBadge estado={transferencia.estado} />} />
              <Dato label="Fecha"   valor={new Date(transferencia.fechaTransferencia).toLocaleString()} />
              <Dato label="Bodega origen"  valor={transferencia.bodegaOrigenNombre} />
              <Dato label="Bodega destino" valor={transferencia.bodegaDestinoNombre} />
              {transferencia.motivo && <Dato label="Motivo" valor={transferencia.motivo} />}
              {transferencia.observaciones && <Dato label="Observaciones" valor={transferencia.observaciones} />}
              {transferencia.fechaConfirmacion && (
                <Dato
                  label="Confirmado el"
                  valor={new Date(transferencia.fechaConfirmacion).toLocaleString()}
                />
              )}
            </div>

            {/* Ítems */}
            <h3 style={{ marginBottom: '8px' }}>Ítems</h3>
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
                    <td>{d.descripcion}</td>
                    <td style={{ textAlign: 'right' }}>{d.cantidad}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            <div style={{ marginTop: '16px' }}>
              <ZHBtn variant="ghost" size="md" onClick={() => navigate('/inventario/transferencias')}>
                ← Volver al listado
              </ZHBtn>
            </div>
          </>
        )}
      </TableCard>
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
      <div style={{ fontSize: '0.75rem', color: 'var(--zh-text-muted)', marginBottom: '2px' }}>{label}</div>
      <div>{valor}</div>
    </div>
  );
}
