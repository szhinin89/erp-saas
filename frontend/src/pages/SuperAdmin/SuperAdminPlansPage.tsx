import { SuperAdminPlansSection } from '../../components/superadmin/SuperAdminPlansSection';
import { SuperAdminCrudTemplate } from '../../templates/SuperAdminCrudTemplate';
import { useI18n } from '../../i18n/i18n';

export function SuperAdminPlansPage() {
  const { t } = useI18n();
  return (
    <SuperAdminCrudTemplate title={t('superadmin.shell.plans')}>
      <SuperAdminPlansSection />
    </SuperAdminCrudTemplate>
  );
}
