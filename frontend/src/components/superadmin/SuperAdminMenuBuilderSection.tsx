import { useSuperAdminMenuBuilder } from './useSuperAdminMenuBuilder';
import { SuperAdminMenuBuilderCrmWorkspace } from './SuperAdminMenuBuilderCrmWorkspace';
import { SuperAdminMenuBuilderLegacyPanel } from './SuperAdminMenuBuilderLegacyPanel';
import '../menu-builder/menu-builder.css';
import './menu-plan-composer.css';

export type SuperAdminMenuBuilderSectionProps = {
  /** Vista única 3 columnas (árbol | empresa | formularios) para el hub menú/planes. */
  crmWorkspace?: boolean;
};

export { adminNavigationToSessionMenu } from '../../modules/superadmin/adminNavigationToSessionMenu';

export function SuperAdminMenuBuilderSection({ crmWorkspace = false }: SuperAdminMenuBuilderSectionProps = {}) {
  const menuBuilder = useSuperAdminMenuBuilder(crmWorkspace);

  if (crmWorkspace) {
    return <SuperAdminMenuBuilderCrmWorkspace {...menuBuilder} />;
  }

  return <SuperAdminMenuBuilderLegacyPanel {...menuBuilder} />;
}
