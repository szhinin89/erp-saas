import { useCallback, useEffect, useMemo, useState } from 'react';
import { TableCard } from '../PageShell';
import { ZHBtn, ZHField } from '../zh/ZHForm';
import { ZHCardSection, ZHGridRow, ZHInlineRowRight } from '../zh/ZHLayout';
import { ZHPageNotice } from '../zh/ZHPageNotice';
import { useI18n } from '../../i18n/i18n';
import { MenuBuilder, type MenuBuilderViewMode } from '../menu-builder/MenuBuilder';
import type { MenuPreviewLayout } from '../menu-builder/MenuPreview';
import {
  buildFuncionalidadMaps,
  serializeEditorTreeToMenuJson,
  sessionGroupsToEditorTree,
  validateSessionMenuGroups,
  type EditorMenuItem,
} from '../menu-builder/menuBuilderTypes';
import {
  superAdminService,
  type AdminNavigationMenu,
  type AdminNavItemRow,
  type FuncionalidadArbolDto,
  type SaasPlanAdmin,
  type SuperAdminTenant,
} from '../../services/superAdminService';
import type { SessionMenuGroupDto, SessionMenuItemDto } from '../../types/access';
import { formatApiRequestError } from '../../modules/lib/apiError';
import '../menu-builder/menu-builder.css';

type SubMode = 'plan' | 'tenant';

type EditorMainTab = 'json' | 'visual';

function isPlatformRoute(path: string): boolean {
  const p = (path ?? '').trim();
  return p === '/companies' || p.startsWith('/superadmin');
}

function mapAdminItems(items: AdminNavItemRow[] | null | undefined): SessionMenuItemDto[] {
  if (!items?.length) return [];
  const out: SessionMenuItemDto[] = [];
  for (const i of items) {
    if (!i.isActive) continue;
    if (isPlatformRoute(i.routePath)) continue;
    const children = mapAdminItems(i.children ?? undefined);
    out.push({
      routePath: i.routePath,
      labelKey: i.labelKey,
      displayLabel: i.displayLabel ?? null,
      sortOrder: i.sortOrder,
      moduleKey: i.moduleKey,
      permissionKey: i.permissionKey,
      permissionKeysAny: i.permissionKeysAny,
      itemRoles: null,
      icon: null,
      children: children.length ? children : undefined,
    });
  }
  return out;
}

export function adminNavigationToSessionMenu(menu: AdminNavigationMenu): SessionMenuGroupDto[] {
  return menu.groups
    .filter((g) => g.isActive && !g.requireSuperAdminPanel)
    .map((g) => ({
      code: g.code,
      icon: g.icon,
      labelKey: g.labelKey,
      sortOrder: g.sortOrder,
      moduleKey: g.moduleKey,
      roles: g.roles,
      requireSuperAdminPanel: false,
      items: mapAdminItems(g.rootItems),
    }))
    .filter((g) => g.items.length > 0);
}

export function SuperAdminMenuBuilderSection() {
  const { t } = useI18n();
  const [sub, setSub] = useState<SubMode>('plan');
  const [plans, setPlans] = useState<SaasPlanAdmin[]>([]);
  const [tenants, setTenants] = useState<SuperAdminTenant[]>([]);
  const [planId, setPlanId] = useState('');
  const [tenantId, setTenantId] = useState('');
  const [json, setJson] = useState('[]');
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState('');
  const [tenantFlags, setTenantFlags] = useState<{
    hasCustomMenu: boolean;
    usedPlanMenu: boolean;
    usedGlobalFallback: boolean;
  } | null>(null);

  const [arbol, setArbol] = useState<FuncionalidadArbolDto[]>([]);
  const { byPerm } = useMemo(() => buildFuncionalidadMaps(arbol), [arbol]);

  const [editorMainTab, setEditorMainTab] = useState<EditorMainTab>('visual');
  const [visualTree, setVisualTree] = useState<EditorMenuItem[]>([]);
  const [menuViewMode, setMenuViewMode] = useState<MenuBuilderViewMode>('split');
  const [previewLayout, setPreviewLayout] = useState<MenuPreviewLayout>('vertical');

  const visualLabelKey = t('superadmin.menuBuilder.visualGroupLabel');

  const applyMenuJsonString = useCallback(
    (raw: string) => {
      setJson(raw);
      try {
        const groups = JSON.parse(raw.trim() || '[]') as SessionMenuGroupDto[];
        setVisualTree(sessionGroupsToEditorTree(groups, byPerm));
      } catch {
        setVisualTree([]);
      }
    },
    [byPerm],
  );

  const reloadArbol = useCallback(async () => {
    try {
      const rows = await superAdminService.getFuncionalidadesArbol();
      setArbol(rows);
    } catch {
      setArbol([]);
    }
  }, []);

  useEffect(() => {
    void superAdminService
      .listSaasPlansAdmin()
      .then(setPlans)
      .catch(() => setPlans([]));
    void superAdminService
      .getTenants()
      .then(setTenants)
      .catch(() => setTenants([]));
    void reloadArbol();
  }, [reloadArbol]);

  const syncCatalog = async () => {
    setBusy(true);
    setErr('');
    try {
      await superAdminService.syncFuncionalidadesCatalogo();
      await reloadArbol();
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    } finally {
      setBusy(false);
    }
  };

  const handleVisualTreeChange = useCallback(
    (next: EditorMenuItem[]) => {
      setVisualTree(next);
      setJson(serializeEditorTreeToMenuJson(next, visualLabelKey));
    },
    [visualLabelKey],
  );

  const fillFromGlobal = async () => {
    setErr('');
    try {
      const menu = await superAdminService.getNavigationMenu();
      const sess = adminNavigationToSessionMenu(menu);
      applyMenuJsonString(JSON.stringify(sess, null, 2));
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    }
  };

  const loadPlanSaved = async () => {
    if (!planId) return;
    setErr('');
    try {
      const raw = await superAdminService.getPlanMenuJson(planId);
      if (raw?.trim()) {
        applyMenuJsonString(JSON.stringify(JSON.parse(raw), null, 2));
      } else {
        await fillFromGlobal();
      }
    } catch {
      setErr(t('common.errorGeneric'));
    }
  };

  const savePlan = async () => {
    if (!planId) return;
    setBusy(true);
    setErr('');
    try {
      const trimmed = json.trim();
      if (trimmed.length > 0) {
        const groups = JSON.parse(trimmed) as SessionMenuGroupDto[];
        const v = validateSessionMenuGroups(groups);
        if (v.length) {
          setErr(v.join(' '));
          return;
        }
      }
      await superAdminService.setPlanMenuJson(planId, trimmed.length === 0 ? null : trimmed);
      const next = await superAdminService.listSaasPlansAdmin();
      setPlans(next);
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    } finally {
      setBusy(false);
    }
  };

  const clearPlan = async () => {
    if (!planId) return;
    setBusy(true);
    setErr('');
    try {
      await superAdminService.setPlanMenuJson(planId, null);
      applyMenuJsonString('[]');
      const next = await superAdminService.listSaasPlansAdmin();
      setPlans(next);
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    } finally {
      setBusy(false);
    }
  };

  const loadTenantResolved = async () => {
    if (!tenantId) return;
    setErr('');
    try {
      const r = await superAdminService.getTenantResolvedMenu(tenantId);
      applyMenuJsonString(JSON.stringify(r.menu, null, 2));
      setTenantFlags({
        hasCustomMenu: r.hasCustomMenu,
        usedPlanMenu: r.usedPlanMenu,
        usedGlobalFallback: r.usedGlobalFallback,
      });
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    }
  };

  const saveTenant = async () => {
    if (!tenantId) return;
    setBusy(true);
    setErr('');
    try {
      const trimmed = json.trim();
      if (trimmed.length > 0) {
        const groups = JSON.parse(trimmed) as SessionMenuGroupDto[];
        const v = validateSessionMenuGroups(groups);
        if (v.length) {
          setErr(v.join(' '));
          return;
        }
      }
      await superAdminService.putTenantCustomMenu(tenantId, json);
      await loadTenantResolved();
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    } finally {
      setBusy(false);
    }
  };

  const resetTenant = async () => {
    if (!tenantId) return;
    setBusy(true);
    setErr('');
    try {
      await superAdminService.deleteTenantCustomMenu(tenantId);
      await loadTenantResolved();
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <TableCard>
      <ZHCardSection title={t('superadmin.menuBuilder.title')}>
        <p className="subtle">{t('superadmin.menuBuilder.subtitle')}</p>

        <div className="menu-builder-page-catalog">
          <h3 className="menu-builder-page-catalog__title">{t('superadmin.menuBuilder.treeTitle')}</h3>
          <div className="menu-builder-page-catalog__actions">
            <ZHBtn variant="ghost" size="md" type="button" onClick={() => void syncCatalog()} disabled={busy}>
              {t('superadmin.menuBuilder.syncCatalog')}
            </ZHBtn>
            <ZHBtn variant="ghost" size="md" type="button" onClick={() => void reloadArbol()} disabled={busy}>
              {t('common.refresh')}
            </ZHBtn>
          </div>
        </div>

        <div className="zh-form-tabs" style={{ marginTop: 12 }} role="tablist">
          <button
            type="button"
            role="tab"
            className={editorMainTab === 'visual' ? 'is-active' : ''}
            onClick={() => {
              try {
                const groups = JSON.parse(json.trim() || '[]') as SessionMenuGroupDto[];
                setVisualTree(sessionGroupsToEditorTree(groups, byPerm));
              } catch {
                setVisualTree([]);
              }
              setEditorMainTab('visual');
            }}
          >
            {t('superadmin.menuBuilder.tabVisual')}
          </button>
          <button
            type="button"
            role="tab"
            className={editorMainTab === 'json' ? 'is-active' : ''}
            onClick={() => setEditorMainTab('json')}
          >
            {t('superadmin.menuBuilder.tabJson')}
          </button>
        </div>

        {editorMainTab === 'visual' ? (
          <div style={{ marginTop: 16 }}>
            <MenuBuilder
              catalogArbol={arbol}
              tree={visualTree}
              onTreeChange={handleVisualTreeChange}
              viewMode={menuViewMode}
              onViewModeChange={setMenuViewMode}
              previewLayout={previewLayout}
              onPreviewLayoutChange={setPreviewLayout}
              onBuilderMessage={(msg) => setErr(msg)}
            />
          </div>
        ) : null}

        <div className="zh-form-tabs" style={{ marginTop: 12 }} role="tablist">
          <button type="button" role="tab" className={sub === 'plan' ? 'is-active' : ''} onClick={() => setSub('plan')}>
            {t('superadmin.menuBuilder.byPlan')}
          </button>
          <button type="button" role="tab" className={sub === 'tenant' ? 'is-active' : ''} onClick={() => setSub('tenant')}>
            {t('superadmin.menuBuilder.byTenant')}
          </button>
        </div>

        {err ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={err} /> : null}

        {sub === 'plan' ? (
          <ZHGridRow cols={1}>
            <ZHField label={t('superadmin.menuBuilder.planSelect')}>
              <select className="zh-input" value={planId} onChange={(e) => setPlanId(e.target.value)} disabled={busy}>
                <option value="">{t('common.select')}</option>
                {plans.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name} ({p.code}){p.hasMenuConfig ? ' *' : ''}
                  </option>
                ))}
              </select>
            </ZHField>
          </ZHGridRow>
        ) : (
          <ZHGridRow cols={1}>
            <ZHField label={t('superadmin.menuBuilder.tenantSelect')}>
              <select className="zh-input" value={tenantId} onChange={(e) => setTenantId(e.target.value)} disabled={busy}>
                <option value="">{t('common.select')}</option>
                {tenants.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name} ({x.slug}){x.hasCustomMenu ? ' · menú' : ''}
                  </option>
                ))}
              </select>
            </ZHField>
            {tenantFlags ? (
              <p className="subtle" style={{ marginTop: 4 }}>
                {t('superadmin.menuBuilder.hintFlags')}{' '}
                <strong>
                  {tenantFlags.hasCustomMenu ? 'custom ' : ''}
                  {tenantFlags.usedPlanMenu ? 'plan ' : ''}
                  {tenantFlags.usedGlobalFallback ? 'global' : ''}
                </strong>
              </p>
            ) : null}
          </ZHGridRow>
        )}

        {editorMainTab === 'json' ? (
          <ZHField label={t('superadmin.menuBuilder.jsonLabel')}>
            <textarea
              className="zh-input"
              rows={18}
              value={json}
              onChange={(e) => setJson(e.target.value)}
              spellCheck={false}
              disabled={busy}
              style={{ fontFamily: 'ui-monospace, monospace', fontSize: 12 }}
            />
          </ZHField>
        ) : null}

        <ZHInlineRowRight>
          <ZHBtn variant="ghost" size="md" type="button" onClick={() => void fillFromGlobal()} disabled={busy}>
            {t('superadmin.menuBuilder.loadGlobal')}
          </ZHBtn>
          {sub === 'plan' ? (
            <>
              <ZHBtn variant="ghost" size="md" type="button" onClick={() => void loadPlanSaved()} disabled={busy || !planId}>
                {t('superadmin.menuBuilder.loadSavedPlan')}
              </ZHBtn>
              <ZHBtn variant="ghost" size="md" type="button" onClick={() => void clearPlan()} disabled={busy || !planId}>
                {t('superadmin.menuBuilder.clearPlan')}
              </ZHBtn>
              <ZHBtn variant="primary" size="md" type="button" onClick={() => void savePlan()} disabled={busy || !planId}>
                {t('superadmin.menuBuilder.savePlan')}
              </ZHBtn>
            </>
          ) : (
            <>
              <ZHBtn variant="ghost" size="md" type="button" onClick={() => void loadTenantResolved()} disabled={busy || !tenantId}>
                {t('superadmin.menuBuilder.loadResolvedTenant')}
              </ZHBtn>
              <ZHBtn variant="ghost" size="md" type="button" onClick={() => void resetTenant()} disabled={busy || !tenantId}>
                {t('superadmin.menuBuilder.resetTenant')}
              </ZHBtn>
              <ZHBtn variant="primary" size="md" type="button" onClick={() => void saveTenant()} disabled={busy || !tenantId}>
                {t('superadmin.menuBuilder.saveTenant')}
              </ZHBtn>
            </>
          )}
        </ZHInlineRowRight>
      </ZHCardSection>
    </TableCard>
  );
}
