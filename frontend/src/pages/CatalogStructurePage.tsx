import { useCallback, useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { PageShell, EmptyState, LoadingState, Badge, NoAccessPage } from '../components/PageShell';
import { Card } from '../components/ui/Card';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { useAuthStore } from '../store/authStore';
import {
  catalogService,
  type CatalogActiveStatus,
  type CatalogItem,
  type ProductCategoryListItem,
  type ProductSubcategoryListItem,
} from '../services/catalogService';
import { activityService, type UserActivityDto } from '../services/activityService';
import { ZHBtn, ZHField, ZHGrid } from '../components/zh/ZHForm';
import { ZHActionsRow, ZHGridRow, ZHInlineRow, ZHInlineRowRight, ZHSection } from '../components/zh/ZHLayout';
import { formatApiError } from '../modules/lib/formatApiError';
import {
  catalogLineCodeNameSchema,
  type CatalogLineCodeNameValues,
} from '../schemas/catalog/catalogStructureFormsSchema';
import {
  catalogCategoryFormSchema,
  catalogSubcategoryFormSchema,
  type CatalogCategoryFormValues,
  type CatalogSubcategoryFormValues,
} from '../schemas/catalog/catalogPagesFormsSchema';
import './CatalogStructurePage.css';

type Tab = 'line' | 'category' | 'subcategory';

function errMsg(err: unknown): string {
  return formatApiError(err);
}

function activityLabel(action: string): string {
  const [entityRaw, verbRaw] = action.split('.');
  const verb = (verbRaw ?? action).toLowerCase();
  const entity = (entityRaw ?? action).toLowerCase();
  const verbLabel =
    verb === 'create' ? 'Creó' :
    verb === 'update' ? 'Actualizó' :
    verb === 'enable' ? 'Habilitó' :
    verb === 'disable' ? 'Deshabilitó' :
    verb;

  const entityLabel =
    entity === 'unitofmeasure' ? 'Unidad de medida' :
    entity === 'brand' ? 'Marca' :
    entity === 'producttype' ? 'Tipo de producto' :
    entity === 'taxrate' ? 'Impuesto' :
    entity === 'tariff' ? 'Arancel' :
    entity === 'productline' ? 'Línea' :
    entity === 'productcategory' ? 'Categoría' :
    entity === 'productsubcategory' ? 'Subcategoría' :
    entityRaw ?? action;

  return `${verbLabel} ${entityLabel}`;
}

export function CatalogStructurePage() {
  const { t } = useI18n();
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm = usePermissionsStore((s) => s.has);

  const canViewLines = isAdmin || hasPerm('inventario.productLines.view');
  const canViewCategories = isAdmin || hasPerm('inventario.categories.view');
  const canViewSubcategories = isAdmin || hasPerm('inventario.subcategories.view');
  const canView = canViewLines && canViewCategories && canViewSubcategories;

  const canCreateLines = isAdmin || hasPerm('inventario.productLines.create');
  const canCreateCategories = isAdmin || hasPerm('inventario.categories.create');
  const canCreateSubcategories = isAdmin || hasPerm('inventario.subcategories.create');
  const canUpdateLines = isAdmin || hasPerm('inventario.productLines.update');
  const canDeleteLines = isAdmin || hasPerm('inventario.productLines.delete');
  const canUpdateCategories = isAdmin || hasPerm('inventario.categories.update');
  const canDeleteCategories = isAdmin || hasPerm('inventario.categories.delete');
  const canUpdateSubcategories = isAdmin || hasPerm('inventario.subcategories.update');
  const canDeleteSubcategories = isAdmin || hasPerm('inventario.subcategories.delete');

  const [tab, setTab] = useState<Tab>('line');
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  const [lineStatus, setLineStatus] = useState<CatalogActiveStatus>('active');
  const [lineSearch, setLineSearch] = useState('');
  const [lines, setLines] = useState<CatalogItem[]>([]);
  const [linesLoading, setLinesLoading] = useState(false);

  const [catStatus, setCatStatus] = useState<CatalogActiveStatus>('active');
  const [catSearch, setCatSearch] = useState('');
  const [catFilterLineId, setCatFilterLineId] = useState('');
  const [categories, setCategories] = useState<ProductCategoryListItem[]>([]);
  const [catsLoading, setCatsLoading] = useState(false);

  const [subStatus, setSubStatus] = useState<CatalogActiveStatus>('active');
  const [subSearch, setSubSearch] = useState('');
  const [subFilterLineId, setSubFilterLineId] = useState('');
  const [subFilterCategoryId, setSubFilterCategoryId] = useState('');
  const [subcategories, setSubcategories] = useState<ProductSubcategoryListItem[]>([]);
  const [subsLoading, setSubsLoading] = useState(false);

  const lineCreate = useForm<CatalogLineCodeNameValues>({
    resolver: zodResolver(catalogLineCodeNameSchema),
    defaultValues: { code: '', name: '' },
    mode: 'onChange',
  });
  const lineEdit = useForm<CatalogLineCodeNameValues>({
    resolver: zodResolver(catalogLineCodeNameSchema),
    defaultValues: { code: '', name: '' },
    mode: 'onChange',
  });
  const catCreate = useForm<CatalogCategoryFormValues>({
    resolver: zodResolver(catalogCategoryFormSchema),
    defaultValues: { code: '', name: '', lineId: '' },
    mode: 'onChange',
  });
  const catEdit = useForm<CatalogCategoryFormValues>({
    resolver: zodResolver(catalogCategoryFormSchema),
    defaultValues: { code: '', name: '', lineId: '' },
    mode: 'onChange',
  });
  const subCreate = useForm<CatalogSubcategoryFormValues>({
    resolver: zodResolver(catalogSubcategoryFormSchema),
    defaultValues: { code: '', name: '', lineId: '', categoryId: '' },
    mode: 'onChange',
  });
  const subEdit = useForm<CatalogSubcategoryFormValues>({
    resolver: zodResolver(catalogSubcategoryFormSchema),
    defaultValues: { code: '', name: '', lineId: '', categoryId: '' },
    mode: 'onChange',
  });

  const {
    register: regLineCreate,
    handleSubmit: handleLineCreate,
    reset: resetLineCreate,
    watch: watchLineCreate,
    formState: { errors: errLineCreate },
  } = lineCreate;
  const {
    register: regLineEdit,
    handleSubmit: handleLineEdit,
    reset: resetLineEdit,
    formState: { errors: errLineEdit },
  } = lineEdit;
  const {
    register: regCatCreate,
    handleSubmit: handleCatCreate,
    reset: resetCatCreate,
    watch: watchCatCreate,
    formState: { errors: errCatCreate },
  } = catCreate;
  const {
    register: regCatEdit,
    handleSubmit: handleCatEdit,
    reset: resetCatEdit,
    formState: { errors: errCatEdit },
  } = catEdit;
  const {
    register: regSubCreate,
    handleSubmit: handleSubCreate,
    reset: resetSubCreate,
    watch: watchSubCreate,
    formState: { errors: errSubCreate },
  } = subCreate;
  const {
    register: regSubEdit,
    handleSubmit: handleSubEdit,
    reset: resetSubEdit,
    watch: watchSubEdit,
    formState: { errors: errSubEdit },
  } = subEdit;

  const subCreateLineId = watchSubCreate('lineId');
  const subEditLineId = watchSubEdit('lineId');
  const lineCreateWatch = watchLineCreate();
  const catCreateWatch = watchCatCreate();
  const subCreateWatch = watchSubCreate();

  const [activity, setActivity] = useState<UserActivityDto[]>([]);
  const [activityLoading, setActivityLoading] = useState(false);

  const [linesPick, setLinesPick] = useState<CatalogItem[]>([]);
  const [catsForSubForm, setCatsForSubForm] = useState<ProductCategoryListItem[]>([]);
  const [subFilterCategories, setSubFilterCategories] = useState<ProductCategoryListItem[]>([]);

  const loadMyActivity = useCallback(async () => {
    setActivityLoading(true);
    try {
      const data = await activityService.my({ module: 'inventario', page: 1, pageSize: 12 });
      setActivity(data ?? []);
    } catch {
      setActivity([]);
    } finally {
      setActivityLoading(false);
    }
  }, []);

  const loadLinesPick = useCallback(async () => {
    try {
      const li = await catalogService.productLines({ activeStatus: 'all' });
      setLinesPick(li ?? []);
    } catch {
      setLinesPick([]);
    }
  }, []);

  useEffect(() => {
    if (!canView) return;
    const id = window.setTimeout(() => {
      void loadLinesPick();
      void loadMyActivity();
    }, 0);
    return () => window.clearTimeout(id);
  }, [canView, loadLinesPick, loadMyActivity]);

  const loadLines = useCallback(async () => {
    setError('');
    setLinesLoading(true);
    try {
      const data = await catalogService.productLines({
        activeStatus: lineStatus,
        search: lineSearch || undefined,
      });
      setLines(data ?? []);
    } catch (err: unknown) {
      setError(errMsg(err));
    } finally {
      setLinesLoading(false);
    }
  }, [lineStatus, lineSearch]);

  const loadCategories = useCallback(async () => {
    setError('');
    setCatsLoading(true);
    try {
      const data = await catalogService.categories({
        activeStatus: catStatus,
        search: catSearch || undefined,
        lineId: catFilterLineId || undefined,
      });
      setCategories(data ?? []);
    } catch (err: unknown) {
      setError(errMsg(err));
    } finally {
      setCatsLoading(false);
    }
  }, [catStatus, catSearch, catFilterLineId]);

  const loadSubcategories = useCallback(async () => {
    setError('');
    setSubsLoading(true);
    try {
      const data = await catalogService.subcategories({
        activeStatus: subStatus,
        search: subSearch || undefined,
        lineId: subFilterLineId || undefined,
        categoryId: subFilterCategoryId || undefined,
      });
      setSubcategories(data ?? []);
    } catch (err: unknown) {
      setError(errMsg(err));
    } finally {
      setSubsLoading(false);
    }
  }, [subStatus, subSearch, subFilterLineId, subFilterCategoryId]);

  useEffect(() => {
    if (!canView || tab !== 'line') return;
    const id = window.setTimeout(() => {
      void loadLines();
    }, 0);
    return () => window.clearTimeout(id);
  }, [canView, tab, loadLines]);

  useEffect(() => {
    if (!canView || tab !== 'category') return;
    const id = window.setTimeout(() => {
      void loadCategories();
    }, 0);
    return () => window.clearTimeout(id);
  }, [canView, tab, loadCategories]);

  useEffect(() => {
    if (!canView || tab !== 'subcategory') return;
    const id = window.setTimeout(() => {
      void loadSubcategories();
    }, 0);
    return () => window.clearTimeout(id);
  }, [canView, tab, loadSubcategories]);

  useEffect(() => {
    let cancelled = false;
    if (!subFilterLineId) {
      const tid = window.setTimeout(() => {
        if (!cancelled) setSubFilterCategories([]);
      }, 0);
      return () => {
        cancelled = true;
        window.clearTimeout(tid);
      };
    }
    void catalogService
      .categories({ activeStatus: 'all', lineId: subFilterLineId })
      .then((c) => {
        if (!cancelled) setSubFilterCategories(c ?? []);
      })
      .catch(() => {
        if (!cancelled) setSubFilterCategories([]);
      });
    return () => {
      cancelled = true;
    };
  }, [subFilterLineId]);

  useEffect(() => {
    let cancelled = false;
    if (!subCreateLineId) {
      const tid = window.setTimeout(() => {
        if (!cancelled) setCatsForSubForm([]);
      }, 0);
      return () => {
        cancelled = true;
        window.clearTimeout(tid);
      };
    }
    void catalogService
      .categories({ activeStatus: 'all', lineId: subCreateLineId })
      .then((c) => {
        if (!cancelled) setCatsForSubForm(c ?? []);
      })
      .catch(() => {
        if (!cancelled) setCatsForSubForm([]);
      });
    return () => {
      cancelled = true;
    };
  }, [subCreateLineId]);

  const onSubFilterLineChange = (id: string) => {
    setSubFilterLineId(id);
    setSubFilterCategoryId('');
  };

  const [editLine, setEditLine] = useState<CatalogItem | null>(null);
  const [editCat, setEditCat] = useState<ProductCategoryListItem | null>(null);
  const [editSub, setEditSub] = useState<ProductSubcategoryListItem | null>(null);
  const [editSubCats, setEditSubCats] = useState<ProductCategoryListItem[]>([]);

  useEffect(() => {
    let cancelled = false;
    if (!editSub || !subEditLineId) {
      const tid = window.setTimeout(() => {
        if (!cancelled) setEditSubCats([]);
      }, 0);
      return () => {
        cancelled = true;
        window.clearTimeout(tid);
      };
    }
    void catalogService
      .categories({ activeStatus: 'all', lineId: subEditLineId })
      .then((c) => {
        if (!cancelled) setEditSubCats(c ?? []);
      })
      .catch(() => {
        if (!cancelled) setEditSubCats([]);
      });
    return () => {
      cancelled = true;
    };
  }, [editSub, subEditLineId]);

  const startEditLine = (row: CatalogItem) => {
    setEditLine(row);
    resetLineEdit({ code: row.code, name: row.name });
  };

  const startEditCat = (row: ProductCategoryListItem) => {
    setEditCat(row);
    resetCatEdit({ code: row.code, name: row.name, lineId: row.lineId });
  };

  const startEditSub = (row: ProductSubcategoryListItem) => {
    setEditSub(row);
    resetSubEdit({
      code: row.code,
      name: row.name,
      lineId: row.lineId,
      categoryId: row.categoryId,
    });
  };

  const submitLineEdit = handleLineEdit(async (v) => {
    if (!editLine) return;
    setSaving(true);
    setError('');
    try {
      await catalogService.updateProductLine(editLine.id, {
        code: v.code.trim(),
        name: v.name.trim(),
      });
      setEditLine(null);
      resetLineEdit({ code: '', name: '' });
      await loadLines();
      await loadLinesPick();
    } catch (err: unknown) {
      setError(errMsg(err));
    } finally {
      setSaving(false);
    }
  });

  const submitCatEdit = handleCatEdit(async (v) => {
    if (!editCat) return;
    setSaving(true);
    setError('');
    try {
      await catalogService.updateCategory(editCat.id, {
        code: v.code.trim(),
        name: v.name.trim(),
        lineId: v.lineId,
      });
      setEditCat(null);
      resetCatEdit({ code: '', name: '', lineId: '' });
      await loadCategories();
      await loadLinesPick();
    } catch (err: unknown) {
      setError(errMsg(err));
    } finally {
      setSaving(false);
    }
  });

  const submitSubEdit = handleSubEdit(async (v) => {
    if (!editSub) return;
    setSaving(true);
    setError('');
    try {
      await catalogService.updateSubcategory(editSub.id, {
        code: v.code.trim(),
        name: v.name.trim(),
        categoryId: v.categoryId,
      });
      setEditSub(null);
      resetSubEdit({ code: '', name: '', lineId: '', categoryId: '' });
      await loadSubcategories();
    } catch (err: unknown) {
      setError(errMsg(err));
    } finally {
      setSaving(false);
    }
  });

  const submitLineCreate = handleLineCreate(async (v) => {
    setSaving(true);
    setError('');
    try {
      await catalogService.createProductLine({ code: v.code.trim(), name: v.name.trim() });
      resetLineCreate({ code: '', name: '' });
      await loadLines();
      await loadLinesPick();
    } catch (err: unknown) {
      setError(errMsg(err));
    } finally {
      setSaving(false);
    }
  });

  const submitCatCreate = handleCatCreate(async (v) => {
    setSaving(true);
    setError('');
    try {
      await catalogService.createCategory({
        code: v.code.trim(),
        name: v.name.trim(),
        lineId: v.lineId,
      });
      resetCatCreate({ code: '', name: '', lineId: '' });
      await loadCategories();
    } catch (err: unknown) {
      setError(errMsg(err));
    } finally {
      setSaving(false);
    }
  });

  const submitSubCreate = handleSubCreate(async (v) => {
    setSaving(true);
    setError('');
    try {
      await catalogService.createSubcategory({
        code: v.code.trim(),
        name: v.name.trim(),
        categoryId: v.categoryId,
      });
      resetSubCreate({ code: '', name: '', lineId: '', categoryId: '' });
      await loadSubcategories();
    } catch (err: unknown) {
      setError(errMsg(err));
    } finally {
      setSaving(false);
    }
  });

  const statusSelect = useMemo(
    () => (
      <>
        <option value="active">{t('common.active')}</option>
        <option value="inactive">{t('common.inactive')}</option>
        <option value="all">{t('catalog.structure.statusAll')}</option>
      </>
    ),
    [t]
  );

  if (!canView) {
    return <NoAccessPage title={t('catalog.structure.title')} />;
  }

  return (
    <PageShell kicker={t('app.nav.group.inventario')} title={t('catalog.structure.title')}>
      <Card>
        {error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />}

        <ZHInlineRow className="zh-mb-10">
          <div className="zh-card-section-title">{t('catalog.structure.activityHistoryTitle')}</div>
          <ZHInlineRowRight>
            <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void loadMyActivity()} disabled={activityLoading}>
              {activityLoading ? t('common.loading') : t('common.refresh')}
            </ZHBtn>
          </ZHInlineRowRight>
        </ZHInlineRow>
        {activityLoading ? (
          <LoadingState />
        ) : activity.length === 0 ? (
          <div className="empty-state zh-mb-12">{t('common.noData')}</div>
        ) : (
          <ul className="catalog-structure-activityList">
            {activity.map((a) => (
              <li key={a.id} className="zh-mb-6">
                {new Date(a.createdAt).toLocaleString()} · {activityLabel(a.action)}
                {a.description ? ` — ${a.description}` : ''}
              </li>
            ))}
          </ul>
        )}

        <div className="zh-form-tabs" role="tablist">
          <button type="button" className={tab === 'line' ? 'is-active' : ''} onClick={() => setTab('line')}>
            {t('catalog.structure.tabLine')}
          </button>
          <button type="button" className={tab === 'category' ? 'is-active' : ''} onClick={() => setTab('category')}>
            {t('catalog.structure.tabCategory')}
          </button>
          <button type="button" className={tab === 'subcategory' ? 'is-active' : ''} onClick={() => setTab('subcategory')}>
            {t('catalog.structure.tabSubcategory')}
          </button>
        </div>

        {tab === 'line' && (
          <>
            <ZHGridRow cols={2} className="zh-mb-14">
                <ZHField label={t('catalog.structure.filterStatus')}>
                  <select value={lineStatus} onChange={(e) => setLineStatus(e.target.value as CatalogActiveStatus)} disabled={linesLoading}>
                    {statusSelect}
                  </select>
                </ZHField>
                <ZHField label={t('catalog.structure.search')}>
                  <input value={lineSearch} onChange={(e) => setLineSearch(e.target.value)} disabled={linesLoading} placeholder={t('catalog.structure.searchPlaceholder')} />
                </ZHField>
            </ZHGridRow>

            {canCreateLines && (
              <ZHSection bottom={14}>
                <ZHGrid cols={3}>
                  <ZHField label={t('common.code')} fieldError={errLineCreate.code?.message}>
                    <input {...regLineCreate('code')} disabled={saving} />
                  </ZHField>
                  <ZHField label={t('common.name')} fieldError={errLineCreate.name?.message}>
                    <input {...regLineCreate('name')} disabled={saving} />
                  </ZHField>
                  <ZHActionsRow>
                    <ZHBtn
                      variant="primary"
                      size="md"
                      type="button"
                      onClick={() => void submitLineCreate()}
                      disabled={saving || !lineCreateWatch.code?.trim() || !lineCreateWatch.name?.trim()}
                    >
                      {saving ? t('common.saving') : t('catalog.structure.primaryCreateLine')}
                    </ZHBtn>
                  </ZHActionsRow>
                </ZHGrid>
              </ZHSection>
            )}

            {editLine && (
              <div className="table-card zh-mb-14">
                <div className="zh-card-section-title zh-mb-8">{t('catalog.structure.editLine')}</div>
                <ZHGrid cols={3}>
                  <ZHField label={t('common.code')} fieldError={errLineEdit.code?.message}>
                    <input {...regLineEdit('code')} disabled={saving} />
                  </ZHField>
                  <ZHField label={t('common.name')} fieldError={errLineEdit.name?.message}>
                    <input {...regLineEdit('name')} disabled={saving} />
                  </ZHField>
                  <ZHActionsRow>
                    <ZHBtn
                      variant="ghost"
                      size="md"
                      type="button"
                      onClick={() => {
                        setEditLine(null);
                        resetLineEdit({ code: '', name: '' });
                      }}
                      disabled={saving}
                    >
                      {t('catalog.structure.cancel')}
                    </ZHBtn>
                    <ZHBtn variant="primary" size="md" type="button" onClick={() => void submitLineEdit()} disabled={saving}>
                      {t('common.saveChanges')}
                    </ZHBtn>
                  </ZHActionsRow>
                </ZHGrid>
              </div>
            )}

            {linesLoading ? (
              <LoadingState />
            ) : lines.length === 0 ? (
              <EmptyState message={t('common.noData')} />
            ) : (
              <table className="table catalog-structure-responsive-table">
                <thead>
                  <tr>
                    <th>{t('common.code')}</th>
                    <th>{t('common.name')}</th>
                    <th>{t('common.status')}</th>
                    <th>{t('catalog.structure.actions')}</th>
                  </tr>
                </thead>
                <tbody>
                  {lines.map((x) => (
                    <tr key={x.id}>
                      <td data-label={t('common.code')}>{x.code}</td>
                      <td data-label={t('common.name')}>{x.name}</td>
                      <td data-label={t('common.status')}>
                        <Badge label={x.isActive ? t('common.active') : t('common.inactive')} variant={x.isActive ? 'green' : 'gray'} />
                      </td>
                      <td data-label={t('catalog.structure.actions')}>
                        <div className="catalog-structure-actions">
                        {canUpdateLines && (
                          <ZHBtn variant="secondary" size="xs" type="button" onClick={() => startEditLine(x)} disabled={saving}>
                            {t('catalog.structure.edit')}
                          </ZHBtn>
                        )}
                        {x.isActive && canDeleteLines && (
                          <ZHBtn
                            variant="ghost"
                            size="xs"
                            type="button"
                            onClick={() => {
                              void (async () => {
                                setSaving(true);
                                setError('');
                                try {
                                  await catalogService.disableProductLine(x.id);
                                  await loadLines();
                                  await loadLinesPick();
                                } catch (err: unknown) {
                                  setError(errMsg(err));
                                } finally {
                                  setSaving(false);
                                }
                              })();
                            }}
                            disabled={saving}
                          >
                            {t('catalog.structure.disable')}
                          </ZHBtn>
                        )}
                        {!x.isActive && canUpdateLines && (
                          <ZHBtn
                            variant="secondary"
                            size="xs"
                            type="button"
                            onClick={() => {
                              void (async () => {
                                setSaving(true);
                                setError('');
                                try {
                                  await catalogService.enableProductLine(x.id);
                                  await loadLines();
                                  await loadLinesPick();
                                } catch (err: unknown) {
                                  setError(errMsg(err));
                                } finally {
                                  setSaving(false);
                                }
                              })();
                            }}
                            disabled={saving}
                          >
                            {t('catalog.structure.enable')}
                          </ZHBtn>
                        )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}

        {tab === 'category' && (
          <>
            <ZHGridRow cols={3} className="zh-mb-14">
                <ZHField label={t('catalog.structure.filterStatus')}>
                  <select value={catStatus} onChange={(e) => setCatStatus(e.target.value as CatalogActiveStatus)} disabled={catsLoading}>
                    {statusSelect}
                  </select>
                </ZHField>
                <ZHField label={t('catalog.categories.line')}>
                  <select value={catFilterLineId} onChange={(e) => setCatFilterLineId(e.target.value)} disabled={catsLoading}>
                    <option value="">{t('catalog.structure.allLines')}</option>
                    {linesPick.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.code} — {l.name}
                      </option>
                    ))}
                  </select>
                </ZHField>
                <ZHField label={t('catalog.structure.search')}>
                  <input value={catSearch} onChange={(e) => setCatSearch(e.target.value)} disabled={catsLoading} placeholder={t('catalog.structure.searchPlaceholder')} />
                </ZHField>
            </ZHGridRow>

            {canCreateCategories && (
              <ZHSection bottom={14}>
                <ZHGrid cols={3}>
                  <ZHField label={t('common.code')} fieldError={errCatCreate.code?.message}>
                    <input {...regCatCreate('code')} disabled={saving} />
                  </ZHField>
                  <ZHField label={t('common.name')} fieldError={errCatCreate.name?.message}>
                    <input {...regCatCreate('name')} disabled={saving} />
                  </ZHField>
                  <ZHField label={t('catalog.categories.line')} fieldError={errCatCreate.lineId?.message}>
                    <select {...regCatCreate('lineId')} disabled={saving}>
                      <option value="">{t('common.select')}</option>
                      {linesPick.map((l) => (
                        <option key={l.id} value={l.id}>
                          {l.code} — {l.name}
                        </option>
                      ))}
                    </select>
                  </ZHField>
                  <ZHActionsRow>
                    <ZHBtn
                      variant="primary"
                      size="md"
                      type="button"
                      onClick={() => void submitCatCreate()}
                      disabled={
                        saving ||
                        !catCreateWatch.code?.trim() ||
                        !catCreateWatch.name?.trim() ||
                        !catCreateWatch.lineId
                      }
                    >
                      {saving ? t('common.saving') : t('catalog.structure.primaryCreateCategory')}
                    </ZHBtn>
                  </ZHActionsRow>
                </ZHGrid>
              </ZHSection>
            )}

            {editCat && (
              <div className="table-card zh-mb-14">
                <div className="zh-card-section-title zh-mb-8">{t('catalog.structure.editCategory')}</div>
                <ZHGrid cols={3}>
                  <ZHField label={t('common.code')} fieldError={errCatEdit.code?.message}>
                    <input {...regCatEdit('code')} disabled={saving} />
                  </ZHField>
                  <ZHField label={t('common.name')} fieldError={errCatEdit.name?.message}>
                    <input {...regCatEdit('name')} disabled={saving} />
                  </ZHField>
                  <ZHField label={t('catalog.categories.line')} fieldError={errCatEdit.lineId?.message}>
                    <select {...regCatEdit('lineId')} disabled={saving}>
                      {linesPick.map((l) => (
                        <option key={l.id} value={l.id}>
                          {l.code} — {l.name}
                        </option>
                      ))}
                    </select>
                  </ZHField>
                  <ZHActionsRow>
                    <ZHBtn
                      variant="ghost"
                      size="md"
                      type="button"
                      onClick={() => {
                        setEditCat(null);
                        resetCatEdit({ code: '', name: '', lineId: '' });
                      }}
                      disabled={saving}
                    >
                      {t('catalog.structure.cancel')}
                    </ZHBtn>
                    <ZHBtn variant="primary" size="md" type="button" onClick={() => void submitCatEdit()} disabled={saving}>
                      {t('common.saveChanges')}
                    </ZHBtn>
                  </ZHActionsRow>
                </ZHGrid>
              </div>
            )}

            {catsLoading ? (
              <LoadingState />
            ) : categories.length === 0 ? (
              <EmptyState message={t('common.noData')} />
            ) : (
              <table className="table catalog-structure-responsive-table">
                <thead>
                  <tr>
                    <th>{t('common.code')}</th>
                    <th>{t('common.name')}</th>
                    <th>{t('catalog.structure.lineColumn')}</th>
                    <th>{t('common.status')}</th>
                    <th>{t('catalog.structure.actions')}</th>
                  </tr>
                </thead>
                <tbody>
                  {categories.map((x) => (
                    <tr key={x.id}>
                      <td data-label={t('common.code')}>{x.code}</td>
                      <td data-label={t('common.name')}>{x.name}</td>
                      <td data-label={t('catalog.structure.lineColumn')}>
                        {x.lineCode} — {x.lineName}
                      </td>
                      <td data-label={t('common.status')}>
                        <Badge label={x.isActive ? t('common.active') : t('common.inactive')} variant={x.isActive ? 'green' : 'gray'} />
                      </td>
                      <td data-label={t('catalog.structure.actions')}>
                        <div className="catalog-structure-actions">
                        {canUpdateCategories && (
                          <ZHBtn variant="secondary" size="xs" type="button" onClick={() => startEditCat(x)} disabled={saving}>
                            {t('catalog.structure.edit')}
                          </ZHBtn>
                        )}
                        {x.isActive && canDeleteCategories && (
                          <ZHBtn
                            variant="ghost"
                            size="xs"
                            type="button"
                            onClick={() => {
                              void (async () => {
                                setSaving(true);
                                setError('');
                                try {
                                  await catalogService.disableCategory(x.id);
                                  await loadCategories();
                                } catch (err: unknown) {
                                  setError(errMsg(err));
                                } finally {
                                  setSaving(false);
                                }
                              })();
                            }}
                            disabled={saving}
                          >
                            {t('catalog.structure.disable')}
                          </ZHBtn>
                        )}
                        {!x.isActive && canUpdateCategories && (
                          <ZHBtn
                            variant="secondary"
                            size="xs"
                            type="button"
                            onClick={() => {
                              void (async () => {
                                setSaving(true);
                                setError('');
                                try {
                                  await catalogService.enableCategory(x.id);
                                  await loadCategories();
                                } catch (err: unknown) {
                                  setError(errMsg(err));
                                } finally {
                                  setSaving(false);
                                }
                              })();
                            }}
                            disabled={saving}
                          >
                            {t('catalog.structure.enable')}
                          </ZHBtn>
                        )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}

        {tab === 'subcategory' && (
          <>
            <ZHGridRow cols={3} className="zh-mb-14">
                <ZHField label={t('catalog.structure.filterStatus')}>
                  <select value={subStatus} onChange={(e) => setSubStatus(e.target.value as CatalogActiveStatus)} disabled={subsLoading}>
                    {statusSelect}
                  </select>
                </ZHField>
                <ZHField label={t('catalog.categories.line')}>
                  <select value={subFilterLineId} onChange={(e) => onSubFilterLineChange(e.target.value)} disabled={subsLoading}>
                    <option value="">{t('catalog.structure.allLines')}</option>
                    {linesPick.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.code} — {l.name}
                      </option>
                    ))}
                  </select>
                </ZHField>
                <ZHField label={t('catalog.subcategories.category')}>
                  <select value={subFilterCategoryId} onChange={(e) => setSubFilterCategoryId(e.target.value)} disabled={subsLoading || !subFilterLineId}>
                    <option value="">{t('catalog.structure.allCategories')}</option>
                    {subFilterCategories.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.code} — {c.name}
                      </option>
                    ))}
                  </select>
                </ZHField>
            </ZHGridRow>
            <ZHGridRow cols={1} className="zh-mb-14">
              <ZHField label={t('catalog.structure.search')}>
                <input value={subSearch} onChange={(e) => setSubSearch(e.target.value)} disabled={subsLoading} placeholder={t('catalog.structure.searchPlaceholder')} />
              </ZHField>
            </ZHGridRow>

            {canCreateSubcategories && (
              <ZHSection bottom={14}>
                <ZHGrid cols={3}>
                  <ZHField label={t('common.code')} fieldError={errSubCreate.code?.message}>
                    <input {...regSubCreate('code')} disabled={saving} />
                  </ZHField>
                  <ZHField label={t('common.name')} fieldError={errSubCreate.name?.message}>
                    <input {...regSubCreate('name')} disabled={saving} />
                  </ZHField>
                  <ZHField label={t('catalog.categories.line')} fieldError={errSubCreate.lineId?.message}>
                    <select
                      {...regSubCreate('lineId', {
                        onChange: () => {
                          subCreate.setValue('categoryId', '', { shouldValidate: true });
                        },
                      })}
                      disabled={saving}
                    >
                      <option value="">{t('common.select')}</option>
                      {linesPick.map((l) => (
                        <option key={l.id} value={l.id}>
                          {l.code} — {l.name}
                        </option>
                      ))}
                    </select>
                  </ZHField>
                  <ZHField label={t('catalog.subcategories.category')} fieldError={errSubCreate.categoryId?.message}>
                    <select {...regSubCreate('categoryId')} disabled={saving || !subCreateLineId}>
                      <option value="">{t('common.select')}</option>
                      {catsForSubForm.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.code} — {c.name}
                        </option>
                      ))}
                    </select>
                  </ZHField>
                  <ZHActionsRow>
                    <ZHBtn
                      variant="primary"
                      size="md"
                      type="button"
                      onClick={() => void submitSubCreate()}
                      disabled={
                        saving ||
                        !subCreateWatch.code?.trim() ||
                        !subCreateWatch.name?.trim() ||
                        !subCreateWatch.lineId ||
                        !subCreateWatch.categoryId
                      }
                    >
                      {saving ? t('common.saving') : t('catalog.structure.primaryCreateSubcategory')}
                    </ZHBtn>
                  </ZHActionsRow>
                </ZHGrid>
              </ZHSection>
            )}

            {editSub && (
              <div className="table-card zh-mb-14">
                <div className="zh-card-section-title zh-mb-8">{t('catalog.structure.editSubcategory')}</div>
                <ZHGrid cols={3}>
                  <ZHField label={t('common.code')} fieldError={errSubEdit.code?.message}>
                    <input {...regSubEdit('code')} disabled={saving} />
                  </ZHField>
                  <ZHField label={t('common.name')} fieldError={errSubEdit.name?.message}>
                    <input {...regSubEdit('name')} disabled={saving} />
                  </ZHField>
                  <ZHField label={t('catalog.categories.line')} fieldError={errSubEdit.lineId?.message}>
                    <select
                      {...regSubEdit('lineId', {
                        onChange: () => {
                          subEdit.setValue('categoryId', '', { shouldValidate: true });
                        },
                      })}
                      disabled={saving}
                    >
                      {linesPick.map((l) => (
                        <option key={l.id} value={l.id}>
                          {l.code} — {l.name}
                        </option>
                      ))}
                    </select>
                  </ZHField>
                  <ZHField label={t('catalog.subcategories.category')} fieldError={errSubEdit.categoryId?.message}>
                    <select {...regSubEdit('categoryId')} disabled={saving || !subEditLineId}>
                      {editSubCats.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.code} — {c.name}
                        </option>
                      ))}
                    </select>
                  </ZHField>
                  <ZHActionsRow>
                    <ZHBtn
                      variant="ghost"
                      size="md"
                      type="button"
                      onClick={() => {
                        setEditSub(null);
                        resetSubEdit({ code: '', name: '', lineId: '', categoryId: '' });
                      }}
                      disabled={saving}
                    >
                      {t('catalog.structure.cancel')}
                    </ZHBtn>
                    <ZHBtn variant="primary" size="md" type="button" onClick={() => void submitSubEdit()} disabled={saving}>
                      {t('common.saveChanges')}
                    </ZHBtn>
                  </ZHActionsRow>
                </ZHGrid>
              </div>
            )}

            {subsLoading ? (
              <LoadingState />
            ) : subcategories.length === 0 ? (
              <EmptyState message={t('common.noData')} />
            ) : (
              <table className="table catalog-structure-responsive-table">
                <thead>
                  <tr>
                    <th>{t('common.code')}</th>
                    <th>{t('common.name')}</th>
                    <th>{t('catalog.structure.hierarchyColumn')}</th>
                    <th>{t('common.status')}</th>
                    <th>{t('catalog.structure.actions')}</th>
                  </tr>
                </thead>
                <tbody>
                  {subcategories.map((x) => (
                    <tr key={x.id}>
                      <td data-label={t('common.code')}>{x.code}</td>
                      <td data-label={t('common.name')}>{x.name}</td>
                      <td data-label={t('catalog.structure.hierarchyColumn')}>
                        {x.lineCode} — {x.lineName} → {x.categoryCode} — {x.categoryName}
                      </td>
                      <td data-label={t('common.status')}>
                        <Badge label={x.isActive ? t('common.active') : t('common.inactive')} variant={x.isActive ? 'green' : 'gray'} />
                      </td>
                      <td data-label={t('catalog.structure.actions')}>
                        <div className="catalog-structure-actions">
                        {canUpdateSubcategories && (
                          <ZHBtn variant="secondary" size="xs" type="button" onClick={() => startEditSub(x)} disabled={saving}>
                            {t('catalog.structure.edit')}
                          </ZHBtn>
                        )}
                        {x.isActive && canDeleteSubcategories && (
                          <ZHBtn
                            variant="ghost"
                            size="xs"
                            type="button"
                            onClick={() => {
                              void (async () => {
                                setSaving(true);
                                setError('');
                                try {
                                  await catalogService.disableSubcategory(x.id);
                                  await loadSubcategories();
                                } catch (err: unknown) {
                                  setError(errMsg(err));
                                } finally {
                                  setSaving(false);
                                }
                              })();
                            }}
                            disabled={saving}
                          >
                            {t('catalog.structure.disable')}
                          </ZHBtn>
                        )}
                        {!x.isActive && canUpdateSubcategories && (
                          <ZHBtn
                            variant="secondary"
                            size="xs"
                            type="button"
                            onClick={() => {
                              void (async () => {
                                setSaving(true);
                                setError('');
                                try {
                                  await catalogService.enableSubcategory(x.id);
                                  await loadSubcategories();
                                } catch (err: unknown) {
                                  setError(errMsg(err));
                                } finally {
                                  setSaving(false);
                                }
                              })();
                            }}
                            disabled={saving}
                          >
                            {t('catalog.structure.enable')}
                          </ZHBtn>
                        )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}
      </Card>
    </PageShell>
  );
}
