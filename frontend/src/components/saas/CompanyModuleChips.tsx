import { useI18n } from '../../i18n/i18n';
import { SUBSCRIBER_MODULE_KEYS, moduleKeysMatch } from '../../constants/subscriptionModules';
import type { CompanyItem } from '../../modules/companies/api/companyService';

export function CompanyModuleChips({
  company,
}: {
  company: Pick<CompanyItem, 'enabledModules' | 'hasModuleRestrictions'> | CompanyItem;
}) {
  const { t } = useI18n();

  const enabled = company.enabledModules ?? [];

  if (!company.hasModuleRestrictions) {
    return (
      <span className="companies-module-chip companies-module-chip--all">
        {t('companies.subscription.badgeAllModules')}
      </span>
    );
  }

  if (enabled.length === 0) {
    return <span className="companies-module-chip companies-module-chip--muted">—</span>;
  }

  const keys = SUBSCRIBER_MODULE_KEYS.filter((k) =>
    enabled.some((m) => moduleKeysMatch(m, k))
  );

  if (keys.length === 0) {
    return <span className="companies-module-chip companies-module-chip--muted">—</span>;
  }

  return (
    <div className="companies-module-chips">
      {keys.map((k) => (
        <span key={k} className="companies-module-chip" title={t(`companies.subscription.module.${k}`)}>
          {t(`companies.subscription.moduleShort.${k}`)}
        </span>
      ))}
    </div>
  );
}
