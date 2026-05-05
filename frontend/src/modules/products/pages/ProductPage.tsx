import { PageShell } from '../../../components/PageShell';
import { useI18n } from '../../../i18n/i18n';
import { ProductForm } from '../components/ProductForm';
import { useProducts } from '../hooks/useProducts';
import { toOptionalGuid, type ProductFormValues } from '../schemas/productSchema';

export function ProductPage() {
  const { t } = useI18n();
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

  return (
    <PageShell
      kicker={`${t('app.nav.group.inventario')} · ${t('products.title')}`}
      title={t('products.title')}
      subtitle={t('products.list.subtitle')}
    >
      {productsError ? <p>{productsError}</p> : null}
      {catalogsError ? <p>{catalogsError}</p> : null}
      {createError ? <p>{createError}</p> : null}

      <ProductForm t={t} catalogs={catalogs} loading={creating || catalogsLoading} onSubmit={handleSubmit} />

      <section>
        <h2>{t('products.tabList')}</h2>
        {productsLoading ? <p>{t('common.loading')}</p> : null}
        {!productsLoading && recentProducts.length === 0 ? <p>{t('products.empty')}</p> : null}
        {!productsLoading && recentProducts.length > 0 ? (
          <table>
            <thead>
              <tr>
                <th>{t('products.table.code')}</th>
                <th>{t('products.table.name')}</th>
                <th>{t('products.table.description')}</th>
                <th>{t('products.table.createdAt')}</th>
              </tr>
            </thead>
            <tbody>
              {recentProducts.map((product) => (
                <tr key={product.id}>
                  <td>{product.saleCode}</td>
                  <td>{product.shortName}</td>
                  <td>{product.description}</td>
                  <td>{new Date(product.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
      </section>
    </PageShell>
  );
}
