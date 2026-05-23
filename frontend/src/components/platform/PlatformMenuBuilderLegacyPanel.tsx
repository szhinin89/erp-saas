import { Card } from '../ui';
import { ZHBtn, ZHField } from '../zh/ZHForm';
import { ZHCardSection, ZHGridRow, ZHInlineRowRight } from '../zh/ZHLayout';
import { ZHPageNotice } from '../zh/ZHPageNotice';
import { MenuBuilder } from '../menu-builder/MenuBuilder';
import {
  normalizeParsedMenuGroups,
  readPlanCustomMenuBarLayout,
  sessionGroupsToEditorTree,
} from '../menu-builder/menuBuilderTypes';
import type { SessionMenuGroupDto } from '../../types/access';
import type { UsePlatformMenuBuilderReturn } from './usePlatformMenuBuilder';

export type PlatformMenuBuilderLegacyPanelProps = UsePlatformMenuBuilderReturn;

export function PlatformMenuBuilderLegacyPanel(props: PlatformMenuBuilderLegacyPanelProps) {
  const {
    t,
    sub,
    setSub,
    plans,
    planId,
    setPlanId,
    subscriberId,
    setSubscriberId,
    json,
    setJson,
    busy,
    err,
    setErr,
    subscriberMenuFlags,
    arbol,
    byPerm,
    editorMainTab,
    setEditorMainTab,
    visualTree,
    commitEditorTree,
    menuViewMode,
    setMenuViewMode,
    previewLayout,
    setPreviewLayout,
    copySourcePlanId,
    setCopySourcePlanId,
    copyMenu,
    setCopyMenu,
    syncCatalog,
    handleVisualTreeChange,
    fillFromGlobal,
    loadPlanSaved,
    savePlan,
    clearPlan,
    loadTenantResolved,
    saveSubscriber,
    resetSubscriber,
    runCopyFromPlan,
    reloadArbol,
  } = props;

  return (
    <Card>
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

        <div className="zh-form-tabs menu-plan-composer__legacyTabs" role="tablist">
          <button
            type="button"
            role="tab"
            className={editorMainTab === 'visual' ? 'is-active' : ''}
            onClick={() => {
              try {
                const parsed = JSON.parse(json.trim() || '[]') as SessionMenuGroupDto[];
                const groups = Array.isArray(parsed) ? normalizeParsedMenuGroups(parsed) : [];
                const layout = readPlanCustomMenuBarLayout(groups);
                if (layout) setPreviewLayout(layout);
                commitEditorTree(sessionGroupsToEditorTree(groups, byPerm), false);
              } catch {
                commitEditorTree([], false);
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
          <div className="menu-plan-composer__legacyBuilderWrap">
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

        <div className="zh-form-tabs menu-plan-composer__legacyTabs" role="tablist">
          <button type="button" role="tab" className={sub === 'plan' ? 'is-active' : ''} onClick={() => setSub('plan')}>
            {t('superadmin.menuBuilder.byPlan')}
          </button>
          <button type="button" role="tab" className={sub === 'subscriber' ? 'is-active' : ''} onClick={() => setSub('subscriber')}>
            {t('superadmin.menuBuilder.bySubscriber')}
          </button>
        </div>

        {err ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={err} /> : null}

        {sub === 'plan' ? (
          <>
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
            <div className="subtle menu-plan-composer__legacySubtleLabel">
              {t('superadmin.menuBuilder.copyFromTitle')}
            </div>
            <ZHGridRow cols={1}>
              <ZHField label={t('superadmin.menuBuilder.copySourcePlan')}>
                <select
                  className="zh-input"
                  value={copySourcePlanId}
                  onChange={(e) => setCopySourcePlanId(e.target.value)}
                  disabled={busy || !planId}
                >
                  <option value="">{t('common.select')}</option>
                  {plans
                    .filter((p) => p.id !== planId)
                    .map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name} ({p.code})
                      </option>
                    ))}
                </select>
              </ZHField>
            </ZHGridRow>
            <ZHGridRow cols={1}>
              <label className="zh-inline-check">
                <input
                  type="checkbox"
                  checked={copyMenu}
                  onChange={(e) => setCopyMenu(e.target.checked)}
                  disabled={busy}
                />
                <span>{t('superadmin.menuBuilder.copyMenuCheck')}</span>
              </label>
            </ZHGridRow>
            <ZHInlineRowRight>
              <ZHBtn
                variant="ghost"
                size="md"
                type="button"
                onClick={() => void runCopyFromPlan()}
                disabled={busy || !planId || !copySourcePlanId}
              >
                {t('superadmin.menuBuilder.copyExecute')}
              </ZHBtn>
            </ZHInlineRowRight>
          </>
        ) : (
          <ZHGridRow cols={1}>
            <ZHField label={t('superadmin.menuBuilder.subscriberSelect')}>
              <select className="zh-input" value={subscriberId} onChange={(e) => setSubscriberId(e.target.value)} disabled={busy}>
                <option value="">{t('common.select')}</option>
                {props.subscribers.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name} ({x.slug}){x.hasCustomMenu ? ' · menú' : ''}
                  </option>
                ))}
              </select>
            </ZHField>
            {subscriberMenuFlags ? (
              <p className="subtle menu-plan-composer__legacySubtleHelp">
                {t('superadmin.menuBuilder.hintFlags')}{' '}
                <strong>
                  {subscriberMenuFlags.hasCustomMenu ? 'custom ' : ''}
                  {subscriberMenuFlags.usedPlanMenu ? 'plan ' : ''}
                  {subscriberMenuFlags.usedGlobalFallback ? 'global' : ''}
                </strong>
              </p>
            ) : null}
          </ZHGridRow>
        )}

        {editorMainTab === 'json' ? (
          <ZHField label={t('superadmin.menuBuilder.jsonLabel')}>
            <textarea
              className="zh-input menu-plan-composer__legacyJsonTextarea"
              rows={18}
              value={json}
              onChange={(e) => setJson(e.target.value)}
              spellCheck={false}
              disabled={busy}
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
              <ZHBtn variant="ghost" size="md" type="button" onClick={() => void loadTenantResolved()} disabled={busy || !subscriberId}>
                {t('superadmin.menuBuilder.loadResolvedSubscriber')}
              </ZHBtn>
              <ZHBtn variant="ghost" size="md" type="button" onClick={() => void resetSubscriber()} disabled={busy || !subscriberId}>
                {t('superadmin.menuBuilder.resetSubscriber')}
              </ZHBtn>
              <ZHBtn variant="primary" size="md" type="button" onClick={() => void saveSubscriber()} disabled={busy || !subscriberId}>
                {t('superadmin.menuBuilder.saveSubscriber')}
              </ZHBtn>
            </>
          )}
        </ZHInlineRowRight>
      </ZHCardSection>
    </Card>
  );
}
