import { useParams, useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage, PageShell, TableCard } from '../../../../components/PageShell';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { AjusteEstadoBadge, AjusteTipoBadge } from '../components/AjusteEstadoBadge';
import { useAjusteDetalle, useAjusteAcciones } from '../hooks/useAjustes';

export function AjusteDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const hasPerm  = usePermissionsStore((s) => s.has);

  const canView    = hasPerm('inventario.ajustes.view');
  const canExecute = hasPerm('inventario.ajustes.execute');
  const canCancel  = hasPerm('inventario.ajustes.cancel');

  const { data: ajuste, loading, error, refetch } = useAjusteDetalle(id ?? null);
  const { loading: actLoading, error: actError, ejecutar, cancelar } = useAjusteAcciones(refetch);

  if (!canView) return <NoAccessPage title="Detalle de Ajuste" />;

  const esBorrador = ajuste?.estado === 'Borrador';

  return (
    <PageShell
      kicker="Inventario · Ajustes"
      title={ajuste?.numeroAjuste ?? 'Cargando…'}
      action={
        esBorrador ? (
          <div style={{ display: 'flex', gap: '8px' }}>
            {canExecute && (
              <ZHBtn variant="primary" size="md" disabled={actLoading}
                onClick={() => {
                  if (confirm('¿Ejecutar este ajuste? El stock se actualizará inmediatamente.'))
                    void ejecutar(id!);
                }}>
                {actLoading ? 'Procesando…' : 'Ejecutar'}
              </ZHBtn>
            )}
            {canCancel && (
              <ZHBtn variant="destructive" size="md" disabled={actLoading}
                onClick={() => {
                  if (confirm('¿Cancelar este ajuste?')) void cancelar(id!);
                }}>
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
        ) : !ajuste ? (
          <EmptyState message="Ajuste no encontrado." />
        ) : (
          <>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '24px' }}>
              <Dato label="Número"   valor={ajuste.numeroAjuste} />
              <Dato label="Estado"   valor={<AjusteEstadoBadge estado={ajuste.estado} />} />
              <Dato label="Tipo"     valor={
                <AjusteTipoBadge tipo={ajuste.tipoAjuste} cantidad={Math.abs(ajuste.cantidadAjuste)} />
              } />
              <Dato label="Producto" valor={ajuste.productoNombre} />
              <Dato label="Bodega"   valor={ajuste.bodegaNombre} />
              <Dato label="Motivo"   valor={ajuste.motivo} />
              <Dato label="Fecha"    valor={new Date(ajuste.fechaAjuste).toLocaleString()} />
              {ajuste.observaciones && <Dato label="Observaciones" valor={ajuste.observaciones} />}
              {ajuste.fechaEjecucion && (
                <Dato label="Ejecutado el" valor={new Date(ajuste.fechaEjecucion).toLocaleString()} />
              )}
            </div>

            <div style={{ marginTop: '16px' }}>
              <ZHBtn variant="ghost" size="md" onClick={() => navigate('/inventario/ajustes')}>
                ← Volver al listado
              </ZHBtn>
            </div>
          </>
        )}
      </TableCard>
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
