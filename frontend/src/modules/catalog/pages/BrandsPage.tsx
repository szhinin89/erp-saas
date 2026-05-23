import { useCallback, useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { catalogService, type BrandItem } from '../api/catalogService';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { formatApiRequestError } from '../../lib/apiError';

import { useBrandUiStore } from '../store/brandUiStore';
import { BrandResumenTab }  from '../components/BrandResumenTab';
import { BrandListadoTab }  from '../components/BrandListadoTab';
import { BrandFormTab }     from '../components/BrandFormTab';
import { BrandToastManager } from '../components/BrandToastManager';

import '../../../pages/ProductsPage.css'; // prd-* shared styles
import './BrandsPage.css';                    // cat-brand-* module styles

// ── Schema (unchanged from original) ─────────────────────────
const brandSchema = z.object({
  code:            z.string().min(1, 'Required').max(20),
  name:            z.string().min(1, 'Required').max(120),
  manufacturer:    z.string().max(120).optional(),
  countryOfOrigin: z.string().max(80).optional(),
});
type BrandFormValues = z.infer<typeof brandSchema>;

// ── Tab bar definition ────────────────────────────────────────
const TABS = [
  { id: 'resumen' as const, labelKey: 'brands.tabs.resumen', labelFb: 'Resumen',     icon: 'bar_chart_4_bars' },
  { id: 'listado' as const, labelKey: 'brands.tabs.listado', labelFb: 'Listado',      icon: 'view_list'       },
  { id: 'nuevo'   as const, labelKey: 'brands.tabs.nuevo',   labelFb: 'Nueva Marca',  icon: 'add_box'         },
] as const;

export function BrandsPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();

  const canView   = canShow('inventory.brands.view');
  const canCreate = canShow('inventory.brands.create');
  const canUpdate = canShow('inventory.brands.update');
  const canDelete = canShow('inventory.brands.delete');

  // ── Store ─────────────────────────────────────────────────
  const activeTab    = useBrandUiStore((s) => s.activeTab);
  const editingItem  = useBrandUiStore((s) => s.editingItem);
  const setActiveTab = useBrandUiStore((s) => s.setActiveTab);
  const cancelEdit   = useBrandUiStore((s) => s.cancelEdit);
  const showToast    = useBrandUiStore((s) => s.showToast);
  const addActivity  = useBrandUiStore((s) => s.addActivity);

  // ── Data state ────────────────────────────────────────────
  const [brands,    setBrands]    = useState<BrandItem[]>([]);
  const [loading,   setLoading]   = useState(false);
  const [error,     setError]     = useState('');
  const [saving,    setSaving]    = useState(false);
  const [saveError, setSaveError] = useState('');

  const searchRef = useRef<HTMLInputElement>(null);

  // ── Form ──────────────────────────────────────────────────
  const { register, handleSubmit, reset, formState: { errors } } = useForm<BrandFormValues>({
    resolver: zodResolver(brandSchema),
    defaultValues: { code: '', name: '', manufacturer: '', countryOfOrigin: '' },
  });

  // ── Load ──────────────────────────────────────────────────
  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setBrands(await catalogService.brands(false) ?? []);
    } catch (e) {
      setError(formatApiRequestError(e, { generic: t('brands.error.load') }));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => { if (canView) void load(); }, [canView, load]);

  // ── Ctrl+K ───────────────────────────────────────────────
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

  // ── Submit (inline — detección de éxito directa) ─────────
  const onSubmit = handleSubmit(async (values) => {
    setSaving(true);
    setSaveError('');
    try {
      const payload = {
        code:            values.code.trim(),
        name:            values.name.trim(),
        manufacturer:    values.manufacturer?.trim() || null,
        countryOfOrigin: values.countryOfOrigin?.trim() || null,
      };
      if (editingItem) {
        await catalogService.updateBrand(editingItem.id, payload);
        addActivity(values.name, 'updated');
        showToast(t('brands.updated.success', 'Marca actualizada correctamente.'), 'success');
      } else {
        await catalogService.createBrand(payload);
        addActivity(values.name, 'created');
        showToast(t('brands.created.success', 'Marca creada correctamente.'), 'success');
      }
      await load();
      cancelEdit();
      reset({ code: '', name: '', manufacturer: '', countryOfOrigin: '' });
    } catch (e) {
      setSaveError(formatApiRequestError(e, { generic: t('brands.error.save') }));
    } finally {
      setSaving(false);
    }
  });

  // ── Toggle ────────────────────────────────────────────────
  const handleToggle = useCallback(async (item: BrandItem) => {
    setError('');
    try {
      if (item.isActive) await catalogService.disableBrand(item.id);
      else               await catalogService.enableBrand(item.id);
      addActivity(item.name, item.isActive ? 'disabled' : 'enabled');
      showToast(
        item.isActive
          ? t('brands.toggle.success.disabled', 'Marca desactivada.')
          : t('brands.toggle.success.activated', 'Marca activada.'),
        'info',
      );
      await load();
    } catch (e) {
      setError(formatApiRequestError(e, { generic: t('brands.error.toggle') }));
    }
  }, [load, addActivity, showToast, t]);

  // ── Sync form when editingItem changes (set by BrandListadoTab via store) ──
  useEffect(() => {
    if (editingItem) {
      reset({
        code:            editingItem.code,
        name:            editingItem.name,
        manufacturer:    editingItem.manufacturer ?? '',
        countryOfOrigin: editingItem.countryOfOrigin ?? '',
      });
    }
  }, [editingItem, reset]);

  // ── Cancel: reset form ────────────────────────────────────
  const handleCancel = useCallback(() => {
    cancelEdit();
    setSaveError('');
    reset({ code: '', name: '', manufacturer: '', countryOfOrigin: '' });
  }, [cancelEdit, reset]);

  // ── New brand: clear form + go to tab ────────────────────
  const handleNew = useCallback(() => {
    cancelEdit();
    reset({ code: '', name: '', manufacturer: '', countryOfOrigin: '' });
    setSaveError('');
    setActiveTab('nuevo');
  }, [cancelEdit, reset, setActiveTab]);

  if (!canView) return <NoAccessPage title={t('brands.title', 'Gestión de Marcas')} />;

  const nuevoLabel = editingItem ? t('brands.tabs.edit', 'Editar Marca') : t('brands.tabs.nuevo', 'Nueva Marca');
  const nuevoIcon  = editingItem ? 'edit' : 'add_box';

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.inventario', 'Inventario')}
      title={t('brands.title', 'Gestión de Marcas')}
      action={
        canCreate ? (
          <ZHBtn variant="primary" size="md" type="button" onClick={handleNew}>
            <span className="material-symbols-outlined">add</span>
            {t('brands.new', 'Nueva marca')}
          </ZHBtn>
        ) : undefined
      }
    >
      {error && (
        <ZHPageNotice variant="error" message={t('common.errorPrefix', 'Error:')} detail={error} />
      )}

      {/* Tab bar */}
      <div className="prd-tabs" role="tablist" aria-label={t('brands.title', 'Secciones de marcas')}>
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
              {tab.id === 'nuevo' && editingItem && (
                <span className="prd-tab-edit-badge" aria-hidden>●</span>
              )}
            </button>
          );
        })}
      </div>

      {/* Tab panels */}
      <div className="prd-tab-content">
        {activeTab === 'resumen' && (
          <BrandResumenTab brands={brands} />
        )}

        {activeTab === 'listado' && (
          <BrandListadoTab
            brands={brands}
            loading={loading}
            canUpdate={canUpdate}
            canDelete={canDelete}
            onToggle={handleToggle}
            searchInputRef={searchRef}
          />
        )}

        {activeTab === 'nuevo' && canCreate && (
          <BrandFormTab
            editingId={editingItem?.id ?? null}
            saving={saving}
            saveError={saveError}
            register={register}
            errors={errors}
            onSave={() => void onSubmit()}
            onCancel={handleCancel}
          />
        )}
      </div>

      <BrandToastManager />
    </ErpPageTemplate>
  );
}
