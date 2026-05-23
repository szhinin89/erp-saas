import { useEffect, useState, useMemo, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { platformService, type PlatformSubscriber, type PlatformPlanCatalogEntry, type CreateSubscriberWithAdminBody } from '../api/platformService';
import { goToSubscriberDetail } from '../../../navigation/platformSubscriberDetailNav';
import { LoadingState, EmptyState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { PlatformPanelCreateSubscriberModal } from '../PlatformPanelCreateSubscriberModal';
import { defaultModuleChecksAllOn, slugifySubscriberName, normalizeEnabledModulesForApi } from '../platformPanelUtils';
import { SUBSCRIBER_MODULE_KEYS } from '../../../constants/subscriptionModules';
import { formatApiRequestError } from '../../lib/apiError';
import { useI18n } from '../../../i18n/i18n';
import './PlatformSubscribersPage.css';

export function PlatformSubscribersPage() {
  const navigate = useNavigate();
  const { t } = useI18n();

  const [items, setItems] = useState<PlatformSubscriber[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  // ── Create subscriber modal state ──
  const [plans, setPlans] = useState<PlatformPlanCatalogEntry[]>([]);
  const [createOpen, setCreateOpen] = useState(false);
  const [createBusy, setCreateBusy] = useState(false);
  const [createError, setCreateError] = useState('');
  const [createForm, setCreateForm] = useState<CreateSubscriberWithAdminBody>({
    subscriberName: '', subscriberSlug: '', adminFirstName: '',
    adminLastName: '', adminEmail: '', adminPassword: '',
    passwordResetMode: 0, linkExistingAdmin: false,
  });
  const [createPlanCode, setCreatePlanCode] = useState('');
  const [createRestrictModules, setCreateRestrictModules] = useState(false);
  const [createModuleChecks, setCreateModuleChecks] = useState<Record<string, boolean>>(defaultModuleChecksAllOn);

  const activePlans = useMemo(() => plans.filter((p) => p.isActive).sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0)), [plans]);
  const moduleLabel = useCallback((key: string) => t(`companies.subscription.module.${key}`, key), [t]);

  useEffect(() => {
    platformService.getSubscribers()
      .then((list) => setItems(list ?? []))
      .catch(() => setError('No se pudo cargar la lista de suscriptores.'))
      .finally(() => setLoading(false));
    platformService.getPlansCatalog()
      .then((list) => setPlans(list ?? []))
      .catch(() => setPlans([]));
  }, []);

  const openCreate = () => {
    setCreateError('');
    setCreatePlanCode('');
    setCreateRestrictModules(false);
    setCreateModuleChecks(defaultModuleChecksAllOn());
    setCreateForm({ subscriberName: '', subscriberSlug: '', adminFirstName: '', adminLastName: '', adminEmail: '', adminPassword: '', passwordResetMode: 0, linkExistingAdmin: false });
    setCreateOpen(true);
  };

  const saveCreate = async () => {
    setCreateBusy(true);
    setCreateError('');
    try {
      if (!createPlanCode.trim()) { setCreateError(t('platform.createSubscriber.error.planRequired')); return; }
      if (createRestrictModules && SUBSCRIBER_MODULE_KEYS.filter((k) => createModuleChecks[k]).length === 0) {
        setCreateError(t('platform.createSubscriber.error.noModulesSelected')); return;
      }
      const subscriberName = createForm.subscriberName.trim();
      const subscriberSlug = (createForm.subscriberSlug.trim() || slugifySubscriberName(subscriberName)).toLowerCase();
      const session = await platformService.createSubscriberWithAdmin({
        ...createForm, subscriberName, subscriberSlug,
        adminEmail: createForm.adminEmail.trim().toLowerCase(),
        ruc: createForm.ruc?.trim() || null,
        countryCode: createForm.countryCode?.trim() || 'ECU',
        timezone: createForm.timezone?.trim() || 'America/Guayaquil',
        adminPassword: createForm.linkExistingAdmin ? '' : createForm.adminPassword,
        planCode: createPlanCode.trim(),
        enabledModules: normalizeEnabledModulesForApi(createRestrictModules, createModuleChecks) ?? null,
      });
      setCreateOpen(false);
      goToSubscriberDetail(navigate, session.subscriberId);
    } catch (e) {
      setCreateError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setCreateBusy(false);
    }
  };

  const filtered = useMemo(
    () =>
      items.filter(
        (s) =>
          !search ||
          s.name.toLowerCase().includes(search.toLowerCase()) ||
          s.slug.toLowerCase().includes(search.toLowerCase()) ||
          (s.planCode ?? '').toLowerCase().includes(search.toLowerCase()),
      ),
    [items, search],
  );

  const openSubscriberSheet = (subscriber: PlatformSubscriber) => {
    goToSubscriberDetail(navigate, subscriber.id);
  };

  return (
    <div className="sa-subscribers-page">
      <div className="sa-subscribers-header">
        <div>
          <h2 className="sa-subscribers-title">Suscriptores</h2>
          <p className="sa-subscribers-sub">
            Abre la ficha de un suscriptor para administrarlo o entrar a su tenant.
          </p>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <div className="sa-subscribers-search">
            <span className="material-symbols-outlined">search</span>
            <input
              type="text"
              placeholder="Buscar por nombre, slug o plan…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <ZHBtn variant="primary" size="sm" onClick={openCreate}>
            {t('platform.createSubscriber')}
          </ZHBtn>
        </div>
      </div>

      {error && <ZHPageNotice variant="error" message={error} />}

      {loading ? (
        <div className="sa-subscribers-center">
          <LoadingState />
        </div>
      ) : filtered.length === 0 ? (
        <div className="sa-subscribers-center">
          <EmptyState message={items.length === 0 ? 'Sin suscriptores registrados.' : 'Sin resultados.'} />
        </div>
      ) : (
        <div className="pg-overflow-x">
          <table className="table">
            <thead>
              <tr>
                <th>Suscriptor</th>
                <th>Plan</th>
                <th>Usuarios</th>
                <th>Estado</th>
                <th className="pg-th-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((s) => (
                <tr
                  key={s.id}
                  className={`sa-sub-row${!s.isActive ? ' pg-row-inactive' : ''}`}
                  tabIndex={0}
                  role="button"
                  aria-label={`Abrir ficha de ${s.name}`}
                  onClick={() => openSubscriberSheet(s)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      openSubscriberSheet(s);
                    }
                  }}
                >
                  <td>
                    <div className="sa-sub-name">{s.name}</div>
                    <div className="sa-sub-slug subtle">{s.slug}</div>
                  </td>
                  <td>
                    {s.planCode ? (
                      <span className="badge badge--blue badge--md badge--upper">{s.planCode}</span>
                    ) : (
                      <span className="subtle">—</span>
                    )}
                  </td>
                  <td>
                    <span className="mono">{s.activeUsers}</span>
                    <span className="subtle"> / {s.totalUsers}</span>
                  </td>
                  <td>
                    <span className={s.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                      {s.isActive ? 'Activo' : 'Inactivo'}
                    </span>
                  </td>
                  <td className="pg-td-right">
                    <div className="sa-sub-actions">
                      <button
                        type="button"
                        className="zh-btn zh-btn--primary zh-btn--sm"
                        title={t('platform.subscriberRow.openSheet')}
                        onClick={(e) => {
                          e.stopPropagation();
                          openSubscriberSheet(s);
                        }}
                      >
                        <span className="material-symbols-outlined">description</span>
                        {t('platform.subscriberRow.openSheet')}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div className="sa-subscribers-footer subtle">
        {filtered.length} de {items.length} suscriptores
      </div>

      <PlatformPanelCreateSubscriberModal
        t={t}
        createSubscriberOpen={createOpen}
        setCreateSubscriberOpen={setCreateOpen}
        createBusy={createBusy}
        createError={createError}
        createForm={createForm}
        setCreateForm={setCreateForm}
        createPlanCode={createPlanCode}
        setCreatePlanCode={setCreatePlanCode}
        createRestrictModules={createRestrictModules}
        setCreateRestrictModules={setCreateRestrictModules}
        createModuleChecks={createModuleChecks}
        setCreateModuleChecks={setCreateModuleChecks}
        activePlans={activePlans}
        moduleLabel={moduleLabel}
        saveCreateSubscriber={saveCreate}
        slugify={slugifySubscriberName}
      />
    </div>
  );
}
