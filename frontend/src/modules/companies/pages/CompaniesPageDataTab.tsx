import type { UseFormRegister, FieldErrors } from 'react-hook-form';
import { useI18n } from '../../../i18n/i18n';
import { CompanyModuleChips } from '../../../components/saas/CompanyModuleChips';
import type { CompanyItem, SubscriberDetailDto } from '../api/companyService';
import type { CreateCompanyFormValues, UpdateSubscriberCompanyFormValues } from '../../../schemas/saas/companySchema';
import { EmptyState, LoadingState, Badge } from '../../../components/PageShell';
import { ZHFormSection, ZHFormBody, ZHGrid, ZHField, ZHBtn } from '../../../components/zh/ZHForm';
import { ZHCard } from '../../../components/zh/ZHCard';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';

type CompaniesPageDataTabProps = {
  items: CompanyItem[];
  filtered: CompanyItem[];
  listQuery: string;
  setListQuery: (value: string) => void;
  loading: boolean;
  creating: boolean;
  detailSubscriberId: string | null;
  subscriberDetail: SubscriberDetailDto | null;
  detailLoading: boolean;
  detailError: string;
  detailSaving: boolean;
  detailSaveOk: boolean;
  detailSaveError: string;
  subscriberId: string;
  maxActiveSubscribers: number | null | undefined;
  maxIdentityUsers: number | null | undefined;
  linkExistingAdmin: boolean;
  register: UseFormRegister<CreateCompanyFormValues>;
  errors: FieldErrors<CreateCompanyFormValues>;
  registerDetail: UseFormRegister<UpdateSubscriberCompanyFormValues>;
  detailFieldErrors: FieldErrors<UpdateSubscriberCompanyFormValues>;
  onClearDetail: () => void;
  onSelectRow: (id: string) => void;
  onSubmitCreate: (e?: React.BaseSyntheticEvent) => Promise<void>;
  onSubmitDetail: (e?: React.BaseSyntheticEvent) => Promise<void>;
};

export function CompaniesPageDataTab({
  items,
  filtered,
  listQuery,
  setListQuery,
  loading,
  creating,
  detailSubscriberId,
  subscriberDetail,
  detailLoading,
  detailError,
  detailSaving,
  detailSaveOk,
  detailSaveError,
  subscriberId,
  maxActiveSubscribers,
  maxIdentityUsers,
  linkExistingAdmin,
  register,
  errors,
  registerDetail,
  detailFieldErrors,
  onClearDetail,
  onSelectRow,
  onSubmitCreate,
  onSubmitDetail,
}: CompaniesPageDataTabProps) {
  const { t } = useI18n();

  return (
    <>
      <div className="companies-data-picker zh-mb-12">
        <div className="companies-picker-toolbar">
          <ZHField label={t('companies.search')}>
            <input
              className="zh-input"
              placeholder={t('companies.search')}
              value={listQuery}
              onChange={(event) => setListQuery(event.target.value)}
              disabled={loading}
            />
          </ZHField>
          <div className="companies-picker-actions">
            <ZHBtn
              variant="secondary"
              size="sm"
              type="button"
              onClick={() => setListQuery('')}
              disabled={loading || !listQuery.trim()}
            >
              {t('common.clear')}
            </ZHBtn>
            <ZHBtn variant="primary" size="sm" type="button" onClick={onClearDetail} disabled={loading}>
              {t('companies.list.newAction')}
            </ZHBtn>
          </div>
        </div>
        <p className="subtle companies-picker-meta">
          {filtered.length} {t('companies.list.entityLabel')}
        </p>
        {loading ? (
          <LoadingState />
        ) : filtered.length === 0 ? (
          <EmptyState message={items.length === 0 ? t('common.noData') : t('common.listTab.noMatch')} />
        ) : (
          <table className="companies-table companies-table--embedded responsive-table companies-responsive-table">
            <thead>
              <tr>
                <th>{t('companies.table.name')}</th>
                <th>{t('companies.table.slug')}</th>
                <th>{t('companies.table.plan')}</th>
                <th>{t('companies.table.modules')}</th>
                <th>{t('companies.table.id')}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((x) => (
                <tr
                  key={x.id}
                  className="zh-tr-clickable"
                  onClick={() => onSelectRow(x.id)}
                >
                  <td data-label={t('companies.table.name')}>{x.name}</td>
                  <td data-label={t('companies.table.slug')}>{x.slug}</td>
                  <td data-label={t('companies.table.plan')} className="companies-plan-cell">{x.planCode?.trim() ? x.planCode : '—'}</td>
                  <td data-label={t('companies.table.modules')} className="companies-modules-cell">
                    <CompanyModuleChips company={x} />
                  </td>
                  <td data-label={t('companies.table.id')} className="mono companies-mono">{x.id}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {!detailSubscriberId ? (
        <div className="companies-inner-stack">
          <ZHCard title={t('companies.form.create')}>
            <form onSubmit={onSubmitCreate}>
              <input type="hidden" name="subscriberId" value={subscriberId} />

              {maxActiveSubscribers != null ? (
                <p className="companies-quota-hint" role="note">
                  {t('companies.deployment.maxSubscribersHint')} <strong>{maxActiveSubscribers}</strong>
                </p>
              ) : null}
              {maxIdentityUsers != null ? (
                <p className="companies-quota-hint" role="note">
                  {t('companies.deployment.maxUsersHint')} <strong>{maxIdentityUsers}</strong>
                </p>
              ) : null}
              <ZHGrid cols={2}>
                <ZHField label={t('companies.form.subscriberName')} required fieldError={errors.subscriberName?.message}>
                  <input className="zh-input" disabled={creating} {...register('subscriberName')} />
                </ZHField>
                <ZHField label={t('companies.form.subscriberSlug')} required fieldError={errors.subscriberSlug?.message}>
                  <input className="zh-input" disabled={creating} {...register('subscriberSlug')} />
                </ZHField>
                <ZHField label={t('companies.form.ruc')} fieldError={errors.ruc?.message}>
                  <input className="zh-input" disabled={creating} {...register('ruc')} />
                </ZHField>
                <ZHField label={t('companies.form.shortName')} fieldError={errors.shortName?.message}>
                  <input className="zh-input" disabled={creating} {...register('shortName')} />
                </ZHField>
                <ZHField label={t('companies.form.tradeName')} fieldError={errors.tradeName?.message}>
                  <input className="zh-input" disabled={creating} {...register('tradeName')} />
                </ZHField>
                <ZHField label={t('companies.form.dinardap')} fieldError={errors.dinardap?.message}>
                  <input className="zh-input" disabled={creating} {...register('dinardap')} />
                </ZHField>
                <ZHField label={t('companies.form.logoUrl')} fieldError={errors.logoUrl?.message}>
                  <input className="zh-input" disabled={creating} {...register('logoUrl')} />
                </ZHField>
                <ZHField label={t('companies.form.displayOrder')} fieldError={errors.displayOrder?.message}>
                  <input className="zh-input" type="number" disabled={creating} {...register('displayOrder', { valueAsNumber: true })} />
                </ZHField>
                <ZHField label={t('companies.form.priority')} fieldError={errors.priority?.message}>
                  <input className="zh-input" type="number" disabled={creating} {...register('priority', { valueAsNumber: true })} />
                </ZHField>
                <ZHField
                  label={t('companies.form.adminFirstName')}
                  required={!linkExistingAdmin}
                  fieldError={errors.adminFirstName?.message}
                >
                  <input className="zh-input" disabled={creating || linkExistingAdmin} {...register('adminFirstName')} />
                </ZHField>
                <ZHField
                  label={t('companies.form.adminLastName')}
                  required={!linkExistingAdmin}
                  fieldError={errors.adminLastName?.message}
                >
                  <input className="zh-input" disabled={creating || linkExistingAdmin} {...register('adminLastName')} />
                </ZHField>
                <ZHField label={t('companies.form.adminEmail')} required fieldError={errors.adminEmail?.message}>
                  <input className="zh-input" type="email" disabled={creating} {...register('adminEmail')} />
                </ZHField>
                <ZHField label={t('companies.form.linkExistingAdmin')}>
                  <label className="companies-checkbox-label">
                    <input type="checkbox" disabled={creating} {...register('linkExistingAdmin')} />
                    <span>{t('companies.form.linkExistingAdminHint')}</span>
                  </label>
                </ZHField>
                {!linkExistingAdmin ? (
                  <ZHField label={t('companies.form.adminPassword')} required fieldError={errors.adminPassword?.message}>
                    <input className="zh-input" type="password" disabled={creating} autoComplete="new-password" {...register('adminPassword')} />
                  </ZHField>
                ) : null}
              </ZHGrid>
              <div className="zh-form-actions-row zh-form-actions-row--end">
                <ZHBtn variant="primary" size="sm" type="submit" disabled={creating}>
                  {creating ? t('companies.form.creating') : t('companies.form.create')}
                </ZHBtn>
              </div>
            </form>
          </ZHCard>
        </div>
      ) : detailLoading ? (
        <LoadingState />
      ) : detailError ? (
        <>
          <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={detailError} />
          <div className="zh-mt-12">
            <ZHBtn variant="secondary" size="sm" type="button" onClick={onClearDetail}>
              {t('companies.data.newCompany')}
            </ZHBtn>
          </div>
        </>
      ) : subscriberDetail ? (
        <div className="companies-inner-stack">
          <ZHCard title={t('companies.data.sectionMeta')}>
            <ZHFormBody standalone>
              <ZHGrid cols={2}>
                <ZHField label={t('companies.table.id')}>
                  <div className="companies-readonly-value mono">{subscriberDetail.id}</div>
                </ZHField>
                <ZHField label={t('companies.data.createdAt')}>
                  <div className="companies-readonly-value">
                    {new Date(subscriberDetail.createdAt).toLocaleString()}
                  </div>
                </ZHField>
                <ZHField label={t('common.status')}>
                  <div className="companies-readonly-value">
                    <Badge
                      label={subscriberDetail.isActive ? t('common.active') : t('common.inactive')}
                      variant={subscriberDetail.isActive ? 'green' : 'gray'}
                    />
                  </div>
                </ZHField>
              </ZHGrid>
            </ZHFormBody>
          </ZHCard>

          <ZHCard title={t('companies.data.sectionCompany')}>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                void onSubmitDetail();
              }}
            >
              {detailSaveOk ? (
                <ZHPageNotice variant="success" message={t('companies.data.saveSuccess')} />
              ) : null}
              {detailSaveError ? (
                <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={detailSaveError} />
              ) : null}
              <ZHFormSection title={t('companies.data.sectionCompany')}>
                <ZHGrid cols={2}>
                  <ZHField label={t('companies.form.subscriberName')} required fieldError={detailFieldErrors.subscriberName?.message}>
                    <input className="zh-input" disabled={detailSaving} {...registerDetail('subscriberName')} />
                  </ZHField>
                  <ZHField label={t('companies.form.subscriberSlug')} required fieldError={detailFieldErrors.subscriberSlug?.message}>
                    <input className="zh-input mono" disabled={detailSaving} {...registerDetail('subscriberSlug')} />
                  </ZHField>
                  <ZHField label={t('companies.form.ruc')} fieldError={detailFieldErrors.ruc?.message}>
                    <input className="zh-input" disabled={detailSaving} {...registerDetail('ruc')} />
                  </ZHField>
                  <ZHField label={t('companies.form.shortName')} fieldError={detailFieldErrors.shortName?.message}>
                    <input className="zh-input" disabled={detailSaving} {...registerDetail('shortName')} />
                  </ZHField>
                  <ZHField label={t('companies.form.tradeName')} fieldError={detailFieldErrors.tradeName?.message}>
                    <input className="zh-input" disabled={detailSaving} {...registerDetail('tradeName')} />
                  </ZHField>
                  <ZHField label={t('companies.form.dinardap')} fieldError={detailFieldErrors.dinardap?.message}>
                    <input className="zh-input" disabled={detailSaving} {...registerDetail('dinardap')} />
                  </ZHField>
                  <ZHField label={t('companies.form.logoUrl')} fieldError={detailFieldErrors.logoUrl?.message}>
                    <input className="zh-input" disabled={detailSaving} {...registerDetail('logoUrl')} />
                  </ZHField>
                  <ZHField label={t('companies.form.displayOrder')} fieldError={detailFieldErrors.displayOrder?.message}>
                    <input className="zh-input" type="number" disabled={detailSaving} {...registerDetail('displayOrder', { valueAsNumber: true })} />
                  </ZHField>
                  <ZHField label={t('companies.form.priority')} fieldError={detailFieldErrors.priority?.message}>
                    <input className="zh-input" type="number" disabled={detailSaving} {...registerDetail('priority', { valueAsNumber: true })} />
                  </ZHField>
                </ZHGrid>
              </ZHFormSection>
              <div className="zh-form-actions-row zh-form-actions-row--end">
                <ZHBtn variant="primary" size="sm" type="submit" disabled={detailSaving}>
                  {detailSaving ? t('companies.data.saving') : t('companies.data.updateGeneral')}
                </ZHBtn>
              </div>
            </form>
          </ZHCard>
        </div>
      ) : null}
    </>
  );
}
