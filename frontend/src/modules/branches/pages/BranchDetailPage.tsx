import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { NoAccessPage, LoadingState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { branchService, type BranchDetailDto } from '../api/branchService';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { BranchConfigSection } from '../components/BranchConfigSection';
import { BranchGeneralSection } from '../components/BranchGeneralSection';
import '../../../styles/shared/items-catalog.css';

type Tab = 'general' | 'establishments' | 'configuraciones';

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
      <div className="prd-tabs" role="tablist">
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'general'}
          className={`prd-tab-btn ${tab === 'general' ? 'prd-tab-btn--active' : ''}`}
          onClick={() => setTab('general')}
        >
          General
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'establishments'}
          className={`prd-tab-btn ${tab === 'establishments' ? 'prd-tab-btn--active' : ''}`}
          onClick={() => setTab('establishments')}
        >
          Establecimientos
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'configuraciones'}
          className={`prd-tab-btn ${tab === 'configuraciones' ? 'prd-tab-btn--active' : ''}`}
          onClick={() => setTab('configuraciones')}
        >
          Configuraciones
        </button>
      </div>

      {tab === 'general' && id && (
        <BranchGeneralSection
          branchId={id}
          initialData={branch}
          onSaved={(updated) => setBranch(updated)}
        />
      )}

      {tab === 'configuraciones' && id && (
        <BranchConfigSection branchId={id} />
      )}

      {tab === 'establishments' && (
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">receipt_long</span>
              <span className="pg-section-label">Establecimientos SRI asociados</span>
            </div>
          </div>
          <div className="pg-section-body">
            <p className="zh-text-muted zh-mb-16">
              Los establecimientos SRI se gestionan de forma independiente. Usa el módulo
              de Establecimientos para crear, editar y asignar establecimientos a esta sucursal.
            </p>
            <Link
              to={`/settings/establishments?branchId=${id}`}
              className="zh-btn zh-btn--primary zh-btn--md"
            >
              <span className="material-symbols-outlined zh-icon-md">receipt_long</span>
              Ver establecimientos de esta sucursal
            </Link>
          </div>
        </div>
      )}
    </ErpPageTemplate>
  );
}
