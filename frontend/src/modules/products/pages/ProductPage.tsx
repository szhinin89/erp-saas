import { useMemo, useState, type ReactNode } from 'react';
import { EmptyState, LoadingState, NoAccessPage, PageShell } from '../../../components/PageShell';
import { Alert, Badge, Button, Card, Input, Tabs } from '../../../components/ui';
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
  const canEdit = isAdmin || hasPerm('inventario.products.edit');
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
    updateError,
    updating,
    updateProduct,
    toggleError,
    toggling,
    toggleProductStatus,
  } = useProducts();
  const [listQuery, setListQuery] = useState('');
  const [editMode, setEditMode] = useState(false);
  const [editingProduct, setEditingProduct] = useState<any>(null);

  const handleEdit = (product: any) => {
    setEditingProduct(product);
    setEditMode(true);
  };

  const handleCancelEdit = () => {
    setEditingProduct(null);
    setEditMode(false);
  };

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
    if (editMode && editingProduct) {
      // Update existing product
      await updateProduct({
        id: editingProduct.id,
        saleCode: values.saleCode,
        purchaseCode: values.purchaseCode || undefined,
        shortName: values.shortName,
        description: values.description,
        observations: values.observations || undefined,
        lineId: values.lineId,
        categoryId: values.categoryId,
        subcategoryId: values.subcategoryId,
        unitOfMeasureId: values.unitOfMeasureId,
        brandId: values.brandId,
        productTypeId: values.productTypeId,
        tariffId: values.tariffId,
        appliesVatOnSale: values.appliesVatOnSale,
        appliesVatOnPurchase: values.appliesVatOnPurchase,
        appliesExciseTax: values.appliesExciseTax,
        saleTaxId: toOptionalGuid(values.saleTaxId),
        purchaseTaxId: toOptionalGuid(values.purchaseTaxId),
        exciseTaxId: toOptionalGuid(values.exciseTaxId),
        saleVatAccountId: null,
        purchaseVatAccountId: null,
        exciseAccountId: null,
        isService: values.isService,
        tracksStock: values.tracksStock,
        tracksLot: values.tracksLot,
        tracksSeries: values.tracksSeries,
        hasRecipe: values.hasRecipe,
        stockWithDecimal: values.stockWithDecimal,
        saleWithDecimal: values.saleWithDecimal,
        maxItemDiscountPercent: values.maxItemDiscountPercent,
        availableOnWeb: values.availableOnWeb,
        availableOnMobile: values.availableOnMobile,
        isEcommerceActive: values.isEcommerceActive,
        isFavorite: values.isFavorite,
        isForSale: values.isForSale,
        baseColor: values.baseColor || undefined,
        hasMultipleColors: values.hasMultipleColors,
        hasSizes: values.hasSizes,
        handlesTariff: values.handlesTariff,
        barcodes: values.barcodes?.map((barcode) => ({ code: barcode.code, type: barcode.type })) ?? [],
      });
      handleCancelEdit();
    } else {
      // Create new product
      await createProduct({
        saleCode: values.saleCode,
        purchaseCode: values.purchaseCode || undefined,
        shortName: values.shortName,
        description: values.description,
        observations: values.observations || undefined,
        lineId: values.lineId,
        categoryId: values.categoryId,
        subcategoryId: values.subcategoryId,
        unitOfMeasureId: values.unitOfMeasureId,
        brandId: values.brandId,
        productTypeId: values.productTypeId,
        tariffId: values.tariffId,
        appliesVatOnSale: values.appliesVatOnSale,
        appliesVatOnPurchase: values.appliesVatOnPurchase,
        appliesExciseTax: values.appliesExciseTax,
        saleTaxId: toOptionalGuid(values.saleTaxId),
        purchaseTaxId: toOptionalGuid(values.purchaseTaxId),
        exciseTaxId: toOptionalGuid(values.exciseTaxId),
        saleVatAccountId: null,
        purchaseVatAccountId: null,
        exciseAccountId: null,
        isService: values.isService,
        tracksStock: values.tracksStock,
        tracksLot: values.tracksLot,
        tracksSeries: values.tracksSeries,
        hasRecipe: values.hasRecipe,
        stockWithDecimal: values.stockWithDecimal,
        saleWithDecimal: values.saleWithDecimal,
        maxItemDiscountPercent: values.maxItemDiscountPercent,
        availableOnWeb: values.availableOnWeb,
        availableOnMobile: values.availableOnMobile,
        isEcommerceActive: values.isEcommerceActive,
        isFavorite: values.isFavorite,
        isForSale: values.isForSale,
        baseColor: values.baseColor || undefined,
        hasMultipleColors: values.hasMultipleColors,
        hasSizes: values.hasSizes,
        handlesTariff: values.handlesTariff,
        barcodes: values.barcodes?.map((barcode) => ({ code: barcode.code, type: barcode.type })) ?? [],
      });
    }
  };

  if (!canView) {
    return <NoAccessPage title={t('products.title')} />;
  }

  const tabs = [
    {
      id: 'formulario',
      label: 'Formulario',
      content: canCreate ? (
        <div className="products-form-wrap">
          <ProductForm
            t={t}
            catalogs={catalogs}
            loading={creating || updating || catalogsLoading}
            onSubmit={handleSubmit}
            editMode={editMode}
            existingProduct={editingProduct}
            onCancelEdit={handleCancelEdit}
          />
        </div>
      ) : (
        <EmptyState message={t('common.noPermission')} />
      ),
    },
    {
      id: 'listado',
      label: 'Listado',
      content: (
        <>
          <div className="products-search-row">
            <Input
              label={t('products.list.searchLabel', 'Buscar producto')}
              placeholder={t('products.list.searchPlaceholder')}
              value={listQuery}
              onChange={(event) => setListQuery(event.target.value)}
            />
          </div>
          {productsLoading ? <LoadingState /> : null}
          {!productsLoading && filteredProducts.length === 0 ? (
            <EmptyState message={recentProducts.length === 0 ? t('products.empty') : t('common.listTab.noMatch')} />
          ) : null}
          {!productsLoading && filteredProducts.length > 0 ? (
            <div className="table-wrapper">
              <table className="products-responsive-table">
                <thead>
                  <tr>
                    <th>{t('products.table.code')}</th>
                    <th>{t('products.table.name')}</th>
                    <th>{t('products.table.description')}</th>
                    <th>{t('products.table.barcodes')}</th>
                    <th>{t('products.table.status')}</th>
                    <th>{t('products.table.createdAt')}</th>
                    {canEdit ? <th>{t('common.actions')}</th> : null}
                  </tr>
                </thead>
                <tbody>
                  {filteredProducts.map((product) => (
                    <tr key={product.id}>
                      <td data-label={t('products.table.code')}>
                        <span className="mono">{product.saleCode}</span>
                      </td>
                      <td data-label={t('products.table.name')}>{product.shortName}</td>
                      <td data-label={t('products.table.description')} className="subtle">
                        {product.description}
                      </td>
                      <td data-label={t('products.table.barcodes')}>
                        {product.barcodes && product.barcodes.length > 0 ? (
                          <div className="products-barcodes-wrap">
                            {product.barcodes.slice(0, 2).map((barcode, index) => (
                              <span key={index} className="mono products-barcode-pill">
                                {barcode.code}
                              </span>
                            ))}
                            {product.barcodes.length > 2 && (
                              <span className="products-barcodes-more">
                                +{product.barcodes.length - 2} {t('products.table.moreBarcodes', 'más')}
                              </span>
                            )}
                          </div>
                        ) : (
                          <span className="subtle products-no-barcodes">{t('products.table.noBarcodes')}</span>
                        )}
                      </td>
                      <td data-label={t('products.table.status')}>
                        <Badge variant={product.isActive ? 'success' : 'danger'}>
                          {product.isActive ? t('common.active') : t('common.inactive')}
                        </Badge>
                      </td>
                      <td data-label={t('products.table.createdAt')}>{new Date(product.createdAt).toLocaleString()}</td>
                      {canEdit ? (
                        <td data-label={t('common.actions')}>
                          <div className="products-actions-cell">
                            <Button
                              variant="secondary"
                              size="sm"
                              onClick={() => handleEdit(product)}
                              disabled={editMode || toggling || !product.isActive}
                            >
                              {t('common.edit')}
                            </Button>
                            <Button
                              variant={product.isActive ? 'danger' : 'success'}
                              size="sm"
                              onClick={() => toggleProductStatus(product.id, !product.isActive)}
                              disabled={toggling || editMode}
                            >
                              {product.isActive ? t('common.disable') : t('common.enable')}
                            </Button>
                          </div>
                        </td>
                      ) : null}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </>
      ),
    },
  ] satisfies { id: string; label: string; content: ReactNode }[];

  return (
    <PageShell
      kicker={`${t('app.nav.group.inventario')} · ${t('products.title')}`}
      title={t('products.title')}
      subtitle={t('products.list.subtitle')}
    >
      <div className="products-shell">
        <Card
          title={
            <span className="products-title">
              <span>📦</span>
              <span>{t('products.title')}</span>
            </span>
          }
          actions={
            editMode ? (
              <Button variant="secondary" size="sm" onClick={handleCancelEdit}>
                {t('common.cancel')}
              </Button>
            ) : null
          }
        >
          {productsError ? <Alert type="error" message={`${t('common.errorPrefix')}: ${productsError}`} /> : null}
          {catalogsError ? <Alert type="error" message={`${t('common.errorPrefix')}: ${catalogsError}`} /> : null}
          {createError ? <Alert type="error" message={`${t('common.errorPrefix')}: ${createError}`} /> : null}
          {updateError ? <Alert type="error" message={`${t('common.errorPrefix')}: ${updateError}`} /> : null}
          {toggleError ? <Alert type="error" message={`${t('common.errorPrefix')}: ${toggleError}`} /> : null}

          <Tabs tabs={tabs} defaultActiveId="formulario" />
        </Card>
      </div>
    </PageShell>
  );
}
