import { useEffect, useRef } from 'react';
import {
  serializeEditorTreeToMenuJson,
  type EditorMenuItem,
} from '../menu-builder/menuBuilderTypes';
import { platformService, type CommercialPlanAdmin } from '../../modules/platform/api/platformService';
import type { SessionMenuGroupDto } from '../../types/access';
import { adminNavigationToSessionMenu } from '../../modules/platform/adminNavigationToSessionMenu';
import type { MenuPreviewLayout } from '../menu-builder/MenuPreview';
import type { EditorMainTab } from './platformMenuBuilderUtils';

export type SuperAdminMenuBuilderSharedEffectsParams = {
  crmWorkspace: boolean;
  planId: string;
  plans: CommercialPlanAdmin[];
  editorMainTab: EditorMainTab;
  visualTree: EditorMenuItem[];
  visualLabelKey: string;
  previewLayout: MenuPreviewLayout;
  setPreviewLayout: (v: MenuPreviewLayout) => void;
  setJson: (v: string | ((curr: string) => string)) => void;
  setErr: (v: string) => void;
  applyMenuJsonString: (raw: string) => void;
  t: (key: string) => string;
  hydratingPlanRef: React.MutableRefObject<boolean>;
  planSwitchClock: React.MutableRefObject<number>;
};

export function usePlatformMenuBuilderSharedEffects(params: SuperAdminMenuBuilderSharedEffectsParams): void {
  const {
    crmWorkspace,
    planId,
    plans,
    editorMainTab,
    visualTree,
    visualLabelKey,
    previewLayout,
    setPreviewLayout,
    setJson,
    setErr,
    applyMenuJsonString,
    t,
    hydratingPlanRef,
    planSwitchClock,
  } = params;

  useEffect(() => {
    if (crmWorkspace) return;
    if (!planId) return;
    const p = plans.find((x) => x.id === planId);
    const l = p?.menuSidebarLayout?.trim().toLowerCase();
    if (l === 'horizontal' || l === 'vertical') setPreviewLayout(l);
  }, [crmWorkspace, planId, plans, setPreviewLayout]);

  useEffect(() => {
    if (crmWorkspace || !planId) return;
    let cancelled = false;
    hydratingPlanRef.current = true;
    planSwitchClock.current = Date.now();
    void (async () => {
      try {
        const bundle = await platformService.getPlanMenu(planId);
        if (cancelled) return;
        const l = bundle.menuSidebarLayout?.trim().toLowerCase();
        if (l === 'horizontal' || l === 'vertical') setPreviewLayout(l);
        if (bundle.menuConfigJson?.trim()) {
          applyMenuJsonString(JSON.stringify(JSON.parse(bundle.menuConfigJson), null, 2));
        } else {
          const menu = await platformService.getNavigationMenu();
          const sess = adminNavigationToSessionMenu(menu);
          applyMenuJsonString(JSON.stringify(sess, null, 2));
        }
      } catch {
        if (!cancelled) setErr(t('common.errorGeneric'));
      } finally {
        if (!cancelled) {
          queueMicrotask(() => {
            hydratingPlanRef.current = false;
          });
        }
      }
    })();
    return () => {
      cancelled = true;
      hydratingPlanRef.current = false;
    };
  }, [crmWorkspace, planId, applyMenuJsonString, setErr, setPreviewLayout, t, hydratingPlanRef, planSwitchClock]);

  useEffect(() => {
    if (!crmWorkspace && editorMainTab !== 'visual') return;
    setJson(serializeEditorTreeToMenuJson(visualTree, visualLabelKey, previewLayout));
  }, [visualTree, visualLabelKey, previewLayout, crmWorkspace, editorMainTab, setJson]);

  const layoutPatchSkipMount = useRef(true);
  useEffect(() => {
    if (layoutPatchSkipMount.current) {
      layoutPatchSkipMount.current = false;
      return;
    }
    if (editorMainTab !== 'visual') return;
    setJson((curr) => {
      try {
        const groups = JSON.parse(curr.trim() || '[]') as SessionMenuGroupDto[];
        if (!groups.length) return curr;
        return JSON.stringify(
          groups.map((g) => {
            const o = g as unknown as Record<string, unknown>;
            const code = String(o.code ?? o.Code ?? '')
              .trim()
              .toLowerCase();
            return code === 'plan-custom' ? { ...g, code: 'plan-custom', menuBarLayout: previewLayout } : g;
          }),
          null,
          2,
        );
      } catch {
        return curr;
      }
    });
  }, [previewLayout, editorMainTab, setJson]);
}
