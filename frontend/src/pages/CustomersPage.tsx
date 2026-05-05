import { useCallback, useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { PageShell, TableCard, EmptyState, LoadingState, Badge, NoAccessPage } from '../components/PageShell';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { useAuthStore } from '../store/authStore';
import {
  customerService,
  type CustomerDetailDto,
  type CustomerDto,
} from '../services/customerService';
import { formatApiError } from '../modules/lib/formatApiError';
import { ZHBtn } from '../components/zh/ZHForm';
import { ZHConfirmModal } from '../components/zh/ZHConfirmModal';
import { customerFormSchema, type CustomerFormValues } from '../schemas/catalog/customerSchema';
import ZHSearchBar from '../components/shared/ZHSearchBar';
import { EntityAuditPanel } from '../components/EntityAuditPanel';
import { CustomerFormFields } from '../components/catalog/CustomerFormFields';
import {
  customerFormFromDto,
  customerFormToApiBody,
  emptyCustomerForm,
} from '../modules/catalog/customers/customerFormModel';
import './CustomersPage.css';

export function CustomersPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');

  const canView = isAdmin || hasPerm('ventas.customers.view');
  const canCreate = isAdmin || hasPerm('ventas.customers.create');
  const canUpdate = isAdmin || hasPerm('ventas.customers.update');
  const canDelete = isAdmin || hasPerm('ventas.customers.delete');

  const [items, setItems] = useState<CustomerDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [listQuery, setListQuery] = useState('');
  const [listApplied, setListApplied] = useState('');
  const [disableConfirm, setDisableConfirm] = useState<CustomerDto | null>(null);
  const [toggleBusy, setToggleBusy] = useState(false);

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
    defaultValues: emptyCustomerForm(),
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
    reset(emptyCustomerForm());
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
      reset(customerFormFromDto(d));
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
        await customerService.update(editingId, { id: editingId, ...customerFormToApiBody(form) });
        await fetchList();
        const d = await customerService.getById(editingId);
        reset(customerFormFromDto(d));
        setAudit({
          createdAt: d.createdAt,
          updatedAt: d.updatedAt,
          createdBy: d.createdBy,
          updatedBy: d.updatedBy,
        });
        setAuditEntityId(editingId);
        setAuditRefreshKey((k) => k + 1);
      } else {
        const created = await customerService.create(customerFormToApiBody(form));
        await fetchList();
        setEditingId(null);
        reset(emptyCustomerForm());
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

  const runToggleActive = async (row: CustomerDto) => {
    setError('');
    setToggleBusy(true);
    try {
      if (row.isActive) {
        if (!canDelete) return;
        await customerService.disable(row.id);
      } else {
        if (!canUpdate) return;
        await customerService.enable(row.id);
      }
      await fetchList();
      setDisableConfirm(null);
    } catch (err: unknown) {
      setError(formatApiError(err) || t('customers.error.toggle'));
    } finally {
      setToggleBusy(false);
    }
  };

  if (!canView) {
    return <NoAccessPage title={t('customers.title')} />;
  }

  return (
    <PageShell
      kicker={t('app.nav.group.sales')}
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
        {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
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

                <CustomerFormFields register={register} control={control} errors={errors} disabled={formDisabled} />

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
                          <ZHBtn
                            variant="ghost"
                            size="xs"
                            type="button"
                            onClick={() => {
                              if (x.isActive) setDisableConfirm(x);
                              else void runToggleActive(x);
                            }}
                          >
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

      {disableConfirm ? (
        <ZHConfirmModal
          title={t('customers.confirmDisable.title')}
          message={
            <>
              {t('customers.confirmDisable.line1')}{' '}
              <strong>{disableConfirm.legalName}</strong>. {t('customers.confirmDisable.line2')}
            </>
          }
          confirmLabel={t('customers.confirmDisable.confirm')}
          cancelLabel={t('common.no')}
          variant="destructive"
          loading={toggleBusy}
          onCancel={() => setDisableConfirm(null)}
          onConfirm={() => void runToggleActive(disableConfirm)}
        />
      ) : null}
    </PageShell>
  );
}
