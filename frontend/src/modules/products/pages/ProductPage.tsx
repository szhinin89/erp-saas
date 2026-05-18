import { useMemo, useState } from 'react';
import { EmptyState, LoadingState, NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { useAuthStore } from '../../../store/authStore';
import { useI18n } from '../../../i18n/i18n';
import { ProductForm } from '../components/ProductForm';
import { useProducts } from '../hooks/useProducts';
import { toOptionalGuid, type ProductFormValues } from '../schemas/productSchema';
import '../../../pages/ProductsPage.css';

type TabId = 'listado' | 'formulario';

export function ProductPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin   = role === 'Admin' || role === 'SuperAdmin';
  const canView   = isAdmin || hasPerm('inventory.products.view');
  const canCreate = isAdmin || hasPerm('inventory.products.create');
  const canEdit   = isAdmin || hasPerm('inventory.products.edit');

  const {
    recentProducts, productsLoading, productsError,
    catalogs, catalogsLoading, catalogsError,
    createError, creating, createProduct,
    updateError, updating, updateProduct,
    toggleError, toggling, toggleProductStatus,
  } = useProducts();

  const [activeTab, setActiveTab]         = useState<TabId>('listado');
  const [listQuery, setListQuery]         = useState('');
  const [editMode, setEditMode]           = useState(false);
  const [editingProduct, setEditingProduct] = useState<any>(null);

  const filteredProducts = useMemo(() => {
    const q = listQuery.trim().toLowerCase();
    if (!q) return recentProducts;
    return recentProducts.filter((p) =>
      `${p.saleCode} ${p.shortName} ${p.description} ${p.purchaseCode ?? ''}`.toLowerCase().includes(q)
    );
  }, [listQuery, recentProducts]);

  const stats = useMemo(() => ({
    total:    recentProducts.length,
    active:   recentProducts.filter((p) => p.isActive).length,
    inactive: recentProducts.filter((p) => !p.isActive).length,
  }), [recentProducts]);

  const handleEdit = (product: any) => {
    setEditingProduct(product);
    setEditMode(true);
    setActiveTab('formulario');
  };

  const handleCancelEdit = () => {
    setEditingProduct(null);
    setEditMode(false);
    setActiveTab('listado');
  };

  const handleNewProduct = () => {
    setEditingProduct(null);
    setEditMode(false);
    setActiveTab('formulario');
  };

  const handleSubmit = async (values: ProductFormValues) => {
    const base = {
      saleCode:              values.saleCode,
      purchaseCode:          values.purchaseCode || undefined,
      shortName:             values.shortName,
      description:           values.description,
      observations:          values.observations || undefined,
      lineId:                values.lineId,
      categoryId:            values.categoryId,
      subcategoryId:         values.subcategoryId,
      unitOfMeasureId:       values.unitOfMeasureId,
      brandId:               values.brandId,
      productTypeId:         values.productTypeId,
      tariffId:              values.tariffId,
      appliesVatOnSale:      values.appliesVatOnSale,
      appliesVatOnPurchase:  values.appliesVatOnPurchase,
      appliesExciseTax:      values.appliesExciseTax,
      saleTaxId:             toOptionalGuid(values.saleTaxId),
      purchaseTaxId:         toOptionalGuid(values.purchaseTaxId),
      exciseTaxId:           toOptionalGuid(values.exciseTaxId),
      saleVatAccountId:      null,
      purchaseVatAccountId:  null,
      exciseAccountId:       null,
      isService:             values.isService,
      tracksStock:           values.tracksStock,
      tracksLot:             values.tracksLot,
      tracksSeries:          values.tracksSeries,
      hasRecipe:             values.hasRecipe,
      stockWithDecimal:      values.stockWithDecimal,
      saleWithDecimal:       values.saleWithDecimal,
      maxItemDiscountPercent: values.maxItemDiscountPercent,
      availableOnWeb:        values.availableOnWeb,
      availableOnMobile:     values.availableOnMobile,
      isEcommerceActive:     values.isEcommerceActive,
      isFavorite:            values.isFavorite,
      isForSale:             values.isForSale,
      baseColor:             values.baseColor || undefined,
      hasMultipleColors:     values.hasMultipleColors,
      hasSizes:              values.hasSizes,
      handlesTariff:         values.handlesTariff,
      barcodes:              values.barcodes?.map((b) => ({ code: b.code, type: b.type })) ?? [],
    };

    if (editMode && editingProduct) {
      await updateProduct({ id: editingProduct.id, ...base });
      handleCancelEdit();
    } else {
      await createProduct(base);
      setActiveTab('listado');
    }
  };

  if (!canView) return <NoAccessPage title={t('products.title')} />;

  const anyError = productsError || catalogsError || createError || updateError || toggleError;

  return (
    <div className="pg-page">

      {/* ── Page header ── */}
      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Ruta de navegación">
            <span className="pg-breadcrumb-item">{t('app.nav.group.inventario') || 'Inventario'}</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">{t('products.title') || 'Productos'}</span>
          </nav>
          <h1 className="pg-title">{t('products.title') || 'Administración de Productos'}</h1>
          <p className="pg-subtitle">{t('products.list.subtitle') || 'Gestiona el catálogo, niveles de stock y precios unitarios.'}</p>
        </div>
        {canCreate && (
          <div className="pg-header-right">
            {editMode ? (
              <ZHBtn variant="ghost" size="md" type="button" onClick={handleCancelEdit}>
                {t('common.cancel')}
              </ZHBtn>
            ) : (
              <ZHBtn variant="primary" size="md" type="button" onClick={handleNewProduct}>
                <span className="material-symbols-outlined">add</span>
                {t('products.new') || 'Nuevo Producto'}
              </ZHBtn>
            )}
          </div>
        )}
      </div>

      {/* ── Errors ── */}
      {anyError ? (
        <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={anyError} />
      ) : null}

      {/* ── KPI bento grid ── */}
      <div className="prd-kpi-grid">
        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">inventory_2</span>
            </div>
            <span className="badge badge--green">
              <span className="material-symbols-outlined" style={{ fontSize: 12 }}>trending_up</span>
              +12%
            </span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('products.kpi.totalSkus') || 'Total SKUs'}</p>
            <p className="pg-kpi-value">{stats.total.toLocaleString()}</p>
          </div>
        </div>

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--error">
              <span className="material-symbols-outlined">warning</span>
            </div>
            <span className="badge badge--red">{t('products.kpi.urgent') || 'Urgente'}</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('products.kpi.lowStock') || 'Bajo Stock'}</p>
            <p className="pg-kpi-value" style={{ color: 'var(--color-error)' }}>{stats.inactive}</p>
          </div>
        </div>

        <div className="pg-kpi prd-kpi-wide">
          <div className="pg-kpi-top">
            <div>
              <p className="pg-kpi-label">{t('products.kpi.valuation') || 'Valorización de Inventario'}</p>
              <p className="pg-kpi-value">—<span className="pg-kpi-unit">USD</span></p>
            </div>
            <div className="prd-sparkline" aria-hidden>
              <span /><span /><span /><span style={{ background: 'var(--color-primary)' }} />
            </div>
          </div>
        </div>
      </div>

      {/* ── Main section ── */}
      <div className="pg-section">

        {/* Tabs */}
        <div className="pg-section-header">
          <div className="zh-form-tabs" role="tablist" aria-label="Secciones de productos">
            <button
              type="button" role="tab"
              aria-selected={activeTab === 'listado'}
              className={activeTab === 'listado' ? 'is-active' : ''}
              onClick={() => { if (!editMode) setActiveTab('listado'); }}
            >
              <span className="material-symbols-outlined" style={{ fontSize: 16, verticalAlign: 'middle', marginRight: 4 }}>view_list</span>
              {t('products.tabs.list') || 'Listado'}
            </button>
            {canCreate && (
              <button
                type="button" role="tab"
                aria-selected={activeTab === 'formulario'}
                className={activeTab === 'formulario' ? 'is-active' : ''}
                onClick={() => setActiveTab('formulario')}
              >
                <span className="material-symbols-outlined" style={{ fontSize: 16, verticalAlign: 'middle', marginRight: 4 }}>
                  {editMode ? 'edit' : 'add_box'}
                </span>
                {editMode ? t('common.edit') || 'Editar' : t('products.tabs.form') || 'Nuevo Producto'}
              </button>
            )}
          </div>
        </div>

        {/* ── Listado tab ── */}
        {activeTab === 'listado' && (
          <>
            {/* Filter controls */}
            <div className="pg-table-controls">
              <div className="pg-table-controls-left">
                <div className="pg-search">
                  <span className="material-symbols-outlined">search</span>
                  <input
                    className="zh-input"
                    type="search"
                    placeholder={t('products.list.searchPlaceholder') || 'Buscar SKU, nombre…'}
                    value={listQuery}
                    onChange={(e) => setListQuery(e.target.value)}
                    aria-label="Buscar producto"
                  />
                </div>
                <button type="button" className="zh-btn zh-btn--ghost zh-btn--md" aria-label="Filtros">
                  <span className="material-symbols-outlined">filter_list</span>
                  {t('common.filters') || 'Filtros'}
                </button>
              </div>
              <div className="pg-table-controls-right">
                <span>{t('common.showing') || 'Mostrando'} {filteredProducts.length} / {recentProducts.length}</span>
                <div className="pg-pagination-controls">
                  <button type="button" className="pg-pagination-btn" disabled aria-label="Anterior">
                    <span className="material-symbols-outlined">chevron_left</span>
                  </button>
                  <button type="button" className="pg-pagination-btn" aria-label="Siguiente">
                    <span className="material-symbols-outlined">chevron_right</span>
                  </button>
                </div>
              </div>
            </div>

            {/* Table */}
            {productsLoading ? (
              <LoadingState />
            ) : filteredProducts.length === 0 ? (
              <EmptyState message={recentProducts.length === 0 ? t('products.empty') : t('common.listTab.noMatch')} />
            ) : (
              <>
                <div className="prd-table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th>{t('products.table.code')  || 'SKU'}</th>
                        <th>{t('products.table.name')  || 'Descripción'}</th>
                        <th>Categoría</th>
                        <th>{t('products.table.status') || 'Estado'}</th>
                        <th>{t('products.table.createdAt') || 'Creado'}</th>
                        {canEdit ? <th style={{ textAlign: 'right' }}>{t('common.actions') || 'Acciones'}</th> : null}
                      </tr>
                    </thead>
                    <tbody>
                      {filteredProducts.map((product) => (
                        <tr key={product.id} style={{ opacity: product.isActive ? 1 : 0.65 }}>
                          <td>
                            <span className="mono prd-sku">{product.saleCode}</span>
                          </td>
                          <td>
                            <div className="prd-product-cell">
                              <div className="zh-avatar zh-avatar--square prd-thumb" aria-hidden>
                                <span className="material-symbols-outlined" style={{ fontSize: 18, color: 'var(--color-primary)' }}>inventory_2</span>
                              </div>
                              <div>
                                <div className="prd-name">{product.shortName}</div>
                                <div className="subtle" style={{ fontSize: 11 }}>{product.description}</div>
                              </div>
                            </div>
                          </td>
                          <td>
                            <span className="badge badge--gray">
                              {(product as any).categoryName ?? '—'}
                            </span>
                          </td>
                          <td>
                            <span className={product.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                              {product.isActive ? t('common.active') : t('common.inactive')}
                            </span>
                          </td>
                          <td className="subtle mono" style={{ fontSize: 11 }}>
                            {new Date(product.createdAt).toLocaleDateString()}
                          </td>
                          {canEdit ? (
                            <td style={{ textAlign: 'right' }}>
                              <div className="prd-actions-cell">
                                <button
                                  type="button"
                                  className="zh-btn zh-btn--ghost zh-btn--sm"
                                  title={t('common.edit') || 'Editar'}
                                  disabled={editMode || toggling || !product.isActive}
                                  onClick={() => handleEdit(product)}
                                  aria-label="Editar producto"
                                >
                                  <span className="material-symbols-outlined">edit</span>
                                </button>
                                <button
                                  type="button"
                                  className={`zh-btn zh-btn--ghost zh-btn--sm ${product.isActive ? 'prd-btn-mute' : 'prd-btn-activate'}`}
                                  title={product.isActive ? t('common.disable') || 'Desactivar' : t('common.enable') || 'Activar'}
                                  disabled={toggling || editMode}
                                  onClick={() => void toggleProductStatus(product.id, !product.isActive)}
                                  aria-label={product.isActive ? 'Desactivar producto' : 'Activar producto'}
                                >
                                  <span className="material-symbols-outlined">
                                    {product.isActive ? 'visibility_off' : 'visibility'}
                                  </span>
                                </button>
                              </div>
                            </td>
                          ) : null}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {/* Table footer */}
                <div className="pg-table-footer">
                  <p className="subtle" style={{ fontSize: 12, margin: 0 }}>
                    {filteredProducts.length} {t('products.list.results') || 'resultados'}
                  </p>
                  <p className="pg-table-timestamp">
                    {t('products.list.lastUpdated') || 'Actualizado en esta sesión'}
                  </p>
                </div>
              </>
            )}
          </>
        )}

        {/* ── Formulario tab ── */}
        {activeTab === 'formulario' && (
          canCreate ? (
            <div className="prd-form-wrap">
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
          )
        )}
      </div>
    </div>
  );
}
