import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { profileService, type Profile } from '../services/profileService';
import { PageShell, TableCard, EmptyState, LoadingState, NoAccessPage } from '../components/PageShell';
import { ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHBtn } from '../components/zh/ZHForm';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { ZHCardSection, ZHInlineRowRight, ZHActionsRow } from '../components/zh/ZHLayout';
import ZHSearchBar from '../components/shared/ZHSearchBar';
import { ZHFormCard } from '../components/zh/ZHFormCard';
import { profileCreateSchema, type ProfileCreateFormValues } from '../schemas/access/profileSchema';
import './ProfilesPage.css';

type ProfileTab = 'data' | 'perms' | 'list';

export function ProfilesPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');

  const [items, setItems] = useState<Profile[]>([]);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ProfileCreateFormValues>({
    resolver: zodResolver(profileCreateSchema),
    defaultValues: { name: '', description: '' },
  });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [tab, setTab] = useState<ProfileTab>('data');
  const [listQuery, setListQuery] = useState('');

  const [selected, setSelected] = useState<Profile | null>(null);
  const [permLoading, setPermLoading] = useState(false);
  const [permError, setPermError] = useState('');
  const [permSaving, setPermSaving] = useState(false);
  const [permState, setPermState] = useState<Record<string, boolean>>({});
  const createFormRef = useRef<HTMLFormElement>(null);

  const permissionCatalog = useMemo(
    () => [
      { key: 'inventario.products.view', label: t('profiles.perms.inventario.products.view') },
      { key: 'inventario.products.create', label: t('profiles.perms.inventario.products.create') },
      { key: 'inventario.products.update', label: t('profiles.perms.inventario.products.update') },
      { key: 'inventario.products.delete', label: t('profiles.perms.inventario.products.delete') },
      { key: 'ventas.customers.view', label: t('profiles.perms.ventas.customers.view') },
      { key: 'ventas.customers.create', label: t('profiles.perms.ventas.customers.create') },
      { key: 'ventas.customers.update', label: t('profiles.perms.ventas.customers.update') },
      { key: 'ventas.customers.delete', label: t('profiles.perms.ventas.customers.delete') },
      { key: 'ventas.facturas.view', label: t('profiles.perms.ventas.facturas.view') },
      { key: 'ventas.facturas.create', label: t('profiles.perms.ventas.facturas.create') },
      { key: 'ventas.facturas.validate', label: t('profiles.perms.ventas.facturas.validate') },
      { key: 'ventas.facturas.emit', label: t('profiles.perms.ventas.facturas.emit') },
      { key: 'ventas.facturas.cancel', label: t('profiles.perms.ventas.facturas.cancel') },
      { key: 'inventario.bodegas.view', label: t('profiles.perms.inventario.bodegas.view') },
      { key: 'inventario.bodegas.create', label: t('profiles.perms.inventario.bodegas.create') },
      { key: 'inventario.bodegas.update', label: t('profiles.perms.inventario.bodegas.update') },
      { key: 'inventario.bodegas.delete', label: t('profiles.perms.inventario.bodegas.delete') },
      { key: 'compras.facturas.view', label: t('profiles.perms.compras.facturas.view') },
      { key: 'compras.facturas.create', label: t('profiles.perms.compras.facturas.create') },
      { key: 'compras.facturas.validate', label: t('profiles.perms.compras.facturas.validate') },
      { key: 'compras.facturas.approve', label: t('profiles.perms.compras.facturas.approve') },
      { key: 'compras.facturas.reject', label: t('profiles.perms.compras.facturas.reject') },
      { key: 'compras.notas-proveedor.view', label: t('profiles.perms.compras.notas-proveedor.view') },
      { key: 'compras.notas-proveedor.create', label: t('profiles.perms.compras.notas-proveedor.create') },
      { key: 'compras.notas-proveedor.approve', label: t('profiles.perms.compras.notas-proveedor.approve') },
      { key: 'compras.proveedores.view', label: t('profiles.perms.compras.proveedores.view') },
      { key: 'compras.proveedores.create', label: t('profiles.perms.compras.proveedores.create') },
      { key: 'compras.proveedores.update', label: t('profiles.perms.compras.proveedores.update') },
      { key: 'compras.proveedores.delete', label: t('profiles.perms.compras.proveedores.delete') },
      { key: 'gastos.facturas.view', label: t('profiles.perms.gastos.facturas.view') },
      { key: 'gastos.facturas.create', label: t('profiles.perms.gastos.facturas.create') },
      { key: 'gastos.facturas.validate', label: t('profiles.perms.gastos.facturas.validate') },
      { key: 'gastos.facturas.approve', label: t('profiles.perms.gastos.facturas.approve') },
      { key: 'gastos.facturas.reject', label: t('profiles.perms.gastos.facturas.reject') },
      { key: 'inventario.brands.view', label: t('profiles.perms.inventario.brands.view') },
      { key: 'inventario.brands.create', label: t('profiles.perms.inventario.brands.create') },
      { key: 'inventario.brands.update', label: t('profiles.perms.inventario.brands.update') },
      { key: 'inventario.brands.delete', label: t('profiles.perms.inventario.brands.delete') },
      { key: 'inventario.productTypes.view', label: t('profiles.perms.inventario.productTypes.view') },
      { key: 'inventario.productTypes.create', label: t('profiles.perms.inventario.productTypes.create') },
      { key: 'inventario.productTypes.update', label: t('profiles.perms.inventario.productTypes.update') },
      { key: 'inventario.productTypes.delete', label: t('profiles.perms.inventario.productTypes.delete') },
      { key: 'inventario.units.view', label: t('profiles.perms.inventario.units.view') },
      { key: 'inventario.units.create', label: t('profiles.perms.inventario.units.create') },
      { key: 'inventario.units.update', label: t('profiles.perms.inventario.units.update') },
      { key: 'inventario.units.delete', label: t('profiles.perms.inventario.units.delete') },
      { key: 'inventario.taxRates.view', label: t('profiles.perms.inventario.taxRates.view') },
      { key: 'inventario.taxRates.create', label: t('profiles.perms.inventario.taxRates.create') },
      { key: 'inventario.taxRates.update', label: t('profiles.perms.inventario.taxRates.update') },
      { key: 'inventario.taxRates.delete', label: t('profiles.perms.inventario.taxRates.delete') },
      { key: 'inventario.tariffs.view', label: t('profiles.perms.inventario.tariffs.view') },
      { key: 'inventario.tariffs.create', label: t('profiles.perms.inventario.tariffs.create') },
      { key: 'inventario.tariffs.update', label: t('profiles.perms.inventario.tariffs.update') },
      { key: 'inventario.tariffs.delete', label: t('profiles.perms.inventario.tariffs.delete') },
      { key: 'inventario.productLines.view', label: t('profiles.perms.inventario.productLines.view') },
      { key: 'inventario.productLines.create', label: t('profiles.perms.inventario.productLines.create') },
      { key: 'inventario.productLines.update', label: t('profiles.perms.inventario.productLines.update') },
      { key: 'inventario.productLines.delete', label: t('profiles.perms.inventario.productLines.delete') },
      { key: 'inventario.categories.view', label: t('profiles.perms.inventario.categories.view') },
      { key: 'inventario.categories.create', label: t('profiles.perms.inventario.categories.create') },
      { key: 'inventario.categories.update', label: t('profiles.perms.inventario.categories.update') },
      { key: 'inventario.categories.delete', label: t('profiles.perms.inventario.categories.delete') },
      { key: 'inventario.subcategories.view', label: t('profiles.perms.inventario.subcategories.view') },
      { key: 'inventario.subcategories.create', label: t('profiles.perms.inventario.subcategories.create') },
      { key: 'inventario.subcategories.update', label: t('profiles.perms.inventario.subcategories.update') },
      { key: 'inventario.subcategories.delete', label: t('profiles.perms.inventario.subcategories.delete') },
      { key: 'accounting.accounts.view', label: t('profiles.perms.accounting.accounts.view') },
      { key: 'accounting.accounts.create', label: t('profiles.perms.accounting.accounts.create') },
      { key: 'accounting.journal.view', label: t('profiles.perms.accounting.journal.view') },
      { key: 'accounting.journal.create', label: t('profiles.perms.accounting.journal.create') },
      { key: 'accounting.journal.edit', label: t('profiles.perms.accounting.journal.edit') },
      { key: 'accounting.config.view', label: t('profiles.perms.accounting.config.view') },
      { key: 'accounting.config.edit', label: t('profiles.perms.accounting.config.edit') },
      { key: 'saas.branches.view', label: t('profiles.perms.saas.branches.view') },
      { key: 'saas.branches.create', label: t('profiles.perms.saas.branches.create') },
      { key: 'saas.branches.update', label: t('profiles.perms.saas.branches.update') },
      { key: 'saas.branches.delete', label: t('profiles.perms.saas.branches.delete') },
    ],
    [t]
  );

  const refresh = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      setItems(await profileService.list(false));
    } catch {
      setError(t('profiles.error.load'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    let cancelled = false;
    void Promise.resolve().then(async () => {
      if (!cancelled) await refresh();
    });
    return () => {
      cancelled = true;
    };
  }, [refresh]);

  const listFiltered = useMemo(() => {
    const q = listQuery.trim().toLowerCase();
    if (!q) return items;
    return items.filter((p) =>
      `${p.name} ${p.description ?? ''} ${p.id}`.toLowerCase().includes(q)
    );
  }, [items, listQuery]);

  const create = handleSubmit(async (values) => {
    setError('');
    try {
      await profileService.create(values.name, values.description || undefined);
      reset({ name: '', description: '' });
      await refresh();
      setTab('list');
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('profiles.error.create');
      setError(msg);
    }
  });

  const toggle = async (p: Profile) => {
    setError('');
    try {
      await profileService.update({ ...p, isActive: !p.isActive });
      await refresh();
    } catch {
      setError(t('profiles.error.update'));
    }
  };

  const loadPermissions = useCallback(
    async (profileId: string) => {
      setPermError('');
      setPermLoading(true);
      try {
        const res = await profileService.getPermissions(profileId);
        const next: Record<string, boolean> = {};
        for (const p of res?.items ?? []) next[p.permissionKey] = !!p.isAllowed;
        for (const def of permissionCatalog) {
          if (next[def.key] === undefined) next[def.key] = false;
        }
        setPermState(next);
      } catch (err: unknown) {
        const msg =
          (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          t('profiles.perms.error.load');
        setPermError(msg);
      } finally {
        setPermLoading(false);
      }
    },
    [permissionCatalog, t]
  );

  const selectProfile = async (p: Profile) => {
    setSelected(p);
    setTab('perms');
    await loadPermissions(p.id);
  };

  const savePermissions = async () => {
    if (!selected) return;
    setPermError('');
    setPermSaving(true);
    try {
      const permItems = Object.entries(permState).map(([permissionKey, isAllowed]) => ({ permissionKey, isAllowed }));
      await profileService.upsertPermissions(selected.id, permItems);
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('profiles.perms.error.save');
      setPermError(msg);
    } finally {
      setPermSaving(false);
    }
  };

  if (!user || (user.role !== 'Admin' && user.role !== 'SuperAdmin')) {
    return <NoAccessPage title={t('profiles.title')} />;
  }

  return (
    <PageShell
      kicker={t('app.nav.group.configuracion')}
      title={t('profiles.title')}
      subtitle={t('profiles.subtitle')}
      action={
        tab === 'data' ? (
          <ZHBtn variant="primary" size="md" type="button" disabled={loading} onClick={() => createFormRef.current?.requestSubmit()}>
            {t('profiles.form.create')}
          </ZHBtn>
        ) : tab === 'perms' && selected ? (
          <ZHBtn variant="primary" size="md" type="button" onClick={() => void savePermissions()} disabled={permLoading || permSaving}>
            {permSaving ? t('common.saving') : t('common.saveChanges')}
          </ZHBtn>
        ) : undefined
      }
    >
      <TableCard>
        {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
        <div className="zh-form-tabs" role="tablist">
          <button type="button" className={tab === 'data' ? 'is-active' : ''} onClick={() => setTab('data')}>
            {t('common.formTab.data')}
          </button>
          <button type="button" className={tab === 'perms' ? 'is-active' : ''} onClick={() => setTab('perms')}>
            {t('profiles.tab.permissions')}
          </button>
          <button type="button" className={tab === 'list' ? 'is-active' : ''} onClick={() => setTab('list')}>
            {t('profiles.tabList')}
          </button>
        </div>

        {tab === 'data' && (
          <ZHFormCard ref={createFormRef} hideHeader title={t('profiles.title')} subtitle={t('profiles.subtitle')} onSubmit={create}>
            <input type="hidden" name="tenantId" value={tenantId} />

            <ZHFormSection title={t('profiles.form.create')}>
              <ZHGrid cols={2}>
                <ZHField label={t('profiles.form.name')} required fieldError={errors.name?.message}>
                  <input disabled={loading} {...register('name')} />
                </ZHField>
                <ZHField label={t('profiles.form.description')} fieldError={errors.description?.message}>
                  <input disabled={loading} {...register('description')} />
                </ZHField>
              </ZHGrid>
            </ZHFormSection>
          </ZHFormCard>
        )}

        {tab === 'list' && (
          <>
            <div className="zh-mb-12">
              <ZHSearchBar
                searchQuery={listQuery}
                onSearch={setListQuery}
                onClearAll={() => setListQuery('')}
                filterValues={{}}
                placeholder={t('common.zhList.searchPlaceholder')}
                resultCount={listFiltered.length}
                entityLabel={t('common.zhList.entityLabel')}
                loading={loading}
                actionLabel={t('profiles.list.newAction')}
                onAction={() => setTab('data')}
              />
            </div>
            {loading ? (
              <LoadingState />
            ) : items.length === 0 ? (
              <EmptyState message={t('common.noData')} />
            ) : listFiltered.length === 0 ? (
              <EmptyState message={t('common.listTab.noMatch')} />
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>{t('profiles.table.name')}</th>
                    <th>{t('profiles.table.active')}</th>
                    <th>{t('profiles.table.id')}</th>
                  </tr>
                </thead>
                <tbody>
                  {listFiltered.map((p) => (
                    <tr key={p.id}>
                      <td>
                        <div className="zh-card-section-title">{p.name}</div>
                        {p.description ? <div className="zh-text-muted zh-text-xs">{p.description}</div> : null}
                      </td>
                      <td>
                        <ZHActionsRow>
                          <ZHBtn variant="secondary" onClick={() => toggle(p)} type="button">
                            {p.isActive ? t('profiles.actions.disable') : t('profiles.actions.enable')}
                          </ZHBtn>
                          <ZHBtn variant="ghost" onClick={() => void selectProfile(p)} type="button">
                            {t('profiles.actions.permissions')}
                          </ZHBtn>
                        </ZHActionsRow>
                      </td>
                      <td className="mono">{p.id}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}

        {tab === 'perms' && (
          <>
            {!selected ? (
              <EmptyState message={t('profiles.perms.pickFromList')} />
            ) : (
              <ZHFormBody standalone>
                <ZHCardSection
                  title={`${t('profiles.perms.title')} — ${selected.name}`}
                  right={
                    <ZHInlineRowRight>
                      <ZHBtn variant="ghost" size="md" type="button" onClick={() => setSelected(null)} disabled={permSaving}>
                        {t('common.cancel')}
                      </ZHBtn>
                    </ZHInlineRowRight>
                  }
                >
                  <div className="subtle">{t('profiles.perms.subtitle')}</div>
                </ZHCardSection>

                {permError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={permError} /> : null}
                {permLoading ? (
                  <LoadingState />
                ) : (
                  <div className="profiles-permsGrid">
                    {permissionCatalog.map((def) => (
                      <label key={def.key} className="profiles-permItem">
                        <input
                          type="checkbox"
                          checked={!!permState[def.key]}
                          onChange={(e) => setPermState((s) => ({ ...s, [def.key]: e.target.checked }))}
                        />
                        <div className="profiles-permText">
                          <span className="profiles-permLabel">{def.label}</span>
                          <span className="mono profiles-permKey">{def.key}</span>
                        </div>
                      </label>
                    ))}
                  </div>
                )}
              </ZHFormBody>
            )}
          </>
        )}
      </TableCard>
    </PageShell>
  );
}
