import { useCallback, useEffect, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge, NoAccessPage } from '../components/PageShell';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { useAuthStore } from '../store/authStore';
import {
  customerService,
  type CustomerDetailDto,
  type CustomerDto,
} from '../services/customerService';
import { formatApiError } from '../modules/lib/formatApiError';
import { ZHBtn, ZHFormSection, ZHGrid, ZHField, ZHToggle } from '../components/zh/ZHForm';
import { customerFormSchema, type CustomerFormValues } from '../schemas/catalog/customerSchema';
import { ZHColSpan } from '../components/zh/ZHLayout';
import ZHSearchBar from '../components/shared/ZHSearchBar';
import { EntityAuditPanel } from '../components/EntityAuditPanel';
import './CustomersPage.css';

const emptyForm = (): CustomerFormValues => ({
  identificationType: 'RUC',
  identificationNumber: '',
  legalName: '',
  tradeName: '',
  addressLine: '',
  phone: '',
  email: '',
  notes: '',
  isActive: true,
});

function fromDto(d: CustomerDto | CustomerDetailDto): CustomerFormValues {
  return {
    identificationType: d.identificationType,
    identificationNumber: d.identificationNumber,
    legalName: d.legalName,
    tradeName: d.tradeName ?? '',
    addressLine: d.addressLine ?? '',
    phone: d.phone ?? '',
    email: d.email ?? '',
    notes: d.notes ?? '',
    isActive: d.isActive,
  };
}

const idTypes = ['RUC', 'CI', 'PASSPORT', 'OTHER'] as const;

export function CustomersPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');

  const canView = isAdmin || hasPerm('catalog.customers.view');
  const canCreate = isAdmin || hasPerm('catalog.customers.create');
  const canUpdate = isAdmin || hasPerm('catalog.customers.update');
  const canDelete = isAdmin || hasPerm('catalog.customers.delete');

  const [items, setItems] = useState<CustomerDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [listQuery, setListQuery] = useState('');
  const [listApplied, setListApplied] = useState('');

  const [editingId, setEditingId] = useState<string | null>(null);
  const {
    register,
    control,
    handleSubmit,
    reset,
    watch,
    formState: { errors },
  } = useForm<CustomerFormValues>({
    resolver: zodResolver(customerFormSchema),
    defaultValues: emptyForm(),
  });
  const [audit, setAudit] = useState<Pick<
    CustomerDetailDto,
    'createdAt' | 'updatedAt' | 'createdBy' | 'updatedBy'
  > | null>(null);

  const [dialogMode, setDialogMode] = useState<'closed' | 'new' | 'edit'>('closed');
  const [uiTab, setUiTab] = useState<'data' | 'list' | 'audit'>('data');
  const [auditRefreshKey, setAuditRefreshKey] = useState(0);
  /** Cliente cuyo historial se muestra en la pestaña Auditoría (puede fijarse desde listado sin abrir el formulario). */
  const [auditEntityId, setAuditEntityId] = useState<string | null>(null);

  const fetchList = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      setItems(await customerService.list('all', listApplied.trim() || undefined));
    } catch (err: unknown) {
      setError(formatApiError(err) || t('customers.error.load'));
    } finally {
      setLoading(false);
    }
  }, [listApplied, t]);

  useEffect(() => {
    const id = window.setTimeout(() => setListApplied(listQuery.trim()), 320);
    return () => window.clearTimeout(id);
  }, [listQuery]);

  useEffect(() => {
    let cancelled = false;
    void Promise.resolve().then(async () => {
      if (!cancelled) await fetchList();
    });
    return () => {
      cancelled = true;
    };
  }, [fetchList]);

  const beginNewForm = useCallback(() => {
    setEditingId(null);
    reset(emptyForm());
    setAudit(null);
    setAuditEntityId(null);
    setDialogMode('new');
  }, [reset]);

  useEffect(() => {
    if (uiTab !== 'data' || dialogMode !== 'closed' || !canCreate) return;
    queueMicrotask(() => {
      beginNewForm();
    });
  }, [uiTab, dialogMode, canCreate, beginNewForm]);

  const openEdit = async (id: string) => {
    setError('');
    try {
      const d = await customerService.getById(id);
      setEditingId(id);
      reset(fromDto(d));
      setAudit({
        createdAt: d.createdAt,
        updatedAt: d.updatedAt,
        createdBy: d.createdBy,
        updatedBy: d.updatedBy,
      });
      setDialogMode('edit');
      setAuditEntityId(id);
      setUiTab('data');
    } catch (err: unknown) {
      setError(formatApiError(err) || t('customers.error.loadOne'));
    }
  };

  const closeDialog = () => {
    setError('');
    setDialogMode('closed');
    setEditingId(null);
    setAudit(null);
    setAuditEntityId(null);
    setUiTab('list');
  };

  const cancelDataTab = () => {
    setError('');
    if (editingId) {
      closeDialog();
    } else if (canCreate) {
      beginNewForm();
    } else {
      closeDialog();
    }
  };

  const payloadBody = (form: CustomerFormValues) => ({
    identificationType: form.identificationType.trim(),
    identificationNumber: form.identificationNumber.trim(),
    legalName: form.legalName.trim(),
    tradeName: form.tradeName.trim() || null,
    addressLine: form.addressLine.trim() || null,
    phone: form.phone.trim() || null,
    email: form.email.trim() || null,
    notes: form.notes.trim() || null,
    isActive: form.isActive,
  });

  const formDisabled = editingId ? !canUpdate : !canCreate;

  const formWatch = watch();
  const canSubmit =
    Boolean(
      formWatch.identificationType.trim() &&
        formWatch.identificationNumber.trim() &&
        formWatch.legalName.trim()
    ) && (editingId ? canUpdate : canCreate);

  const save = handleSubmit(async (form) => {
    setError('');
    setSaving(true);
    try {
      if (editingId) {
        await customerService.update(editingId, { id: editingId, ...payloadBody(form) });
        await fetchList();
        const d = await customerService.getById(editingId);
        reset(fromDto(d));
        setAudit({
          createdAt: d.createdAt,
          updatedAt: d.updatedAt,
          createdBy: d.createdBy,
          updatedBy: d.updatedBy,
        });
        setAuditEntityId(editingId);
        setAuditRefreshKey((k) => k + 1);
      } else {
        const created = await customerService.create(payloadBody(form));
        await fetchList();
        setEditingId(null);
        reset(emptyForm());
        setAudit(null);
        setDialogMode('new');
        if (created?.id) {
          setAuditEntityId(created.id);
          setAuditRefreshKey((k) => k + 1);
          setUiTab('audit');
        }
      }
    } catch (err: unknown) {
      setError(formatApiError(err) || t('customers.error.save'));
    } finally {
      setSaving(false);
    }
  });

  const toggleDisable = async (row: CustomerDto) => {
    setError('');
    try {
      if (row.isActive) {
        if (!canDelete) return;
        await customerService.disable(row.id);
      } else {
        if (!canUpdate) return;
        await customerService.enable(row.id);
      }
      await fetchList();
    } catch (err: unknown) {
      setError(formatApiError(err) || t('customers.error.toggle'));
    }
  };

  if (!canView) {
    return <NoAccessPage title={t('customers.title')} />;
  }

  return (
    <PageShell
      kicker={t('app.nav.group.catalog')}
      title={t('customers.title')}
      action={
        uiTab === 'data' && dialogMode !== 'closed' ? (
          <>
            <ZHBtn variant="ghost" size="md" type="button" disabled={saving} onClick={cancelDataTab}>
              {t('common.cancel')}
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="md"
              type="button"
              disabled={saving || !canSubmit}
              onClick={() => void save()}
            >
              {saving ? t('common.saving') : editingId ? t('common.saveChanges') : t('customers.primaryCreate')}
            </ZHBtn>
          </>
        ) : undefined
      }
    >
      <TableCard>
        {error ? <ErrorState message={error} /> : null}
        <div className="zh-form-tabs" role="tablist">
          <button
            type="button"
            role="tab"
            aria-selected={uiTab === 'data'}
            className={uiTab === 'data' ? 'is-active' : ''}
            onClick={() => setUiTab('data')}
          >
            {t('common.formTab.data')}
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={uiTab === 'list'}
            className={uiTab === 'list' ? 'is-active' : ''}
            onClick={() => setUiTab('list')}
          >
            {t('customers.tabList')}
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={uiTab === 'audit'}
            className={uiTab === 'audit' ? 'is-active' : ''}
            onClick={() => setUiTab('audit')}
          >
            {t('common.formTab.audit')}
          </button>
        </div>

        {uiTab === 'data' && (
          <>
            {dialogMode === 'closed' ? (
              <EmptyState message={t('customers.dataTabHintUpdateOnly')} />
            ) : (
              <div className="customers-data-panel">
                <input type="hidden" name="tenantId" value={tenantId} />

                <ZHFormSection title={t('customers.section.identity')}>
                  <ZHGrid cols={2}>
                    <ZHField label={t('customers.form.identificationType')} required fieldError={errors.identificationType?.message}>
                      <select disabled={formDisabled} {...register('identificationType')}>
                        {idTypes.map((x) => (
                          <option key={x} value={x}>
                            {t(`customers.idType.${x}`)}
                          </option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label={t('customers.form.identificationNumber')} required fieldError={errors.identificationNumber?.message}>
                      <input disabled={formDisabled} autoComplete="off" {...register('identificationNumber')} />
                    </ZHField>
                    <ZHColSpan span={2}>
                      <ZHField label={t('customers.form.legalName')} required fieldError={errors.legalName?.message}>
                        <input disabled={formDisabled} autoComplete="organization" {...register('legalName')} />
                      </ZHField>
                    </ZHColSpan>
                    <ZHColSpan span={2}>
                      <ZHField label={t('customers.form.tradeName')} fieldError={errors.tradeName?.message}>
                        <input disabled={formDisabled} {...register('tradeName')} />
                      </ZHField>
                    </ZHColSpan>
                  </ZHGrid>
                </ZHFormSection>

                <ZHFormSection title={t('customers.section.contact')}>
                  <ZHGrid cols={2}>
                    <ZHColSpan span={2}>
                      <ZHField label={t('customers.form.addressLine')} fieldError={errors.addressLine?.message}>
                        <input disabled={formDisabled} autoComplete="street-address" {...register('addressLine')} />
                      </ZHField>
                    </ZHColSpan>
                    <ZHField label={t('customers.form.phone')} fieldError={errors.phone?.message}>
                      <input disabled={formDisabled} autoComplete="tel" {...register('phone')} />
                    </ZHField>
                    <ZHField label={t('customers.form.email')} fieldError={errors.email?.message}>
                      <input disabled={formDisabled} autoComplete="email" {...register('email')} />
                    </ZHField>
                    <ZHColSpan span={2}>
                      <ZHField label={t('customers.form.notes')} fieldError={errors.notes?.message}>
                        <textarea disabled={formDisabled} rows={3} {...register('notes')} />
                      </ZHField>
                    </ZHColSpan>
                  </ZHGrid>
                </ZHFormSection>

                <ZHFormSection title={t('common.status')}>
                  <ZHGrid cols={1}>
                    <Controller
                      name="isActive"
                      control={control}
                      render={({ field }) => (
                        <ZHToggle
                          label={t('customers.form.enabled')}
                          description={t('customers.form.enabled')}
                          value={field.value}
                          onChange={field.onChange}
                          disabled={formDisabled}
                        />
                      )}
                    />
                  </ZHGrid>
                </ZHFormSection>

                {audit ? (
                  <div className="customers-audit">
                    <p>
                      <strong>{t('customers.audit.createdAt')}</strong> {new Date(audit.createdAt).toLocaleString()}
                    </p>
                    <p>
                      <strong>{t('customers.audit.updatedAt')}</strong>{' '}
                      {audit.updatedAt ? new Date(audit.updatedAt).toLocaleString() : '—'}
                    </p>
                    <p>
                      <strong>{t('customers.audit.createdBy')}</strong> {audit.createdBy}
                    </p>
                    <p>
                      <strong>{t('customers.audit.updatedBy')}</strong> {audit.updatedBy ?? '—'}
                    </p>
                  </div>
                ) : null}
              </div>
            )}
          </>
        )}

        {uiTab === 'audit' ? (
          auditEntityId ? (
            <EntityAuditPanel entityType="Customer" entityId={auditEntityId} take={10} refreshKey={auditRefreshKey} />
          ) : (
            <EmptyState message={t('customers.audit.pickCustomer')} />
          )
        ) : null}

        {uiTab === 'list' && (
          <>
            <div className="zh-mb-12">
              <ZHSearchBar
                searchQuery={listQuery}
                onSearch={setListQuery}
                onClearAll={() => {
                  setListQuery('');
                  setListApplied('');
                }}
                filterValues={{}}
                placeholder={t('customers.list.searchPlaceholder')}
                resultCount={items.length}
                entityLabel={t('customers.list.entityLabel')}
                loading={loading}
                actionLabel={canCreate ? t('customers.list.newAction') : undefined}
                onAction={
                  canCreate
                    ? () => {
                        setUiTab('data');
                        beginNewForm();
                      }
                    : undefined
                }
              />
            </div>

            {loading ? (
              <LoadingState />
            ) : items.length === 0 ? (
              <EmptyState message={listApplied.trim() ? t('common.listTab.noMatch') : t('common.noData')} />
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>{t('customers.col.document')}</th>
                    <th>{t('customers.col.legalName')}</th>
                    <th>{t('customers.col.phone')}</th>
                    <th>{t('customers.col.email')}</th>
                    <th>{t('common.status')}</th>
                    <th>{t('customers.col.actions')}</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((x) => (
                    <tr key={x.id}>
                      <td>
                        <span className="mono">
                          {x.identificationType} {x.identificationNumber}
                        </span>
                      </td>
                      <td>{x.legalName}</td>
                      <td>{x.phone ?? '—'}</td>
                      <td>{x.email ?? '—'}</td>
                      <td>
                        <Badge
                          label={x.isActive ? t('common.active') : t('common.inactive')}
                          variant={x.isActive ? 'green' : 'gray'}
                        />
                      </td>
                      <td>
                        {canUpdate ? (
                          <ZHBtn variant="secondary" size="xs" type="button" onClick={() => void openEdit(x.id)}>
                            {t('common.edit')}
                          </ZHBtn>
                        ) : null}
                        <ZHBtn
                          variant="ghost"
                          size="xs"
                          type="button"
                          onClick={() => {
                            setAuditEntityId(x.id);
                            setAuditRefreshKey((k) => k + 1);
                            setUiTab('audit');
                          }}
                        >
                          {t('common.formTab.audit')}
                        </ZHBtn>
                        {(x.isActive ? canDelete : canUpdate) ? (
                          <ZHBtn variant="ghost" size="xs" type="button" onClick={() => void toggleDisable(x)}>
                            {x.isActive ? t('customers.disable') : t('customers.enable')}
                          </ZHBtn>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}
      </TableCard>
    </PageShell>
  );
}
