import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { superAdminService } from '../api/superAdminService';
import { storeImpersonationSubscriberName } from '../superAdminPanelUtils';
import { useAuthStore } from '../../../store/authStore';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { formatApiRequestError } from '../../../modules/lib/apiError';
import { getAccessToken } from '../../../lib/session/authTokenMemory';
import { restoreSessionFromCookie } from '../../../lib/session/restoreSessionFromCookie';
import { syncSessionEntitlements } from '../../../lib/syncSessionEntitlements';
import { LoadingState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHFormSection, ZHGrid, ZHField, ZHBtn } from '../../../components/zh/ZHForm';
import { ConfigManagementPanel } from '../../../components/config/ConfigManagementPanel';
import { EntityAuditPanel } from '../../../components/EntityAuditPanel';
import { useSuperAdminGate } from '../../../hooks/useSuperAdminGate';
import { ELECTRONIC_BILLING_TRIAL_KEY, useSubscriberDetailPage } from './subscriber-detail/useSubscriberDetailPage';
import './SuperAdminSubscriberDetailPage.css';

type TabId =
  | 'overview'
  | 'subscription'
  | 'entitlements'
  | 'companies'
  | 'users'
  | 'configuration'
  | 'menu'
  | 'audit'
  | 'metrics';

const TABS: { id: TabId; label: string; icon: string }[] = [
  { id: 'overview', label: 'Overview', icon: 'info' },
  { id: 'subscription', label: 'Subscription', icon: 'subscriptions' },
  { id: 'entitlements', label: 'Entitlements', icon: 'verified' },
  { id: 'companies', label: 'Companies', icon: 'domain' },
  { id: 'users', label: 'Users', icon: 'group' },
  { id: 'configuration', label: 'Configuration', icon: 'tune' },
  { id: 'menu', label: 'Menu Overrides', icon: 'menu_book' },
  { id: 'audit', label: 'Audit', icon: 'history' },
  { id: 'metrics', label: 'Usage', icon: 'monitoring' },
];

function parseTab(raw: string | null): TabId {
  const v = (raw ?? 'overview').trim().toLowerCase();
  return (TABS.find((t) => t.id === v)?.id ?? 'overview') as TabId;
}

export function SuperAdminSubscriberDetailPage() {
  const { subscriberId } = useParams<{ subscriberId: string }>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const tab = useMemo(() => parseTab(searchParams.get('tab')), [searchParams]);
  const { isSuperAdmin } = useSuperAdminGate();
  const { login } = useAuthStore();
  const clearPermissions = usePermissionsStore((s) => s.clearPermissions);

  const page = useSubscriberDetailPage(subscriberId);
  const [actionBusy, setActionBusy] = useState(false);
  const [actionMsg, setActionMsg] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [changePlanInput, setChangePlanInput] = useState('');
  const [platformAudit, setPlatformAudit] = useState<Awaited<ReturnType<typeof superAdminService.getPlatformAudit>>>([]);
  const [entLoading, setEntLoading] = useState(false);

  const { subscriber, loading, error } = page;

  useEffect(() => {
    if (subscriber?.planCode) setChangePlanInput(subscriber.planCode);
  }, [subscriber?.planCode]);

  useEffect(() => {
    if (tab !== 'entitlements' || !subscriberId || page.entitlements) return;
    setEntLoading(true);
    void page.loadEntitlements().finally(() => setEntLoading(false));
  }, [tab, subscriberId, page.entitlements, page.loadEntitlements]);

  useEffect(() => {
    if (tab === 'users' && subscriberId) void page.loadTenantUsers();
  }, [tab, subscriberId, page]);

  useEffect(() => {
    if (tab !== 'audit' || !subscriberId) return;
    superAdminService.getPlatformAudit(50, subscriberId).then(setPlatformAudit).catch(() => setPlatformAudit([]));
  }, [tab, subscriberId]);

  const setTab = (next: TabId) => {
    setSearchParams(next === 'overview' ? {} : { tab: next }, { replace: true });
  };

  const doAction = async (fn: () => Promise<unknown>, successMsg: string) => {
    setActionBusy(true);
    setActionError(null);
    setActionMsg(null);
    try {
      await fn();
      setActionMsg(successMsg);
      await page.reloadSummary();
    } catch (e) {
      setActionError(formatApiRequestError(e, { generic: 'Error al ejecutar la acción.' }));
    } finally {
      setActionBusy(false);
    }
  };

  const handleImpersonate = async (target: '/saas/overview' | '/saas/companies' | '/saas/billing') => {
    if (!subscriber || actionBusy) return;
    setActionBusy(true);
    setActionError(null);
    try {
      if (!getAccessToken()) {
        const restored = await restoreSessionFromCookie();
        if (!restored || !getAccessToken()) throw new Error('Sesión expirada.');
      }
      const auth = await superAdminService.switchSubscriber(subscriber.id);
      storeImpersonationSubscriberName(subscriber.name);
      clearPermissions();
      login(auth);
      try { await syncSessionEntitlements(); } catch { /* */ }
      navigate(target);
    } catch (e) {
      setActionError(formatApiRequestError(e, { generic: 'No se pudo impersonar.' }));
    } finally {
      setActionBusy(false);
    }
  };

  if (loading) {
    return <div className="sa-detail-page"><div className="sa-detail-loading"><LoadingState /></div></div>;
  }

  if (!subscriber || !subscriberId) {
    return (
      <div className="sa-detail-page">
        <ZHPageNotice variant="error" message={error ?? 'Suscriptor no encontrado.'} />
        <ZHBtn variant="ghost" size="md" onClick={() => navigate('/superadmin/subscribers')}>Volver</ZHBtn>
      </div>
    );
  }

  const showNotice = error || actionError || page.saveError;
  const showSuccess = actionMsg || page.saveOk;

  return (
    <div className="sa-detail-page">
      <div className="sa-detail-header">
        <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm sa-detail-back" onClick={() => navigate('/superadmin/subscribers')}>
          <span className="material-symbols-outlined">arrow_back</span>
          Suscriptores
        </button>
        <div className="sa-detail-title-row">
          <div className="sa-detail-avatar">{subscriber.name.charAt(0).toUpperCase()}</div>
          <div>
            <h2 className="sa-detail-name">{subscriber.name}</h2>
            <p className="sa-detail-slug subtle">{subscriber.slug}</p>
          </div>
          <div className="sa-detail-status-chips">
            <span className={subscriber.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
              {subscriber.isActive ? 'Activo' : 'Inactivo'}
            </span>
            {subscriber.planCode && <span className="badge badge--blue badge--md badge--upper">{subscriber.planCode}</span>}
          </div>
        </div>
      </div>

      <div className="sa-detail-tabs sa-detail-tabs--scroll">
        {TABS.map((t) => (
          <button key={t.id} type="button" className={`sa-detail-tab${tab === t.id ? ' is-active' : ''}`} onClick={() => setTab(t.id)}>
            <span className="material-symbols-outlined">{t.icon}</span>
            {t.label}
          </button>
        ))}
      </div>

      {showNotice && <ZHPageNotice variant="error" message={showNotice} />}
      {showSuccess && <ZHPageNotice variant="success" message={typeof showSuccess === 'string' ? showSuccess : 'Guardado.'} />}

      {tab === 'overview' && (
        <div className="sa-detail-body">
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">Resumen</h3>
            <div className="sa-detail-grid">
              <div className="sa-detail-field"><span className="sa-detail-label">ID</span><span className="mono">{subscriber.id}</span></div>
              <div className="sa-detail-field"><span className="sa-detail-label">Usuarios</span><span className="mono">{subscriber.activeUsers}/{subscriber.totalUsers}</span></div>
              <div className="sa-detail-field"><span className="sa-detail-label">Menú custom</span><span>{subscriber.hasCustomMenu ? 'Sí' : 'No'}</span></div>
            </div>
          </div>
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">Impersonación</h3>
            <div className="sa-detail-impersonation-btns">
              <ZHBtn variant="ghost" size="md" disabled={actionBusy} onClick={() => void handleImpersonate('/saas/overview')}>Cuenta SaaS</ZHBtn>
              <ZHBtn variant="ghost" size="md" disabled={actionBusy} onClick={() => void handleImpersonate('/saas/companies')}>Empresas ERP</ZHBtn>
              <ZHBtn variant="ghost" size="md" disabled={actionBusy} onClick={() => void handleImpersonate('/saas/billing')}>Facturación</ZHBtn>
            </div>
          </div>
        </div>
      )}

      {tab === 'subscription' && (
        <div className="sa-detail-body">
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">Lifecycle</h3>
            <div className="sa-detail-action-row">
              <ZHBtn variant="primary" size="md" disabled={actionBusy || subscriber.isActive} onClick={() => void doAction(() => superAdminService.activateSubscriber(subscriber.id), 'Activado.')}>Activar</ZHBtn>
              <ZHBtn variant="ghost" size="md" disabled={actionBusy || !subscriber.isActive} onClick={() => void doAction(() => superAdminService.suspendSubscriber(subscriber.id), 'Suspendido.')}>Suspender</ZHBtn>
            </div>
          </div>
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">Plan comercial</h3>
            <div className="sa-detail-action-row">
              <input className="zh-input" value={changePlanInput} onChange={(e) => setChangePlanInput(e.target.value)} placeholder="Código plan" />
              <ZHBtn variant="primary" size="md" disabled={actionBusy || !changePlanInput.trim()} onClick={() => void doAction(() => superAdminService.changePlan(subscriber.id, changePlanInput.trim()), 'Plan actualizado.')}>Cambiar</ZHBtn>
            </div>
          </div>
        </div>
      )}

      {tab === 'entitlements' && (
        <div className="sa-detail-body">
          {entLoading || page.detailLoading ? <LoadingState /> : page.entitlements ? (
            <>
              <p className="subtle">Plan: {page.entitlements.planName ?? page.entitlements.planCode ?? '—'} · Billing: {page.entitlements.billingAccountStatus ?? '—'}</p>
              <p>Módulos: {page.entitlements.enabledModules.join(', ') || '—'}</p>
              <p>Features: {page.entitlements.enabledFeatures.join(', ') || '—'}</p>
            </>
          ) : <p className="subtle">Sin entitlements.</p>}
        </div>
      )}

      {tab === 'companies' && (
        <div className="sa-detail-body">
          {page.detailLoading ? <LoadingState /> : (
            <form onSubmit={(e) => { e.preventDefault(); void page.saveCompanyProfile(); }}>
              <ZHFormSection title="Perfil comercial / legal">
                <ZHGrid cols={2}>
                  <ZHField label="Nombre"><input className="zh-input" {...page.form.register('subscriberName')} /></ZHField>
                  <ZHField label="Slug"><input className="zh-input" {...page.form.register('subscriberSlug')} /></ZHField>
                  <ZHField label="RUC"><input className="zh-input" {...page.form.register('ruc')} /></ZHField>
                  <ZHField label="Nombre corto"><input className="zh-input" {...page.form.register('shortName')} /></ZHField>
                </ZHGrid>
              </ZHFormSection>
              {page.detail && (
                <ZHFormSection title="Operacional">
                  <p className="subtle mono">{page.detail.currency} · {page.detail.timezone} · {page.detail.language}</p>
                </ZHFormSection>
              )}
              <div className="zh-form-actions-row zh-form-actions-row--end">
                <ZHBtn variant="primary" size="md" type="submit" disabled={page.saving}>Guardar perfil</ZHBtn>
              </div>
            </form>
          )}
        </div>
      )}

      {tab === 'users' && (
        <div className="sa-detail-body sa-detail-card">
          <table className="table">
            <thead><tr><th>Email</th><th>Nombre</th><th>Tipo</th><th>Estado</th></tr></thead>
            <tbody>
              {page.tenantUsers.map((u) => (
                <tr key={u.id}>
                  <td>{u.email}</td>
                  <td>{u.firstName} {u.lastName}</td>
                  <td className="mono subtle">{u.userType}</td>
                  <td>{u.isActive ? 'Activo' : 'Inactivo'}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {page.tenantUsers.length === 0 && <p className="subtle">Sin usuarios tenant.</p>}
        </div>
      )}

      {tab === 'configuration' && subscriberId && (
        <div className="sa-detail-body">
          <div className="sa-detail-card">
            <label className="companies-checkbox-label">
              <input type="checkbox" checked={page.electronicBillingTrialEnabled} onChange={(e) => page.setElectronicBillingTrialEnabled(e.target.checked)} />
              Trial facturación electrónica ({ELECTRONIC_BILLING_TRIAL_KEY})
            </label>
            <ZHBtn variant="primary" size="sm" className="pg-mt-sm" disabled={page.saving} onClick={() => void page.saveGlobalParameters()}>Guardar parámetros</ZHBtn>
            <p className="subtle">Claves globales: {page.globalConfigCount}</p>
          </div>
          <ConfigManagementPanel subscriberId={subscriberId} canManage={isSuperAdmin} title="Config por alcance" subtitle="Global / module / feature" />
        </div>
      )}

      {tab === 'menu' && (
        <div className="sa-detail-body sa-detail-card">
          <p>Menú personalizado: {page.menuFlags?.hasCustomMenu ? 'Sí' : 'No'} · Plan menu: {page.menuFlags?.usedPlanMenu ? 'Sí' : 'No'}</p>
          <ZHBtn variant="ghost" size="md" onClick={() => void page.resetCustomMenu()}>Restablecer menú custom</ZHBtn>
          <p className="subtle pg-mt-md">Editor global: <a className="zh-link" href="/superadmin/plans?tab=menu">Plans → Menu builder</a></p>
        </div>
      )}

      {tab === 'audit' && subscriberId && (
        <div className="sa-detail-body">
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">Platform audit</h3>
            {platformAudit.length === 0 ? <p className="subtle">Sin eventos platform.</p> : (
              <ul className="subtle">{platformAudit.map((e) => <li key={e.id}>{e.action} — {e.createdAtUtc}</li>)}</ul>
            )}
          </div>
          <EntityAuditPanel entityType="Subscriber" entityId={subscriberId} take={15} />
        </div>
      )}

      {tab === 'metrics' && (
        <div className="sa-detail-body sa-detail-card">
          <p>Usuarios activos: {subscriber.activeUsers} / {subscriber.totalUsers}</p>
          <p>Módulos: {subscriber.hasModuleRestrictions ? subscriber.enabledModules?.join(', ') : 'Sin restricción'}</p>
          <ZHBtn variant="ghost" size="sm" onClick={() => navigate('/superadmin/observability')}>Ver observability global</ZHBtn>
        </div>
      )}
    </div>
  );
}
