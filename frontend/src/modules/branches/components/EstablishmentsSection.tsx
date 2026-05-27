import { useState } from 'react';
import { LoadingState, EmptyState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { EstablishmentFormModal } from './EstablishmentFormModal';
import { EmissionPointsSection } from './EmissionPointsSection';
import { useEstablishmentsSection } from '../hooks/useEstablishmentsSection';

type Props = {
  branchId: string;
};

export function EstablishmentsSection({ branchId }: Props) {
  const ctx = useEstablishmentsSection(branchId);
  const { canCreate, canUpdate, canDelete, items, loading, error, openCreateModal, openEditModal, toggleDisable } = ctx;

  const [expandedId, setExpandedId] = useState<string | null>(null);

  const toggleExpand = (id: string) => {
    setExpandedId((prev) => (prev === id ? null : id));
  };

  return (
    <div className="pg-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">store</span>
          <span className="pg-section-label">Establecimientos SRI</span>
        </div>
        {canCreate && (
          <button type="button" className="zh-btn zh-btn--primary zh-btn--sm" onClick={openCreateModal}>
            <span className="material-symbols-outlined">add</span>
            Nuevo establecimiento
          </button>
        )}
      </div>

      {error && <ZHPageNotice variant="error" message="Error" detail={error} />}

      {loading ? (
        <div className="pg-pad-40"><LoadingState /></div>
      ) : items.length === 0 ? (
        <div className="pg-pad-40"><EmptyState message="Sin establecimientos registrados." /></div>
      ) : (
        <div className="pg-overflow-x">
          <table className="table">
            <thead>
              <tr>
                <th>Código</th>
                <th>Nombre</th>
                <th>Dirección</th>
                <th>Principal</th>
                <th>Estado</th>
                {canUpdate || canDelete ? <th className="pg-th-right">Acciones</th> : null}
              </tr>
            </thead>
            <tbody>
              {items.map((row) => (
                <>
                  <tr key={row.id} className={row.isActive ? undefined : 'pg-row-inactive'}>
                    <td>
                      <button
                        type="button"
                        className="zh-btn zh-btn--ghost zh-btn--sm"
                        title="Ver puntos de emisión"
                        onClick={() => toggleExpand(row.id)}
                      >
                        <span className="material-symbols-outlined">
                          {expandedId === row.id ? 'expand_less' : 'expand_more'}
                        </span>
                        <span className="badge badge--gray badge--md mono">{row.code}</span>
                      </button>
                    </td>
                    <td>
                      <div>{row.name}</div>
                    </td>
                    <td>
                      <div className="br-list-sub">{row.address}</div>
                    </td>
                    <td>
                      {row.isMain ? (
                        <span className="badge badge--blue badge--md">Principal</span>
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
                  {expandedId === row.id && (
                    <tr key={`${row.id}-emission`}>
                      <td colSpan={canUpdate || canDelete ? 6 : 5} className="pg-nested-td">
                        <EmissionPointsSection
                          establishmentId={row.id}
                          establishmentCode={row.code}
                        />
                      </td>
                    </tr>
                  )}
                </>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <EstablishmentFormModal {...ctx} />
    </div>
  );
}
