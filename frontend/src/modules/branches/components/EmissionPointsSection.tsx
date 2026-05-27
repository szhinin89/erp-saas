import { LoadingState, EmptyState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { EmissionPointFormModal } from './EmissionPointFormModal';
import { useEmissionPointsSection } from '../hooks/useEmissionPointsSection';

type Props = {
  establishmentId: string;
  establishmentCode: string;
};

export function EmissionPointsSection({ establishmentId, establishmentCode }: Props) {
  const ctx = useEmissionPointsSection(establishmentId);
  const { canCreate, canUpdate, canDelete, items, loading, error, openCreateModal, openEditModal, toggleDisable } = ctx;

  return (
    <div className="pg-section pg-section--nested">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">point_of_sale</span>
          <span className="pg-section-label">Puntos de Emisión — Est. {establishmentCode}</span>
        </div>
        {canCreate && (
          <button type="button" className="zh-btn zh-btn--primary zh-btn--sm" onClick={openCreateModal}>
            <span className="material-symbols-outlined">add</span>
            Nuevo punto
          </button>
        )}
      </div>

      {error && <ZHPageNotice variant="error" message="Error" detail={error} />}

      {loading ? (
        <div className="pg-pad-40"><LoadingState /></div>
      ) : items.length === 0 ? (
        <div className="pg-pad-40"><EmptyState message="Sin puntos de emisión registrados." /></div>
      ) : (
        <div className="pg-overflow-x">
          <table className="table table--sm">
            <thead>
              <tr>
                <th>Código</th>
                <th>Nombre</th>
                <th>Por defecto</th>
                <th>Estado</th>
                {canUpdate || canDelete ? <th className="pg-th-right">Acciones</th> : null}
              </tr>
            </thead>
            <tbody>
              {items.map((row) => (
                <tr key={row.id} className={row.isActive ? undefined : 'pg-row-inactive'}>
                  <td>
                    <span className="badge badge--gray badge--sm mono">{row.code}</span>
                  </td>
                  <td>{row.name ?? <span className="subtle">—</span>}</td>
                  <td>
                    {row.isDefault ? (
                      <span className="badge badge--blue badge--sm">Defecto</span>
                    ) : (
                      <span className="subtle">—</span>
                    )}
                  </td>
                  <td>
                    <span className={row.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                      {row.isActive ? 'Activo' : 'Inactivo'}
                    </span>
                  </td>
                  {canUpdate || canDelete ? (
                    <td className="pg-td-right">
                      <div className="br-actions-tight">
                        {canUpdate && (
                          <button
                            type="button"
                            className="zh-btn zh-btn--ghost zh-btn--sm"
                            title="Editar"
                            onClick={() => openEditModal(row)}
                          >
                            <span className="material-symbols-outlined">edit</span>
                          </button>
                        )}
                        {(row.isActive ? canDelete : canUpdate) && (
                          <button
                            type="button"
                            className="zh-btn zh-btn--ghost zh-btn--sm"
                            title={row.isActive ? 'Desactivar' : 'Activar'}
                            onClick={() => void toggleDisable(row)}
                          >
                            <span className="material-symbols-outlined">
                              {row.isActive ? 'block' : 'check_circle'}
                            </span>
                          </button>
                        )}
                      </div>
                    </td>
                  ) : null}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <EmissionPointFormModal {...ctx} />
    </div>
  );
}
