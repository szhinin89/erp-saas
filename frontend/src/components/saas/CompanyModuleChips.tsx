import { useI18n } from '../../i18n/i18n';
import { TENANT_MODULE_KEYS } from '../../constants/subscriptionModules';
import type { CompanyItem } from '../../services/companyService';

type Row = Pick<CompanyItem, 'enabledModules' | 'hasModuleRestrictions'>;

export function CompanyModuleChips({ company }: { company: Row }) {
  const { t } = useI18n();

  if (!company.hasModuleRestrictions) {
    return (
      <span className="companies-module-chip companies-module-chip--all">
        {t('companies.subscription.badgeAllModules')}
      </span>
    );
  }

  const keys = TENANT_MODULE_KEYS.filter((k) =>
    (company.enabledModules ?? []).some((m) => m.toLowerCase() === k)
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
