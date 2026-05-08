import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { EmptyState, LoadingState, NoAccessPage, PageShell, TableCard } from '../../../components/PageShell';
import ZHSearchBar from '../../../components/shared/ZHSearchBar';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { CustomerForm } from '../components/CustomerForm';
import { useCustomers } from '../hooks/useCustomers';
import { customerSchema, defaultCustomerValues, type CustomerFormValues } from '../schemas/customerSchema';

export function CustomersPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const canView = hasPerm('ventas.customers.view');
  const canCreate = hasPerm('ventas.customers.create');

  const [searchQuery, setSearchQuery] = useState('');
  const { customers, loading, error, creating, createError, createCustomer } = useCustomers();

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CustomerFormValues>({
    resolver: zodResolver(customerSchema),
    defaultValues: defaultCustomerValues,
  });

  const filteredCustomers = useMemo(() => {
    const term = searchQuery.trim().toLowerCase();
    if (!term) return customers;
    return customers.filter((customer) => {
      return (
        customer.fullName.toLowerCase().includes(term) ||
        customer.identificationNumber.toLowerCase().includes(term) ||
        (customer.email ?? '').toLowerCase().includes(term) ||
        (customer.phone ?? '').toLowerCase().includes(term)
      );
    });
  }, [customers, searchQuery]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canCreate) return;
    const created = await createCustomer({
      identification: values.identification,
      fullName: values.fullName,
      email: values.email ?? null,
      phone: values.phone ?? null,
      address: values.address ?? null,
    });
    if (created) reset(defaultCustomerValues);
  });

  if (!canView) {
    return <NoAccessPage title={t('customers.title')} />;
  }

  return (
    <PageShell
      kicker={t('app.nav.group.sales')}
      title={t('customers.title')}
      action={
        canCreate ? (
          <ZHBtn variant="primary" size="md" type="button" disabled={creating} onClick={() => void onSubmit()}>
            {creating ? t('common.saving') : t('customers.form.save')}
          </ZHBtn>
        ) : undefined
      }
    >
      <TableCard>
        {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
        {createError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={createError} /> : null}

        {canCreate ? (
          <>
            <CustomerForm register={register} errors={errors} disabled={creating} />
            <div className="zh-mt-12">
              <ZHBtn variant="ghost" size="md" type="button" disabled={creating} onClick={() => reset(defaultCustomerValues)}>
                {t('customers.form.cancel')}
              </ZHBtn>
            </div>
          </>
        ) : null}

        <div className="zh-mt-16">
          <ZHSearchBar
            searchQuery={searchQuery}
            onSearch={setSearchQuery}
            onClearAll={() => setSearchQuery('')}
            filterValues={{}}
            placeholder={t('customers.list.searchPlaceholder')}
            resultCount={filteredCustomers.length}
            entityLabel={t('customers.list.entityLabel')}
            loading={loading}
          />
        </div>

        {loading ? (
          <LoadingState />
        ) : filteredCustomers.length === 0 ? (
          <EmptyState message={t('common.noData')} />
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>{t('customers.form.identification')}</th>
                <th>{t('customers.form.fullName')}</th>
                <th>{t('customers.form.email')}</th>
                <th>{t('customers.form.phone')}</th>
                <th>{t('customers.form.address')}</th>
              </tr>
            </thead>
            <tbody>
              {filteredCustomers.map((customer) => (
                <tr key={customer.id}>
                  <td>{customer.identificationNumber}</td>
                  <td>{customer.fullName}</td>
                  <td>{customer.email ?? '-'}</td>
                  <td>{customer.phone ?? '-'}</td>
                  <td>{customer.address ?? '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </TableCard>
    </PageShell>
  );
}
