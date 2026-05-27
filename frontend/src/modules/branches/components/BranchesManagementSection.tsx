import { NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { BranchFormModal } from './BranchFormModal';
import { BranchesListSection } from './BranchesListSection';
import { useBranchesPage } from '../hooks/useBranchesPage';
import '../pages/branches-page.css';

export function BranchesManagementSection() {
  const ctx = useBranchesPage();
  const { t, canView, error } = ctx;

  if (!canView) return <NoAccessPage title={t('branches.title')} />;

  return (
    <>
      {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}
      <BranchesListSection {...ctx} />
      <BranchFormModal {...ctx} />
    </>
  );
}
