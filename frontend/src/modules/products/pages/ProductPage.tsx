import { useMemo, useState } from 'react';
import { EmptyState, LoadingState, NoAccessPage, PageShell, TableCard } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import ZHSearchBar from '../../../components/shared/ZHSearchBar';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { useAuthStore } from '../../../store/authStore';
import { useI18n } from '../../../i18n/i18n';
import { ProductForm } from '../components/ProductForm';
import { useProducts } from '../hooks/useProducts';
import { toOptionalGuid, type ProductFormValues } from '../schemas/productSchema';
import '../../../pages/ProductsPage.css';

export function ProductPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const canView = isAdmin || hasPerm('inventario.products.view');
  const canCreate = isAdmin || hasPerm('inventario.products.create');
  const {
    recentProducts,
    productsLoading,
    productsError,
    catalogs,
    catalogsLoading,
    catalogsError,
    createError,
    creating,
    createProduct,
  } = useProducts();
  const [listQuery, setListQuery] = useState('');

  const filteredProducts = useMemo(() => {
    const query = listQuery.trim().toLowerCase();
    if (!query) return recentProducts;
    return recentProducts.filter((product) =>
      `${product.saleCode} ${product.shortName} ${product.description} ${product.purchaseCode ?? ''}`
        .toLowerCase()
        .includes(query)
    );
  }, [listQuery, recentProducts]);

  const handleSubmit = async (values: ProductFormValues) => {
    await createProduct({
      saleCode: values.saleCode,
      shortName: values.shortName,
      description: values.description,
      lineId: values.lineId,
      categoryId: values.categoryId,
      subcategoryId: values.subcategoryId,
      unitOfMeasureId: values.unitOfMeasureId,
      brandId: values.brandId,
      productTypeId: values.productTypeId,
      tariffId: values.tariffId,
      appliesVatOnSale: values.saleTaxId !== '00000000-0000-0000-0000-000000000000',
      saleTaxId: toOptionalGuid(values.saleTaxId),
      saleVatAccountId: null,
      appliesVatOnPurchase: values.purchaseTaxId !== '00000000-0000-0000-0000-000000000000',
      purchaseTaxId: toOptionalGuid(values.purchaseTaxId),
      purchaseVatAccountId: null,
    });
  };

  if (!canView) {
    return <NoAccessPage title={t('products.title')} />;
  }

  return (
    <PageShell
      kicker={`${t('app.nav.group.inventario')} · ${t('products.title')}`}
      title={t('products.title')}
      subtitle={t('products.list.subtitle')}
    >
      {productsError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={productsError} /> : null}
      {catalogsError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={catalogsError} /> : null}
      {createError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={createError} /> : null}

      {canCreate ? (
        <TableCard>
          <ProductForm t={t} catalogs={catalogs} loading={creating || catalogsLoading} onSubmit={handleSubmit} />
        </TableCard>
      ) : null}

      <TableCard>
        <div className="zh-mb-12">
          <ZHSearchBar
            searchQuery={listQuery}
            onSearch={setListQuery}
            onClearAll={() => setListQuery('')}
            filterValues={{}}
            placeholder={t('products.list.searchPlaceholder')}
            resultCount={filteredProducts.length}
            entityLabel={t('products.list.entityLabel')}
            loading={productsLoading}
          />
        </div>
        {productsLoading ? <LoadingState /> : null}
        {!productsLoading && filteredProducts.length === 0 ? (
          <EmptyState message={recentProducts.length === 0 ? t('products.empty') : t('common.listTab.noMatch')} />
        ) : null}
        {!productsLoading && filteredProducts.length > 0 ? (
          <table className="table">
            <thead>
              <tr>
                <th>{t('products.table.code')}</th>
                <th>{t('products.table.name')}</th>
                <th>{t('products.table.description')}</th>
                <th>{t('products.table.createdAt')}</th>
              </tr>
            </thead>
            <tbody>
              {filteredProducts.map((product) => (
                <tr key={product.id}>
                  <td>
                    <span className="mono">{product.saleCode}</span>
                  </td>
                  <td>{product.shortName}</td>
                  <td className="subtle">{product.description}</td>
                  <td>{new Date(product.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
      </TableCard>
    </PageShell>
  );
}
