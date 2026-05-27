import { useSearchParams } from 'react-router-dom';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import { CompanyProfileSettingsSection } from '../sections/CompanyProfileSettingsSection';
import { SriConfigurationSection } from '../../sri/sections/SriConfigurationSection';
import { BranchesManagementSection } from '../../../branches/components/BranchesManagementSection';

type Tab = 'company' | 'sri' | 'branches';

const VALID_TABS: Tab[] = ['company', 'sri', 'branches'];

function parseTab(raw: string | null): Tab {
  if (raw && VALID_TABS.includes(raw as Tab)) return raw as Tab;
  return 'company';
}

export function CompanySettingsHubPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const tab = parseTab(searchParams.get('tab'));

  const { canShow } = usePermissionsUi();
  const canViewSri      = canShow('settings.sri.view');
  const canViewBranches = canShow('settings.branches.view');

  const setTab = (t: Tab) => {
    setSearchParams({ tab: t });
  };

  return (
    <ErpPageTemplate
      kicker="Configuración"
      title="Configuración Empresarial"
      subtitle="Parámetros, facturación electrónica y estructura de sucursales."
    >
      <div className="zh-form-tabs" role="tablist">
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'company'}
          className={tab === 'company' ? 'is-active' : ''}
          onClick={() => setTab('company')}
        >
          Datos Empresa
        </button>

        {canViewSri && (
          <button
            type="button"
            role="tab"
            aria-selected={tab === 'sri'}
            className={tab === 'sri' ? 'is-active' : ''}
            onClick={() => setTab('sri')}
          >
            Configuración SRI
          </button>
        )}

        {canViewBranches && (
          <button
            type="button"
            role="tab"
            aria-selected={tab === 'branches'}
            className={tab === 'branches' ? 'is-active' : ''}
            onClick={() => setTab('branches')}
          >
            Sucursales
          </button>
        )}

        <button type="button" role="tab" disabled title="Próximamente">
          Branding
        </button>
      </div>

      {tab === 'company'  && <CompanyProfileSettingsSection />}
      {tab === 'sri'      && <SriConfigurationSection />}
      {tab === 'branches' && <BranchesManagementSection />}
    </ErpPageTemplate>
  );
}
