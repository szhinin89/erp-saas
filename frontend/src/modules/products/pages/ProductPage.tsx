import { useCallback, useEffect, useRef } from 'react';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { NoAccessPage } from '../../../components/PageShell';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';

import { useProducts } from '../hooks/useProducts';
import { useProductUiStore } from '../store/productUiStore';
import type { ProductFormValues } from '../schemas/productSchema';

import { ResumenTab }   from '../components/ResumenTab';
import { ListadoTab }   from '../components/ListadoTab';
import { ProductWizard, buildProductPayload } from '../components/ProductWizard';
import { ToastManager } from '../components/ToastManager';

import '../../../pages/ProductsPage.css';

const TABS = [
  { id: 'resumen'  as const, labelKey: 'products.tabs.resumen', labelFb: 'Resumen',       icon: 'bar_chart_4_bars' },
  { id: 'listado'  as const, labelKey: 'products.tabs.list',    labelFb: 'Listado',        icon: 'view_list'       },
  { id: 'nuevo'    as const, labelKey: 'products.tabs.nuevo',   labelFb: 'Nuevo Producto', icon: 'add_box'         },
] as const;

export function ProductPage() {
  const { t }       = useI18n();
  const { canShow } = usePermissionsUi();
  const canView     = canShow('inventory.products.view');
  const canCreate   = canShow('inventory.products.create');

  const {
    recentProducts,
    productsLoading, productsError,
    catalogs, catalogsLoading, catalogsError,
    creating, createError, createProduct,
    updating, updateError, updateProduct,
    toggling, toggleError, toggleProductStatus,
  } = useProducts();

  const activeTab      = useProductUiStore((s) => s.activeTab);
  const editingProduct = useProductUiStore((s) => s.editingProduct);
  const setActiveTab   = useProductUiStore((s) => s.setActiveTab);
  const cancelEdit     = useProductUiStore((s) => s.cancelEdit);
  const showToast      = useProductUiStore((s) => s.showToast);
  const addActivity    = useProductUiStore((s) => s.addActivity);

  const searchRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        setActiveTab('listado');
        setTimeout(() => searchRef.current?.focus(), 80);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [setActiveTab]);

  const handleSubmit = useCallback(async (values: ProductFormValues): Promise<boolean> => {
    const base = buildProductPayload(values);
    if (editingProduct) {
      const updated = await updateProduct({ id: editingProduct.id, ...base });
      if (!updated) return false;
      addActivity(updated.shortName, 'updated');
      showToast(t('products.updated.success', 'Producto actualizado correctamente.'), 'success');
      cancelEdit();
      return true;
    }
    const created = await createProduct(base);
    if (!created) return false;
    addActivity(created.shortName, 'created');
    showToast(t('products.created.success', 'Producto creado correctamente.'), 'success');
    setActiveTab('listado');
    return true;
  }, [editingProduct, updateProduct, createProduct, addActivity, showToast, cancelEdit, setActiveTab, t]);

  const handleToggle = useCallback(async (id: string, enable: boolean) => {
    const updated = await toggleProductStatus(id, enable);
    if (!updated) return;
    addActivity(updated.shortName, enable ? 'enabled' : 'disabled');
    showToast(
      enable
        ? t('products.toggle.success.activated', 'Producto activado.')
        : t('products.toggle.success.disabled', 'Producto desactivado.'),
      'info',
    );
  }, [toggleProductStatus, addActivity, showToast, t]);

  if (!canView) return <NoAccessPage title={t('products.title')} />;

  const anyError = productsError || catalogsError || createError || updateError || toggleError;
  const nuevoLabel = editingProduct
    ? t('products.tabs.edit', 'Editar Producto')
    : t('products.tabs.nuevo', 'Nuevo Producto');
  const nuevoIcon  = editingProduct ? 'edit' : 'add_box';

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.inventario')}
      title={t('products.title')}
action={
        canCreate ? (
          <ZHBtn variant="primary" size="md" type="button"
            onClick={() => { cancelEdit(); setActiveTab('nuevo'); }}>
            <span className="material-symbols-outlined">add</span>
            {t('products.new')}
          </ZHBtn>
        ) : null
      }
    >
      {anyError && (
        <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={anyError} />
      )}

      {/* Tab bar */}
      <div className="prd-tabs" role="tablist" aria-label="Secciones de productos">
        {TABS.map((tab) => {
          const label  = tab.id === 'nuevo' ? nuevoLabel : t(tab.labelKey, tab.labelFb);
          const icon   = tab.id === 'nuevo' ? nuevoIcon  : tab.icon;
          const active = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              role="tab"
              aria-selected={active}
              className={`prd-tab-btn ${active ? 'prd-tab-btn--active' : ''}`}
              onClick={() => setActiveTab(tab.id)}
            >
              <span className="material-symbols-outlined prd-tab-icon">{icon}</span>
              {label}
              {tab.id === 'nuevo' && editingProduct && (
                <span className="prd-tab-edit-badge" aria-hidden>●</span>
              )}
            </button>
          );
        })}
      </div>

      {/* Tab panels */}
      <div className="prd-tab-content">
        {activeTab === 'resumen' && (
          <ResumenTab products={recentProducts} catalogs={catalogs} />
        )}
        {activeTab === 'listado' && (
          <ListadoTab
            products={recentProducts}
            catalogs={catalogs}
            loading={productsLoading}
            toggling={toggling}
            onToggle={handleToggle}
            searchInputRef={searchRef}
          />
        )}
        {activeTab === 'nuevo' && canCreate && (
          <ProductWizard
            catalogs={catalogs}
            catalogsLoading={catalogsLoading}
            submitting={creating || updating}
            editingProduct={editingProduct}
            onSubmit={handleSubmit}
            onCancel={cancelEdit}
          />
        )}
      </div>

      <ToastManager />
    </ErpPageTemplate>
  );
}
