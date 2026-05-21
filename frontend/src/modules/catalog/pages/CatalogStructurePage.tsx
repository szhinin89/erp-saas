import { useCallback, useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { useAuthStore } from '../../../store/authStore';
import {
  catalogService,
  type CatalogItem,
  type ProductCategoryListItem,
  type ProductSubcategoryListItem,
} from '../api/catalogService';
import { formatApiError } from '../../lib/formatApiError';

/* ── Types ──────────────────────────────────────────────────── */
type ModalType =
  | { kind: 'create-line' }
  | { kind: 'create-category' }
  | { kind: 'create-subcategory' }
  | { kind: 'edit-line';         item: CatalogItem }
  | { kind: 'edit-category';     item: ProductCategoryListItem }
  | { kind: 'edit-subcategory';  item: ProductSubcategoryListItem };

type ModalForm = {
  code: string;
  name: string;
  lineId: string;
  categoryId: string;
};

/* ── Cascade item component ─────────────────────────────────── */
type CascadeItemProps = {
  id: string;
  icon: string;
  name: string;
  subtitle?: string;
  isSelected: boolean;
  isActive: boolean;
  onSelect: () => void;
  onEdit?: () => void;
  onToggle?: () => void;
  canEdit?: boolean;
  canToggle?: boolean;
  disabled?: boolean;
};

function CascadeItem({ icon, name, subtitle, isSelected, isActive, onSelect, onEdit, onToggle, canEdit, canToggle, disabled }: CascadeItemProps) {
  const [hovered, setHovered] = useState(false);
  return (
    <div
      role="button"
      tabIndex={0}
      onClick={onSelect}
      onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') onSelect(); }}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        padding: '10px 12px', borderRadius: 6, cursor: 'pointer',
        background: isSelected
          ? 'var(--color-surface-container-high)'
          : hovered ? 'var(--color-surface-container-low)' : 'transparent',
        border: isSelected ? '1px solid rgba(58,95,132,0.2)' : '1px solid transparent',
        transition: 'background 0.15s',
        opacity: isActive ? 1 : 0.6,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, flex: 1, minWidth: 0 }}>
        <span
          className="material-symbols-outlined"
          style={{
            fontSize: 20, flexShrink: 0,
            color: isSelected ? 'var(--color-primary)' : 'var(--color-text-secondary)',
            fontVariationSettings: isSelected ? "'FILL' 1" : "'FILL' 0",
          }}
        >
          {icon}
        </span>
        <div style={{ minWidth: 0 }}>
          <p style={{
            margin: 0, fontSize: 13, fontWeight: 500,
            color: isSelected ? 'var(--color-primary)' : 'var(--color-text)',
            overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
          }}>
            {name}
          </p>
          {subtitle && (
            <p style={{ margin: 0, fontSize: 10, color: 'var(--color-text-secondary)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {subtitle}
            </p>
          )}
        </div>
      </div>
      <div style={{ display: 'flex', gap: 2, opacity: hovered || isSelected ? 1 : 0, transition: 'opacity 0.15s', flexShrink: 0 }}>
        {canEdit && (
          <button
            type="button"
            className="zh-btn zh-btn--ghost zh-btn--sm"
            onClick={(e) => { e.stopPropagation(); onEdit?.(); }}
            disabled={disabled}
            aria-label="Editar"
          >
            <span className="material-symbols-outlined" style={{ fontSize: 16 }}>edit</span>
          </button>
        )}
        {canToggle && (
          <button
            type="button"
            className="zh-btn zh-btn--ghost zh-btn--sm"
            onClick={(e) => { e.stopPropagation(); onToggle?.(); }}
            disabled={disabled}
            aria-label={isActive ? 'Desactivar' : 'Activar'}
          >
            <span className="material-symbols-outlined" style={{ fontSize: 16 }}>
              {isActive ? 'visibility_off' : 'visibility'}
            </span>
          </button>
        )}
      </div>
    </div>
  );
}

/* ── Column panel ───────────────────────────────────────────── */
type CascadeColumnProps = {
  icon: string;
  title: string;
  filterLabel?: string;
  loading: boolean;
  empty: boolean;
  children: React.ReactNode;
  onAdd?: () => void;
  canCreate?: boolean;
};

function CascadeColumn({ icon, title, filterLabel, loading, empty, children, onAdd, canCreate }: CascadeColumnProps) {
  return (
    <div style={{
      background: 'var(--color-surface)',
      border: '1px solid var(--color-border)',
      borderRadius: 'var(--radius-lg)',
      display: 'flex', flexDirection: 'column',
      minHeight: 540, overflow: 'hidden',
    }}>
      <div className="pg-section-header" style={{ borderRadius: 0, borderTop: 'none', borderLeft: 'none', borderRight: 'none' }}>
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon" style={{ fontSize: 18 }}>{icon}</span>
          <span className="pg-section-label">{title}</span>
        </div>
        {canCreate && onAdd && (
          <button type="button" className="zh-btn zh-btn--primary zh-btn--sm" onClick={onAdd}>
            <span className="material-symbols-outlined" style={{ fontSize: 15 }}>add</span>
          </button>
        )}
      </div>

      {filterLabel && (
        <div style={{
          padding: '6px 16px',
          background: 'var(--color-primary-lt, #e8f0f8)',
          borderBottom: '1px solid var(--color-border)',
          fontSize: 11, color: 'var(--color-primary)', fontWeight: 500,
        }}>
          {filterLabel}
        </div>
      )}

      <div style={{ flex: 1, overflowY: 'auto', padding: '8px 6px' }}>
        {loading ? (
          <p className="subtle" style={{ padding: 16, textAlign: 'center', fontSize: 12 }}>Cargando…</p>
        ) : empty ? (
          <p className="subtle" style={{ padding: 16, textAlign: 'center', fontSize: 12 }}>Sin registros</p>
        ) : (
          children
        )}
      </div>
    </div>
  );
}

/* ── Main page ──────────────────────────────────────────────── */
export function CatalogStructurePage() {
  const { t } = useI18n();
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm = usePermissionsStore((s) => s.has);

  const canViewLines        = isAdmin || hasPerm('inventory.product-lines.view');
  const canViewCategories   = isAdmin || hasPerm('inventory.categories.view');
  const canViewSubcategories= isAdmin || hasPerm('inventory.subcategories.view');
  const canView             = canViewLines && canViewCategories && canViewSubcategories;
  const canCreateLines      = isAdmin || hasPerm('inventory.product-lines.create');
  const canCreateCategories = isAdmin || hasPerm('inventory.categories.create');
  const canCreateSubcategories = isAdmin || hasPerm('inventory.subcategories.create');
  const canUpdateLines      = isAdmin || hasPerm('inventory.product-lines.update');
  const canDeleteLines      = isAdmin || hasPerm('inventory.product-lines.delete');
  const canUpdateCategories = isAdmin || hasPerm('inventory.categories.update');
  const canDeleteCategories = isAdmin || hasPerm('inventory.categories.delete');
  const canUpdateSubcategories = isAdmin || hasPerm('inventory.subcategories.update');
  const canDeleteSubcategories = isAdmin || hasPerm('inventory.subcategories.delete');

  /* ── Data state ──────────────────────────────────────────── */
  const [lines,         setLines]         = useState<CatalogItem[]>([]);
  const [categories,    setCategories]    = useState<ProductCategoryListItem[]>([]);
  const [subcategories, setSubcategories] = useState<ProductSubcategoryListItem[]>([]);
  const [linesLoading,  setLinesLoading]  = useState(false);
  const [catsLoading,   setCatsLoading]   = useState(false);
  const [subsLoading,   setSubsLoading]   = useState(false);

  /* ── Selection ───────────────────────────────────────────── */
  const [selectedLineId,     setSelectedLineId]     = useState<string | null>(null);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);

  const selectedLine     = lines.find((l) => l.id === selectedLineId) ?? null;
  const selectedCategory = categories.find((c) => c.id === selectedCategoryId) ?? null;

  /* ── Modal + form ────────────────────────────────────────── */
  const [modal, setModal] = useState<ModalType | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [modalCats, setModalCats] = useState<ProductCategoryListItem[]>([]);

  const { register, handleSubmit, reset, watch, setValue, formState: { errors } } = useForm<ModalForm>({
    defaultValues: { code: '', name: '', lineId: '', categoryId: '' },
  });

  const watchedLineId = watch('lineId');

  /* ── Load data ───────────────────────────────────────────── */
  const loadLines = useCallback(async () => {
    setLinesLoading(true);
    try {
      setLines(await catalogService.productLines({ activeStatus: 'all' }) ?? []);
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setLinesLoading(false);
    }
  }, []);

  const loadCategories = useCallback(async (lineId: string) => {
    setCatsLoading(true);
    try {
      setCategories(await catalogService.categories({ activeStatus: 'all', lineId }) ?? []);
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setCatsLoading(false);
    }
  }, []);

  const loadSubcategories = useCallback(async (categoryId: string) => {
    setSubsLoading(true);
    try {
      setSubcategories(await catalogService.subcategories({ activeStatus: 'all', categoryId }) ?? []);
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setSubsLoading(false);
    }
  }, []);

  useEffect(() => { if (canView) void loadLines(); }, [canView, loadLines]);

  useEffect(() => {
    if (!selectedLineId) { setCategories([]); setSubcategories([]); return; }
    setSelectedCategoryId(null);
    setSubcategories([]);
    void loadCategories(selectedLineId);
  }, [selectedLineId, loadCategories]);

  useEffect(() => {
    if (!selectedCategoryId) { setSubcategories([]); return; }
    void loadSubcategories(selectedCategoryId);
  }, [selectedCategoryId, loadSubcategories]);

  /* Load categories for subcategory modal when lineId changes */
  useEffect(() => {
    if (!modal || (modal.kind !== 'create-subcategory' && modal.kind !== 'edit-subcategory')) return;
    if (!watchedLineId) { setModalCats([]); return; }
    catalogService.categories({ activeStatus: 'all', lineId: watchedLineId })
      .then((d) => setModalCats(d ?? []))
      .catch(() => setModalCats([]));
  }, [modal, watchedLineId]);

  /* ── Selection handlers ──────────────────────────────────── */
  const selectLine = (id: string) => {
    setSelectedLineId((prev) => prev === id ? null : id);
  };

  const selectCategory = (id: string) => {
    setSelectedCategoryId((prev) => prev === id ? null : id);
  };

  /* ── Modal helpers ───────────────────────────────────────── */
  const openCreate = (kind: 'create-line' | 'create-category' | 'create-subcategory') => {
    setError('');
    reset({ code: '', name: '', lineId: selectedLineId ?? '', categoryId: selectedCategoryId ?? '' });
    setModal({ kind });
  };

  const openEdit = (item: CatalogItem | ProductCategoryListItem | ProductSubcategoryListItem, kind: ModalType['kind']) => {
    setError('');
    if (kind === 'edit-line') {
      const row = item as CatalogItem;
      reset({ code: row.code, name: row.name, lineId: '', categoryId: '' });
      setModal({ kind, item: row });
    } else if (kind === 'edit-category') {
      const row = item as ProductCategoryListItem;
      reset({ code: row.code, name: row.name, lineId: row.lineId, categoryId: '' });
      setModal({ kind, item: row });
    } else if (kind === 'edit-subcategory') {
      const row = item as ProductSubcategoryListItem;
      reset({ code: row.code, name: row.name, lineId: row.lineId, categoryId: row.categoryId });
      setModal({ kind, item: row });
    }
  };

  const closeModal = () => { setModal(null); setError(''); };

  /* ── Submit modal ────────────────────────────────────────── */
  const onSubmit = handleSubmit(async (values) => {
    if (!modal) return;
    setSaving(true);
    setError('');
    try {
      const code = values.code.trim();
      const name = values.name.trim();

      switch (modal.kind) {
        case 'create-line':
          await catalogService.createProductLine({ code, name });
          await loadLines();
          break;
        case 'edit-line':
          await catalogService.updateProductLine(modal.item.id, { code, name });
          await loadLines();
          break;
        case 'create-category':
          await catalogService.createCategory({ code, name, lineId: values.lineId });
          if (selectedLineId) await loadCategories(selectedLineId);
          break;
        case 'edit-category':
          await catalogService.updateCategory(modal.item.id, { code, name, lineId: values.lineId });
          if (selectedLineId) await loadCategories(selectedLineId);
          break;
        case 'create-subcategory':
          await catalogService.createSubcategory({ code, name, categoryId: values.categoryId });
          if (selectedCategoryId) await loadSubcategories(selectedCategoryId);
          break;
        case 'edit-subcategory':
          await catalogService.updateSubcategory(modal.item.id, { code, name, categoryId: values.categoryId });
          if (selectedCategoryId) await loadSubcategories(selectedCategoryId);
          break;
      }
      closeModal();
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setSaving(false);
    }
  });

  /* ── Toggle active/inactive ──────────────────────────────── */
  const toggleLine = async (item: CatalogItem) => {
    setSaving(true);
    try {
      if (item.isActive) await catalogService.disableProductLine(item.id);
      else await catalogService.enableProductLine(item.id);
      await loadLines();
    } catch (e) { setError(formatApiError(e)); }
    finally { setSaving(false); }
  };

  const toggleCategory = async (item: ProductCategoryListItem) => {
    setSaving(true);
    try {
      if (item.isActive) await catalogService.disableCategory(item.id);
      else await catalogService.enableCategory(item.id);
      if (selectedLineId) await loadCategories(selectedLineId);
    } catch (e) { setError(formatApiError(e)); }
    finally { setSaving(false); }
  };

  const toggleSubcategory = async (item: ProductSubcategoryListItem) => {
    setSaving(true);
    try {
      if (item.isActive) await catalogService.disableSubcategory(item.id);
      else await catalogService.enableSubcategory(item.id);
      if (selectedCategoryId) await loadSubcategories(selectedCategoryId);
    } catch (e) { setError(formatApiError(e)); }
    finally { setSaving(false); }
  };

  /* ── Modal title ─────────────────────────────────────────── */
  const modalTitle = () => {
    if (!modal) return '';
    const labels: Record<ModalType['kind'], string> = {
      'create-line':         t('catalog.structure.primaryCreateLine'),
      'edit-line':           t('catalog.structure.editLine'),
      'create-category':     t('catalog.structure.primaryCreateCategory'),
      'edit-category':       t('catalog.structure.editCategory'),
      'create-subcategory':  t('catalog.structure.primaryCreateSubcategory'),
      'edit-subcategory':    t('catalog.structure.editSubcategory'),
    };
    return labels[modal.kind] ?? '';
  };

  const showLineSelector    = modal?.kind === 'create-category' || modal?.kind === 'edit-category' || modal?.kind === 'create-subcategory' || modal?.kind === 'edit-subcategory';
  const showCategorySelector= modal?.kind === 'create-subcategory' || modal?.kind === 'edit-subcategory';

  if (!canView) return <NoAccessPage title={t('catalog.structure.title')} />;

  return (
    <div className="pg-page">

      {/* ── Header ───────────────────────────────────────────── */}
      <div className="pg-header-row">
        <div>
          <p className="pg-kicker">{t('app.nav.group.inventario')}</p>
          <h1 className="pg-title">{t('catalog.structure.title')}</h1>
          <p className="pg-subtitle">{t('catalog.structure.subtitle')}</p>
        </div>
      </div>

      {/* ── Error ────────────────────────────────────────────── */}
      {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}

      {/* ── 3-column cascade grid ─────────────────────────────  */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 'var(--space-4)', alignItems: 'start' }}>

        {/* Level 1: Lines */}
        <CascadeColumn
          icon="account_tree"
          title={t('catalog.structure.level1')}
          loading={linesLoading}
          empty={lines.length === 0}
          canCreate={canCreateLines}
          onAdd={() => openCreate('create-line')}
        >
          {lines.map((line) => (
            <CascadeItem
              key={line.id}
              id={line.id}
              icon="folder"
              name={line.name}
              subtitle={`${line.code}`}
              isSelected={selectedLineId === line.id}
              isActive={line.isActive}
              onSelect={() => selectLine(line.id)}
              canEdit={canUpdateLines}
              canToggle={canDeleteLines || canUpdateLines}
              onEdit={() => openEdit(line, 'edit-line')}
              onToggle={() => void toggleLine(line)}
              disabled={saving}
            />
          ))}
        </CascadeColumn>

        {/* Level 2: Categories */}
        <CascadeColumn
          icon="category"
          title={t('catalog.structure.level2')}
          filterLabel={selectedLine ? `${t('catalog.structure.filteringBy')}: ${selectedLine.name}` : undefined}
          loading={catsLoading}
          empty={!selectedLineId ? true : categories.length === 0}
          canCreate={canCreateCategories && !!selectedLineId}
          onAdd={() => openCreate('create-category')}
        >
          {!selectedLineId ? (
            <p className="subtle" style={{ padding: 16, textAlign: 'center', fontSize: 12 }}>
              {t('catalog.structure.selectLineFirst')}
            </p>
          ) : categories.map((cat) => (
            <CascadeItem
              key={cat.id}
              id={cat.id}
              icon="list"
              name={cat.name}
              subtitle={`${cat.code}`}
              isSelected={selectedCategoryId === cat.id}
              isActive={cat.isActive}
              onSelect={() => selectCategory(cat.id)}
              canEdit={canUpdateCategories}
              canToggle={canDeleteCategories || canUpdateCategories}
              onEdit={() => openEdit(cat, 'edit-category')}
              onToggle={() => void toggleCategory(cat)}
              disabled={saving}
            />
          ))}
        </CascadeColumn>

        {/* Level 3: Subcategories */}
        <CascadeColumn
          icon="layers"
          title={t('catalog.structure.level3')}
          filterLabel={selectedCategory ? `${t('catalog.structure.filteringBy')}: ${selectedCategory.name}` : undefined}
          loading={subsLoading}
          empty={!selectedCategoryId ? true : subcategories.length === 0}
          canCreate={canCreateSubcategories && !!selectedCategoryId}
          onAdd={() => openCreate('create-subcategory')}
        >
          {!selectedCategoryId ? (
            <p className="subtle" style={{ padding: 16, textAlign: 'center', fontSize: 12 }}>
              {t('catalog.structure.selectCategoryFirst')}
            </p>
          ) : subcategories.map((sub) => (
            <CascadeItem
              key={sub.id}
              id={sub.id}
              icon="subdirectory_arrow_right"
              name={sub.name}
              subtitle={`${sub.code} · ${sub.isActive ? t('common.active') : t('common.inactive')}`}
              isSelected={false}
              isActive={sub.isActive}
              onSelect={() => {}}
              canEdit={canUpdateSubcategories}
              canToggle={canDeleteSubcategories || canUpdateSubcategories}
              onEdit={() => openEdit(sub, 'edit-subcategory')}
              onToggle={() => void toggleSubcategory(sub)}
              disabled={saving}
            />
          ))}
        </CascadeColumn>
      </div>

      {/* ── Summary bar ──────────────────────────────────────── */}
      {(selectedLine || selectedCategory) && (
        <div className="pg-section" style={{ marginTop: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">info</span>
              <span className="pg-section-label">{t('catalog.structure.selectionSummary')}</span>
            </div>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-6)', flexWrap: 'wrap', padding: 'var(--space-3) 0 0' }}>
            <div>
              <p className="subtle" style={{ margin: '0 0 4px', fontSize: 10, textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 600 }}>
                {t('catalog.structure.hierarchyPath')}
              </p>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, fontWeight: 500 }}>
                {selectedLine && <span>{selectedLine.name}</span>}
                {selectedCategory && (
                  <>
                    <span className="material-symbols-outlined" style={{ fontSize: 14, color: 'var(--color-text-secondary)' }}>arrow_forward</span>
                    <span style={{ color: 'var(--color-primary)' }}>{selectedCategory.name}</span>
                  </>
                )}
              </div>
            </div>
            {selectedLineId && (
              <>
                <div style={{ width: 1, height: 36, background: 'var(--color-border)' }} />
                <div>
                  <p className="subtle" style={{ margin: '0 0 4px', fontSize: 10, textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 600 }}>
                    {t('catalog.structure.categoriesCount')}
                  </p>
                  <p style={{ margin: 0, fontSize: 13, fontWeight: 500 }}>{categories.length}</p>
                </div>
              </>
            )}
            {selectedCategoryId && (
              <>
                <div style={{ width: 1, height: 36, background: 'var(--color-border)' }} />
                <div>
                  <p className="subtle" style={{ margin: '0 0 4px', fontSize: 10, textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 600 }}>
                    {t('catalog.structure.subcategoriesCount')}
                  </p>
                  <p style={{ margin: 0, fontSize: 13, fontWeight: 500 }}>{subcategories.length}</p>
                </div>
              </>
            )}
          </div>
        </div>
      )}

      {/* ── Create / Edit modal ───────────────────────────────── */}
      {modal && (
        <div
          className="zh-modal-overlay"
          role="dialog"
          aria-modal="true"
          onClick={(e) => { if (e.target === e.currentTarget) closeModal(); }}
        >
          <div className="zh-modal" style={{ maxWidth: 480 }}>

            <div className="zh-modal-header">
              <h2 style={{ margin: 0, fontSize: 16, fontWeight: 600 }}>{modalTitle()}</h2>
              <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" onClick={closeModal} aria-label={t('common.close')}>
                <span className="material-symbols-outlined">close</span>
              </button>
            </div>

            <form onSubmit={onSubmit}>
              <div className="zh-modal-body">
                {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}

                <div className="pg-form-grid--2">
                  <ZHField label={t('common.code')} required error={errors.code?.message}>
                    <input className="zh-input" {...register('code', { required: t('common.required') })} disabled={saving} />
                  </ZHField>
                  <ZHField label={t('common.name')} required error={errors.name?.message}>
                    <input className="zh-input" {...register('name', { required: t('common.required') })} disabled={saving} />
                  </ZHField>
                </div>

                {showLineSelector && (
                  <ZHField label={t('catalog.categories.line')} required error={errors.lineId?.message}>
                    <select
                      className="zh-input"
                      disabled={saving}
                      {...register('lineId', {
                        required: t('common.required'),
                        onChange: () => setValue('categoryId', ''),
                      })}
                    >
                      <option value="">{t('common.select')}</option>
                      {lines.map((l) => (
                        <option key={l.id} value={l.id}>{l.code} — {l.name}</option>
                      ))}
                    </select>
                  </ZHField>
                )}

                {showCategorySelector && (
                  <ZHField label={t('catalog.subcategories.category')} required error={errors.categoryId?.message}>
                    <select
                      className="zh-input"
                      disabled={saving || !watchedLineId}
                      {...register('categoryId', { required: t('common.required') })}
                    >
                      <option value="">{t('common.select')}</option>
                      {modalCats.map((c) => (
                        <option key={c.id} value={c.id}>{c.code} — {c.name}</option>
                      ))}
                    </select>
                  </ZHField>
                )}
              </div>

              <div className="pg-actions-bar">
                <ZHBtn variant="ghost" size="md" type="button" onClick={closeModal} disabled={saving}>
                  {t('common.cancel')}
                </ZHBtn>
                <ZHBtn variant="primary" size="md" type="submit" disabled={saving}>
                  {saving ? t('common.saving') : t('common.saveChanges')}
                </ZHBtn>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
