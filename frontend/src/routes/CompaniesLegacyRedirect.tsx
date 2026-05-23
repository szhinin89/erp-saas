import { useEffect } from 'react';
import { Navigate, useSearchParams } from 'react-router-dom';
import {
  readCompaniesDetailSubscriberId,
  clearCompaniesDetailSubscriberId,
} from '../navigation/companiesSubscriberDetailNav';
import { SUPERADMIN_UI } from '../modules/superadmin/api/platformApiPaths';
import { superAdminService } from '../modules/superadmin/api/superAdminService';

/**
 * Legacy `/companies` entry points → rutas canónicas Super Admin.
 * Preserva sessionStorage de ficha de suscriptor (erp.saas.companies.detailSubscriberId).
 */
export function CompaniesLegacyRedirect() {
  const [searchParams] = useSearchParams();

  useEffect(() => {
    void superAdminService.recordLegacyUiRoute('/companies', window.location.pathname);
  }, []);
  const detailId = readCompaniesDetailSubscriberId();

  if (detailId) {
    clearCompaniesDetailSubscriberId();
    return <Navigate to={SUPERADMIN_UI.subscriberDetail(detailId)} replace />;
  }

  const mainNavigation = searchParams.get('mainNavigation');
  if (mainNavigation === '1') {
    return <Navigate to={`${SUPERADMIN_UI.plans}?tab=menu`} replace />;
  }

  const plan = searchParams.get('plan')?.trim();
  if (plan) {
    return <Navigate to={`${SUPERADMIN_UI.plans}?plan=${encodeURIComponent(plan)}`} replace />;
  }

  return <Navigate to={SUPERADMIN_UI.subscribers} replace />;
}
