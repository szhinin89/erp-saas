import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { NoAccessPage, LoadingState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { branchService, type BranchDetailDto } from '../api/branchService';
import { EstablishmentsSection } from '../components/EstablishmentsSection';
import { usePermissionsUi } from '../../../access/usePermissionsUi';

type Tab = 'general' | 'establishments';

export function BranchDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { canShow } = usePermissionsUi();
  const canView = canShow('settings.branches.view');

  const [branch, setBranch] = useState<BranchDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [tab, setTab] = useState<Tab>('general');

  useEffect(() => {
    if (!id) return;
    setLoading(true);
    branchService
      .getById(id)
      .then(setBranch)
      .catch(() => setError('No se pudo cargar la sucursal.'))
      .finally(() => setLoading(false));
  }, [id]);

  if (!canView) return <NoAccessPage title="Sucursal" />;

  if (loading) {
    return (
      <ErpPageTemplate kicker="Configuración" title="Cargando…">
        <LoadingState />
      </ErpPageTemplate>
    );
  }

  if (error || !branch) {
    return (
      <ErpPageTemplate kicker="Configuración" title="Sucursal">
        <ZHPageNotice variant="error" message="Error" detail={error || 'Sucursal no encontrada.'} />
        <Link to="/settings/branches" className="zh-btn zh-btn--ghost">
          ← Volver a sucursales
        </Link>
      </ErpPageTemplate>
    );
  }

  return (
    <ErpPageTemplate
      kicker="Sucursales"
      title={branch.name}
      subtitle={branch.address}
      action={
        <Link to="/settings/branches" className="zh-btn zh-btn--ghost">
          <span className="material-symbols-outlined">arrow_back</span>
          Volver
        </Link>
      }
    >
      <div className="zh-form-tabs" role="tablist">
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'general'}
          className={tab === 'general' ? 'is-active' : ''}
          onClick={() => setTab('general')}
        >
          General
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'establishments'}
          className={tab === 'establishments' ? 'is-active' : ''}
          onClick={() => setTab('establishments')}
        >
          Establecimientos y Puntos de Emisión
        </button>
      </div>

      {tab === 'general' && (
        <div className="pg-section">
          <div className="br-detail-grid">
            <div className="br-detail-field">
              <span className="br-detail-label">Código</span>
              <span className="br-detail-value mono">{branch.code ?? '—'}</span>
            </div>
            <div className="br-detail-field">
              <span className="br-detail-label">Tipo</span>
              <span className="br-detail-value">{branch.branchType ?? '—'}</span>
            </div>
            <div className="br-detail-field">
              <span className="br-detail-label">Encargado</span>
              <span className="br-detail-value">{branch.managerName ?? '—'}</span>
            </div>
            <div className="br-detail-field">
              <span className="br-detail-label">Teléfono</span>
              <span className="br-detail-value">{branch.phones ?? '—'}</span>
            </div>
            <div className="br-detail-field">
              <span className="br-detail-label">Email</span>
              <span className="br-detail-value">{branch.email ?? '—'}</span>
            </div>
            <div className="br-detail-field">
              <span className="br-detail-label">Sucursal principal</span>
              <span className="br-detail-value">
                {branch.isMainBranch ? (
                  <span className="badge badge--blue badge--md">Sí</span>
                ) : (
                  'No'
                )}
              </span>
            </div>
            <div className="br-detail-field">
              <span className="br-detail-label">Estado</span>
              <span className={branch.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                {branch.isActive ? 'Activa' : 'Inactiva'}
              </span>
            </div>
          </div>
        </div>
      )}

      {tab === 'establishments' && id && (
        <EstablishmentsSection branchId={id} />
      )}
    </ErpPageTemplate>
  );
}
