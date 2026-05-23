import { usePlatformMenuBuilder } from './usePlatformMenuBuilder';
import { PlatformMenuBuilderCrmWorkspace } from './PlatformMenuBuilderCrmWorkspace';
import { PlatformMenuBuilderLegacyPanel } from './PlatformMenuBuilderLegacyPanel';
import '../menu-builder/menu-builder.css';
import './menu-plan-composer.css';

export type PlatformMenuBuilderSectionProps = {
  /** Vista única 3 columnas (árbol | empresa | formularios) para el hub menú/planes. */
  crmWorkspace?: boolean;
};

export { adminNavigationToSessionMenu } from '../../modules/platform/adminNavigationToSessionMenu';

export function PlatformMenuBuilderSection({ crmWorkspace = false }: PlatformMenuBuilderSectionProps = {}) {
  const menuBuilder = usePlatformMenuBuilder(crmWorkspace);

  if (crmWorkspace) {
    return <PlatformMenuBuilderCrmWorkspace {...menuBuilder} />;
  }

  return <PlatformMenuBuilderLegacyPanel {...menuBuilder} />;
}
