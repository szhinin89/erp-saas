import { useCallback, useEffect, useMemo, useState } from 'react';
import { TableCard, EmptyState, LoadingState, Badge } from '../PageShell';
import { ZHPageNotice } from '../zh/ZHPageNotice';
import { Modal } from '../Modal';
import { ZHConfirmModal } from '../zh/ZHConfirmModal';
import { ZHModalHeader } from '../zh/ZHModalHeader';
import { ZHBtn, ZHField } from '../zh/ZHForm';
import { ZHGridRow, ZHInlineRowRight } from '../zh/ZHLayout';
import {
  superAdminService,
  type CreateSaasFeatureDefinitionBody,
  type SaasFeatureDefinitionAdmin,
  type SaasPlanAdmin,
  type UpdateSaasFeatureDefinitionBody,
} from '../../services/superAdminService';
import { useI18n } from '../../i18n/i18n';
import { formatApiRequestError } from '../../modules/lib/apiError';
import { buildNavGroups } from '../../nav/navConfig';
import './SuperAdminPlansSection.css';

type FeatureFormState = {
  code: string;
  name: string;
  description: string;
  isMetered: boolean;
  kind: string;
  resourceRef: string;
};

function emptyFeatureForm(): FeatureFormState {
  return {
    code: '',
    name: '',
    description: '',
    isMetered: false,
    kind: '0',
    resourceRef: '',
  };
}

function featureToForm(feature: SaasFeatureDefinitionAdmin): FeatureFormState {
  return {
    code: feature.code,
    name: feature.name,
    description: feature.description ?? '',
    isMetered: feature.isMetered,
    kind: String(feature.kind),
    resourceRef: feature.resourceRef ?? '',
  };
}

function normalizeFeatureKind(value: string): 0 | 1 | 2 | 3 {
  const kind = Number.parseInt(value, 10);
  if (kind === 1 || kind === 2 || kind === 3) return kind;
  return 0;
}

type ResourceRefSuggestion = { value: string; kind: 'route' | 'permission' | 'existing' };

const FEATURE_CODE_PATTERN = /^[A-Z][A-Z0-9_]*$/;

function collectNavReferencesFromItems(
  items: Array<{ to: string; permissionKey?: string; permissionKeysAny?: string[]; children?: unknown }>,
  routes: Set<string>,
  permissions: Set<string>,
) {
  for (const item of items) {
    const route = item.to.trim();
    if (route.startsWith('/')) routes.add(route);
    const permission = item.permissionKey?.trim();
    if (permission) permissions.add(permission);
    const permissionAny = item.permissionKeysAny ?? [];
    for (const p of permissionAny) {
      const trimmed = p.trim();
      if (trimmed) permissions.add(trimmed);
    }
    const children = Array.isArray(item.children) ? item.children : [];
    if (children.length > 0) {
      collectNavReferencesFromItems(
        children as Array<{ to: string; permissionKey?: string; permissionKeysAny?: string[]; children?: unknown }>,
        routes,
        permissions,
      );
    }
  }
}

export function SuperAdminFeaturesSection() {
  const { t } = useI18n();

  const [features, setFeatures] = useState<SaasFeatureDefinitionAdmin[]>([]);
  const [plans, setPlans] = useState<SaasPlanAdmin[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const [featureModal, setFeatureModal] = useState<'closed' | 'create' | 'edit'>('closed');
  const [editingFeatureId, setEditingFeatureId] = useState<string | null>(null);
  const [featureForm, setFeatureForm] = useState<FeatureFormState>(emptyFeatureForm);
  const [deleteFeatureId, setDeleteFeatureId] = useState<string | null>(null);
  const kindHintKey =
    featureForm.kind === '1'
      ? 'superadmin.plansAdmin.kindHelp.form'
      : featureForm.kind === '2'
        ? 'superadmin.plansAdmin.kindHelp.quota'
        : featureForm.kind === '3'
          ? 'superadmin.plansAdmin.kindHelp.integration'
          : 'superadmin.plansAdmin.kindHelp.module';
  const codeError =
    featureModal === 'create' && featureForm.code.trim() && !FEATURE_CODE_PATTERN.test(featureForm.code.trim())
      ? t('superadmin.plansAdmin.field.code.invalid')
      : null;

  const loadAll = useCallback(async () => {
    const [defs, planRows] = await Promise.all([
      superAdminService.listSaasFeatureDefinitions(),
      superAdminService.listSaasPlansAdmin(),
    ]);
    const sortedDefs = [...defs].sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
    setFeatures(sortedDefs);
    setPlans(planRows);
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        setError('');
        await loadAll();
      } catch (e) {
        if (!cancelled) {
          setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [loadAll, t]);

  const plansByFeature = useMemo(() => {
    const map = new Map<string, string[]>();
    for (const feature of features) {
      map.set(feature.id, []);
    }
    for (const plan of plans) {
      const label = plan.shortLabel?.trim() || plan.code;
      for (const feature of plan.features) {
        if (!feature.isIncluded) continue;
        const list = map.get(feature.featureId) ?? [];
        list.push(label);
        map.set(feature.featureId, list);
      }
    }
    return map;
  }, [features, plans]);

  const resourceRefSuggestions = useMemo<ResourceRefSuggestion[]>(() => {
    const routes = new Set<string>([
      '/dashboard',
      '/superadmin',
      '/companies',
      '/access',
      '/profiles',
      '/security',
      '/products',
      '/accounting',
    ]);
    const permissions = new Set<string>();
    const navGroups = buildNavGroups((k) => k, { superAdminPanelEnabled: true });
    for (const group of navGroups) {
      collectNavReferencesFromItems(group.items, routes, permissions);
    }

    const existing = new Set<string>();
    for (const feature of features) {
      const ref = feature.resourceRef?.trim();
      if (!ref) continue;
      if (!routes.has(ref) && !permissions.has(ref)) existing.add(ref);
    }

    const result: ResourceRefSuggestion[] = [
      ...Array.from(routes).sort((a, b) => a.localeCompare(b)).map((value) => ({ value, kind: 'route' as const })),
      ...Array.from(permissions).sort((a, b) => a.localeCompare(b)).map((value) => ({ value, kind: 'permission' as const })),
      ...Array.from(existing).sort((a, b) => a.localeCompare(b)).map((value) => ({ value, kind: 'existing' as const })),
    ];
    return result;
  }, [features]);
  const routeSuggestionCount = resourceRefSuggestions.filter((s) => s.kind === 'route').length;
  const permissionSuggestionCount = resourceRefSuggestions.filter((s) => s.kind === 'permission').length;

  const openCreateFeature = () => {
    setEditingFeatureId(null);
    setFeatureForm(emptyFeatureForm());
    setFeatureModal('create');
  };

  const openEditFeature = (feature: SaasFeatureDefinitionAdmin) => {
    setEditingFeatureId(feature.id);
    setFeatureForm(featureToForm(feature));
    setFeatureModal('edit');
  };

  const saveFeature = async () => {
    if (featureModal === 'edit' && !editingFeatureId) return;
    setBusy(true);
    setError('');
    try {
      if (featureModal === 'create') {
        const normalizedCode = featureForm.code.trim().toUpperCase();
        if (!FEATURE_CODE_PATTERN.test(normalizedCode)) {
          setError(t('superadmin.plansAdmin.field.code.invalid'));
          return;
        }
      }
      const bodyBase = {
        name: featureForm.name.trim(),
        description: featureForm.description.trim() || null,
        isMetered: featureForm.isMetered,
        kind: normalizeFeatureKind(featureForm.kind),
        resourceRef: featureForm.resourceRef.trim() || null,
      };
      if (featureModal === 'create') {
        const body: CreateSaasFeatureDefinitionBody = {
          code: featureForm.code.trim().toUpperCase(),
          ...bodyBase,
        };
        await superAdminService.createSaasFeatureDefinition(body);
      } else if (editingFeatureId) {
        const body: UpdateSaasFeatureDefinitionBody = bodyBase;
        await superAdminService.updateSaasFeatureDefinition(editingFeatureId, body);
      }
      setFeatureModal('closed');
      await loadAll();
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = async () => {
    if (!deleteFeatureId) return;
    setBusy(true);
    setError('');
    try {
      await superAdminService.deleteSaasFeatureDefinition(deleteFeatureId);
      setDeleteFeatureId(null);
      await loadAll();
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="sap-plansSection">
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}

      <TableCard>
        <div className="sap-dash-head">
          <div className="sap-dash-headRow">
            <div className="sap-dash-headText">
              <p className="sap-dash-eyebrow">{t('superadmin.plansDashboard.eyebrow')}</p>
              <p className="subtle sap-dash-lead">{t('superadmin.plansAdmin.featureDefsHint')}</p>
            </div>
            <div className="sap-dash-actions">
              <ZHBtn variant="ghost" size="md" type="button" onClick={() => void loadAll()} disabled={busy}>
                {t('superadmin.plansAdmin.refresh')}
              </ZHBtn>
              <ZHBtn variant="primary" size="md" type="button" onClick={openCreateFeature} disabled={busy}>
                {t('superadmin.plansAdmin.newFeature')}
              </ZHBtn>
            </div>
          </div>
        </div>

        {loading ? (
          <LoadingState />
        ) : features.length === 0 ? (
          <EmptyState message={t('common.noData')} />
        ) : (
          <div className="sap-defList">
            {features.map((feature) => {
              const assignedPlans = plansByFeature.get(feature.id) ?? [];
              return (
                <div key={feature.id} className="sap-defRow">
                  <span className="sap-defCode mono">{feature.code}</span>
                  <span className="sap-defName">{feature.name}</span>
                  <Badge
                    label={t(`superadmin.plansAdmin.kind.${feature.kind === 1 ? 'form' : feature.kind === 2 ? 'quota' : feature.kind === 3 ? 'integration' : 'module'}`)}
                    variant="blue"
                  />
                  <Badge
                    label={feature.isMetered ? t('superadmin.plans.metered') : t('superadmin.plans.limitUnlimited')}
                    variant={feature.isMetered ? 'blue' : 'gray'}
                  />
                  {feature.resourceRef ? <span className="subtle mono">{feature.resourceRef}</span> : null}
                  {assignedPlans.length > 0 ? (
                    assignedPlans.map((planCode) => (
                      <span key={`${feature.id}-${planCode}`} className="sap-planPill">{planCode}</span>
                    ))
                  ) : (
                    <span className="subtle">—</span>
                  )}
                  <ZHInlineRowRight>
                    <ZHBtn variant="ghost" size="sm" type="button" onClick={() => openEditFeature(feature)} disabled={busy}>
                      {t('common.edit')}
                    </ZHBtn>
                    <ZHBtn
                      variant="destructive"
                      size="sm"
                      type="button"
                      onClick={() => setDeleteFeatureId(feature.id)}
                      disabled={busy}
                    >
                      {t('common.delete')}
                    </ZHBtn>
                  </ZHInlineRowRight>
                </div>
              );
            })}
          </div>
        )}
      </TableCard>

      {featureModal !== 'closed' ? (
        <Modal
          onClose={() => (busy ? undefined : setFeatureModal('closed'))}
          size="lg"
          header={
            <ZHModalHeader
              title={featureModal === 'create' ? t('superadmin.plansAdmin.newFeature') : t('common.edit')}
              subtitle={t('superadmin.plansAdmin.featureDefsTitle')}
              onClose={() => (busy ? undefined : setFeatureModal('closed'))}
            />
          }
        >
          <p className="subtle">{t('superadmin.plansAdmin.formIntro')}</p>
          <ZHGridRow cols={2}>
            <ZHField
              label={t('superadmin.plansAdmin.field.code')}
              hint={t('superadmin.plansAdmin.field.code.hint')}
              fieldError={codeError}
              hintType="info"
            >
              <input
                className="zh-input"
                value={featureForm.code}
                onChange={(e) => setFeatureForm((s) => ({ ...s, code: e.target.value.toUpperCase() }))}
                disabled={featureModal === 'edit' || busy}
                placeholder={t('superadmin.plansAdmin.field.code.placeholder')}
                maxLength={64}
              />
            </ZHField>
            <ZHField
              label={t('superadmin.plansAdmin.field.name')}
              hint={t('superadmin.plansAdmin.field.name.hint')}
              hintType="info"
            >
              <input
                className="zh-input"
                value={featureForm.name}
                onChange={(e) => setFeatureForm((s) => ({ ...s, name: e.target.value }))}
                disabled={busy}
                placeholder={t('superadmin.plansAdmin.field.name.placeholder')}
              />
            </ZHField>
          </ZHGridRow>
          <ZHField
            label={t('superadmin.plansAdmin.field.description')}
            hint={t('superadmin.plansAdmin.field.description.hint')}
            hintType="muted"
          >
            <textarea
              className="zh-input"
              value={featureForm.description}
              onChange={(e) => setFeatureForm((s) => ({ ...s, description: e.target.value }))}
              rows={3}
              disabled={busy}
              placeholder={t('superadmin.plansAdmin.field.description.placeholder')}
            />
          </ZHField>
          <ZHGridRow cols={2}>
            <ZHField
              label={t('superadmin.plansAdmin.field.kind')}
              hint={`${t('superadmin.plansAdmin.field.kind.hint')} ${t(kindHintKey)}`}
              hintType="info"
            >
              <select
                className="zh-input"
                value={featureForm.kind}
                onChange={(e) => setFeatureForm((s) => ({ ...s, kind: e.target.value }))}
                disabled={busy}
              >
                <option value="0">{t('superadmin.plansAdmin.kind.module')}</option>
                <option value="1">{t('superadmin.plansAdmin.kind.form')}</option>
                <option value="2">{t('superadmin.plansAdmin.kind.quota')}</option>
                <option value="3">{t('superadmin.plansAdmin.kind.integration')}</option>
              </select>
            </ZHField>
            <ZHField
              label={t('superadmin.plansAdmin.field.resourceRef')}
              hint={t('superadmin.plansAdmin.field.resourceRef.hint')}
              hintType="muted"
            >
              <input
                className="zh-input"
                value={featureForm.resourceRef}
                onChange={(e) => setFeatureForm((s) => ({ ...s, resourceRef: e.target.value }))}
                disabled={busy}
                placeholder={t('superadmin.plansAdmin.field.resourceRef.placeholder')}
                list="sap-feature-resource-ref-options"
              />
            </ZHField>
          </ZHGridRow>
          <datalist id="sap-feature-resource-ref-options">
            {resourceRefSuggestions.map((entry) => (
              <option
                key={`${entry.kind}:${entry.value}`}
                value={entry.value}
                label={`${entry.kind === 'route' ? 'Ruta' : entry.kind === 'permission' ? 'Permiso' : 'Referencia'}: ${entry.value}`}
              />
            ))}
          </datalist>
          <p className="subtle">
            {t('superadmin.plansAdmin.field.resourceRef.searchHelp')
              .replace('{{routes}}', String(routeSuggestionCount))
              .replace('{{permissions}}', String(permissionSuggestionCount))}
          </p>
          <label className="sap-check">
            <input
              type="checkbox"
              checked={featureForm.isMetered}
              onChange={(e) => setFeatureForm((s) => ({ ...s, isMetered: e.target.checked }))}
              disabled={busy}
            />
            {t('superadmin.plansAdmin.field.metered')}
          </label>
          <p className="subtle">{t('superadmin.plansAdmin.field.metered.hint')}</p>
          <ZHInlineRowRight>
            <ZHBtn variant="ghost" type="button" onClick={() => setFeatureModal('closed')} disabled={busy}>
              {t('common.cancel')}
            </ZHBtn>
            <ZHBtn variant="primary" type="button" onClick={() => void saveFeature()} disabled={busy}>
              {t('superadmin.plansAdmin.saveFeature')}
            </ZHBtn>
          </ZHInlineRowRight>
        </Modal>
      ) : null}

      {deleteFeatureId ? (
        <ZHConfirmModal
          title={t('superadmin.plansAdmin.featureDeleteTitle')}
          message={t('superadmin.plansAdmin.featureDeleteMessage')}
          confirmLabel={t('common.delete')}
          loading={busy}
          onCancel={() => setDeleteFeatureId(null)}
          onConfirm={() => void handleDelete()}
        />
      ) : null}
    </div>
  );
}
