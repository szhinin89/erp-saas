import { useCallback, useEffect, useMemo, useState } from 'react';
import { PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge } from '../components/PageShell';
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
import '../components/Modal.css';
import './CatalogStructurePage.css';

type Tab = 'line' | 'category' | 'subcategory';

function errMsg(err: unknown, fallback: string): string {
  const ax = err as { response?: { data?: { message?: string } } };
  return ax?.response?.data?.message ?? fallback;
}

export function CatalogStructurePage() {
  const { t } = useI18n();
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm = usePermissionsStore((s) => s.has);

  const canViewLines = isAdmin || hasPerm('catalog.productLines.view');
  const canViewCategories = isAdmin || hasPerm('catalog.categories.view');
  const canViewSubcategories = isAdmin || hasPerm('catalog.subcategories.view');
  const canView = canViewLines && canViewCategories && canViewSubcategories;

  const canCreateLines = isAdmin || hasPerm('catalog.productLines.create');
  const canCreateCategories = isAdmin || hasPerm('catalog.categories.create');
  const canCreateSubcategories = isAdmin || hasPerm('catalog.subcategories.create');
  const canUpdateLines = isAdmin || hasPerm('catalog.productLines.update');
  const canDeleteLines = isAdmin || hasPerm('catalog.productLines.delete');
  const canUpdateCategories = isAdmin || hasPerm('catalog.categories.update');
  const canDeleteCategories = isAdmin || hasPerm('catalog.categories.delete');
  const canUpdateSubcategories = isAdmin || hasPerm('catalog.subcategories.update');
  const canDeleteSubcategories = isAdmin || hasPerm('catalog.subcategories.delete');

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

  const [lineForm, setLineForm] = useState({ code: '', name: '' });
  const [catForm, setCatForm] = useState({ code: '', name: '', lineId: '' });
  const [subForm, setSubForm] = useState({ code: '', name: '', lineId: '', categoryId: '' });

  const [linesPick, setLinesPick] = useState<CatalogItem[]>([]);
  const [catsForSubForm, setCatsForSubForm] = useState<ProductCategoryListItem[]>([]);
  const [subFilterCategories, setSubFilterCategories] = useState<ProductCategoryListItem[]>([]);

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
    }, 0);
    return () => window.clearTimeout(id);
  }, [canView, loadLinesPick]);

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
      setError(errMsg(err, t('common.errorGeneric')));
    } finally {
      setLinesLoading(false);
    }
  }, [lineStatus, lineSearch, t]);

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
      setError(errMsg(err, t('common.errorGeneric')));
    } finally {
      setCatsLoading(false);
    }
  }, [catStatus, catSearch, catFilterLineId, t]);

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
      setError(errMsg(err, t('common.errorGeneric')));
    } finally {
      setSubsLoading(false);
    }
  }, [subStatus, subSearch, subFilterLineId, subFilterCategoryId, t]);

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
    if (!subForm.lineId) {
      const tid = window.setTimeout(() => {
        if (!cancelled) setCatsForSubForm([]);
      }, 0);
      return () => {
        cancelled = true;
        window.clearTimeout(tid);
      };
    }
    void catalogService
      .categories({ activeStatus: 'all', lineId: subForm.lineId })
      .then((c) => {
        if (!cancelled) setCatsForSubForm(c ?? []);
      })
      .catch(() => {
        if (!cancelled) setCatsForSubForm([]);
      });
    return () => {
      cancelled = true;
    };
  }, [subForm.lineId]);

  const onSubFilterLineChange = (id: string) => {
    setSubFilterLineId(id);
    setSubFilterCategoryId('');
  };

  const [editLine, setEditLine] = useState<CatalogItem | null>(null);
  const [editLineForm, setEditLineForm] = useState({ code: '', name: '' });
  const [editCat, setEditCat] = useState<ProductCategoryListItem | null>(null);
  const [editCatForm, setEditCatForm] = useState({ code: '', name: '', lineId: '' });
  const [editSub, setEditSub] = useState<ProductSubcategoryListItem | null>(null);
  const [editSubForm, setEditSubForm] = useState({ code: '', name: '', lineId: '', categoryId: '' });
  const [editSubCats, setEditSubCats] = useState<ProductCategoryListItem[]>([]);

  useEffect(() => {
    let cancelled = false;
    if (!editSub?.lineId) {
      const tid = window.setTimeout(() => {
        if (!cancelled) setEditSubCats([]);
      }, 0);
      return () => {
        cancelled = true;
        window.clearTimeout(tid);
      };
    }
    void catalogService
      .categories({ activeStatus: 'all', lineId: editSub.lineId })
      .then((c) => {
        if (!cancelled) setEditSubCats(c ?? []);
      })
      .catch(() => {
        if (!cancelled) setEditSubCats([]);
      });
    return () => {
      cancelled = true;
    };
  }, [editSub?.lineId]);

  const startEditLine = (row: CatalogItem) => {
    setEditLine(row);
    setEditLineForm({ code: row.code, name: row.name });
  };

  const startEditCat = (row: ProductCategoryListItem) => {
    setEditCat(row);
    setEditCatForm({ code: row.code, name: row.name, lineId: row.lineId });
  };

  const startEditSub = (row: ProductSubcategoryListItem) => {
    setEditSub(row);
    setEditSubForm({
      code: row.code,
      name: row.name,
      lineId: row.lineId,
      categoryId: row.categoryId,
    });
  };

  const saveLineEdit = async () => {
    if (!editLine) return;
    setSaving(true);
    setError('');
    try {
      await catalogService.updateProductLine(editLine.id, {
        code: editLineForm.code.trim(),
        name: editLineForm.name.trim(),
      });
      setEditLine(null);
      await loadLines();
      await loadLinesPick();
    } catch (err: unknown) {
      setError(errMsg(err, t('common.errorGeneric')));
    } finally {
      setSaving(false);
    }
  };

  const saveCatEdit = async () => {
    if (!editCat) return;
    setSaving(true);
    setError('');
    try {
      await catalogService.updateCategory(editCat.id, {
        code: editCatForm.code.trim(),
        name: editCatForm.name.trim(),
        lineId: editCatForm.lineId,
      });
      setEditCat(null);
      await loadCategories();
      await loadLinesPick();
    } catch (err: unknown) {
      setError(errMsg(err, t('common.errorGeneric')));
    } finally {
      setSaving(false);
    }
  };

  const saveSubEdit = async () => {
    if (!editSub) return;
    setSaving(true);
    setError('');
    try {
      await catalogService.updateSubcategory(editSub.id, {
        code: editSubForm.code.trim(),
        name: editSubForm.name.trim(),
        categoryId: editSubForm.categoryId,
      });
      setEditSub(null);
      await loadSubcategories();
    } catch (err: unknown) {
      setError(errMsg(err, t('common.errorGeneric')));
    } finally {
      setSaving(false);
    }
  };

  const createLine = async () => {
    setSaving(true);
    setError('');
    try {
      await catalogService.createProductLine({ code: lineForm.code.trim(), name: lineForm.name.trim() });
      setLineForm({ code: '', name: '' });
      await loadLines();
      await loadLinesPick();
    } catch (err: unknown) {
      setError(errMsg(err, t('common.errorGeneric')));
    } finally {
      setSaving(false);
    }
  };

  const createCat = async () => {
    if (!catForm.lineId) return;
    setSaving(true);
    setError('');
    try {
      await catalogService.createCategory({
        code: catForm.code.trim(),
        name: catForm.name.trim(),
        lineId: catForm.lineId,
      });
      setCatForm({ code: '', name: '', lineId: '' });
      await loadCategories();
    } catch (err: unknown) {
      setError(errMsg(err, t('common.errorGeneric')));
    } finally {
      setSaving(false);
    }
  };

  const createSub = async () => {
    if (!subForm.categoryId) return;
    setSaving(true);
    setError('');
    try {
      await catalogService.createSubcategory({
        code: subForm.code.trim(),
        name: subForm.name.trim(),
        categoryId: subForm.categoryId,
      });
      setSubForm({ code: '', name: '', lineId: '', categoryId: '' });
      await loadSubcategories();
    } catch (err: unknown) {
      setError(errMsg(err, t('common.errorGeneric')));
    } finally {
      setSaving(false);
    }
  };

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
    return (
      <div className="page-shell">
        <h1 className="page-title">{t('catalog.structure.title')}</h1>
        <p className="page-subtitle">{t('common.noAccess')}</p>
      </div>
    );
  }

  return (
    <PageShell title={t('catalog.structure.title')}>
      <TableCard>
        {error && <ErrorState message={error} />}

        <div className="catalog-structure-tabs" role="tablist">
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
            <div className="catalog-structure-filters">
              <label className="field">
                <span className="label">{t('catalog.structure.filterStatus')}</span>
                <select
                  className="input"
                  value={lineStatus}
                  onChange={(e) => setLineStatus(e.target.value as CatalogActiveStatus)}
                  disabled={linesLoading}
                >
                  {statusSelect}
                </select>
              </label>
              <label className="field" style={{ minWidth: 200, flex: 1 }}>
                <span className="label">{t('catalog.structure.search')}</span>
                <input
                  className="input"
                  value={lineSearch}
                  onChange={(e) => setLineSearch(e.target.value)}
                  disabled={linesLoading}
                  placeholder={t('catalog.structure.searchPlaceholder')}
                />
              </label>
            </div>

            {canCreateLines && (
              <div className="form-grid" style={{ marginBottom: 14 }}>
                <label className="field">
                  <span className="label">{t('common.code')}</span>
                  <input className="input" value={lineForm.code} onChange={(e) => setLineForm((s) => ({ ...s, code: e.target.value }))} disabled={saving} />
                </label>
                <label className="field">
                  <span className="label">{t('common.name')}</span>
                  <input className="input" value={lineForm.name} onChange={(e) => setLineForm((s) => ({ ...s, name: e.target.value }))} disabled={saving} />
                </label>
                <button
                  type="button"
                  className="btn btn--primary"
                  onClick={() => void createLine()}
                  disabled={saving || !lineForm.code.trim() || !lineForm.name.trim()}
                >
                  {saving ? t('common.saving') : t('common.create')}
                </button>
              </div>
            )}

            {editLine && (
              <div className="table-card" style={{ marginBottom: 14 }}>
                <div style={{ fontWeight: 700, marginBottom: 8 }}>{t('catalog.structure.editLine')}</div>
                <div className="form-grid">
                  <label className="field">
                    <span className="label">{t('common.code')}</span>
                    <input className="input" value={editLineForm.code} onChange={(e) => setEditLineForm((s) => ({ ...s, code: e.target.value }))} disabled={saving} />
                  </label>
                  <label className="field">
                    <span className="label">{t('common.name')}</span>
                    <input className="input" value={editLineForm.name} onChange={(e) => setEditLineForm((s) => ({ ...s, name: e.target.value }))} disabled={saving} />
                  </label>
                  <button type="button" className="btn btn--primary" onClick={() => void saveLineEdit()} disabled={saving}>
                    {t('catalog.structure.save')}
                  </button>
                  <button type="button" className="btn btn--ghost" onClick={() => setEditLine(null)} disabled={saving}>
                    {t('catalog.structure.cancel')}
                  </button>
                </div>
              </div>
            )}

            {linesLoading ? (
              <LoadingState />
            ) : lines.length === 0 ? (
              <EmptyState message={t('common.noData')} />
            ) : (
              <table className="table">
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
                      <td>{x.code}</td>
                      <td>{x.name}</td>
                      <td>
                        <Badge label={x.isActive ? t('common.active') : t('common.inactive')} variant={x.isActive ? 'green' : 'gray'} />
                      </td>
                      <td className="catalog-structure-actions">
                        {canUpdateLines && (
                          <button type="button" className="btn btn--primary btn-sm" onClick={() => startEditLine(x)} disabled={saving}>
                            {t('catalog.structure.edit')}
                          </button>
                        )}
                        {x.isActive && canDeleteLines && (
                          <button
                            type="button"
                            className="btn btn--primary btn-sm"
                            onClick={() => {
                              void (async () => {
                                setSaving(true);
                                setError('');
                                try {
                                  await catalogService.disableProductLine(x.id);
                                  await loadLines();
                                  await loadLinesPick();
                                } catch (err: unknown) {
                                  setError(errMsg(err, t('common.errorGeneric')));
                                } finally {
                                  setSaving(false);
                                }
                              })();
                            }}
                            disabled={saving}
                          >
                            {t('catalog.structure.disable')}
                          </button>
                        )}
                        {!x.isActive && canUpdateLines && (
                          <button
                            type="button"
                            className="btn btn--primary btn-sm"
                            onClick={() => {
                              void (async () => {
                                setSaving(true);
                                setError('');
                                try {
                                  await catalogService.enableProductLine(x.id);
                                  await loadLines();
                                  await loadLinesPick();
                                } catch (err: unknown) {
                                  setError(errMsg(err, t('common.errorGeneric')));
                                } finally {
                                  setSaving(false);
                                }
                              })();
                            }}
                            disabled={saving}
                          >
                            {t('catalog.structure.enable')}
                          </button>
                        )}
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
            <div className="catalog-structure-filters">
              <label className="field">
                <span className="label">{t('catalog.structure.filterStatus')}</span>
                <select
                  className="input"
                  value={catStatus}
                  onChange={(e) => setCatStatus(e.target.value as CatalogActiveStatus)}
                  disabled={catsLoading}
                >
                  {statusSelect}
                </select>
              </label>
              <label className="field">
                <span className="label">{t('catalog.categories.line')}</span>
                <select className="input" value={catFilterLineId} onChange={(e) => setCatFilterLineId(e.target.value)} disabled={catsLoading}>
                  <option value="">{t('catalog.structure.allLines')}</option>
                  {linesPick.map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.code} — {l.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field" style={{ minWidth: 200, flex: 1 }}>
                <span className="label">{t('catalog.structure.search')}</span>
                <input
                  className="input"
                  value={catSearch}
                  onChange={(e) => setCatSearch(e.target.value)}
                  disabled={catsLoading}
                  placeholder={t('catalog.structure.searchPlaceholder')}
                />
              </label>
            </div>

            {canCreateCategories && (
              <div className="form-grid" style={{ marginBottom: 14 }}>
                <label className="field">
                  <span className="label">{t('common.code')}</span>
                  <input className="input" value={catForm.code} onChange={(e) => setCatForm((s) => ({ ...s, code: e.target.value }))} disabled={saving} />
                </label>
                <label className="field">
                  <span className="label">{t('common.name')}</span>
                  <input className="input" value={catForm.name} onChange={(e) => setCatForm((s) => ({ ...s, name: e.target.value }))} disabled={saving} />
                </label>
                <label className="field">
                  <span className="label">{t('catalog.categories.line')}</span>
                  <select className="input" value={catForm.lineId} onChange={(e) => setCatForm((s) => ({ ...s, lineId: e.target.value }))} disabled={saving}>
                    <option value="">{t('common.select')}</option>
                    {linesPick.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.code} — {l.name}
                      </option>
                    ))}
                  </select>
                </label>
                <button
                  type="button"
                  className="btn btn--primary"
                  onClick={() => void createCat()}
                  disabled={saving || !catForm.code.trim() || !catForm.name.trim() || !catForm.lineId}
                >
                  {saving ? t('common.saving') : t('common.create')}
                </button>
              </div>
            )}

            {editCat && (
              <div className="table-card" style={{ marginBottom: 14 }}>
                <div style={{ fontWeight: 700, marginBottom: 8 }}>{t('catalog.structure.editCategory')}</div>
                <div className="form-grid">
                  <label className="field">
                    <span className="label">{t('common.code')}</span>
                    <input className="input" value={editCatForm.code} onChange={(e) => setEditCatForm((s) => ({ ...s, code: e.target.value }))} disabled={saving} />
                  </label>
                  <label className="field">
                    <span className="label">{t('common.name')}</span>
                    <input className="input" value={editCatForm.name} onChange={(e) => setEditCatForm((s) => ({ ...s, name: e.target.value }))} disabled={saving} />
                  </label>
                  <label className="field">
                    <span className="label">{t('catalog.categories.line')}</span>
                    <select className="input" value={editCatForm.lineId} onChange={(e) => setEditCatForm((s) => ({ ...s, lineId: e.target.value }))} disabled={saving}>
                      {linesPick.map((l) => (
                        <option key={l.id} value={l.id}>
                          {l.code} — {l.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  <button type="button" className="btn btn--primary" onClick={() => void saveCatEdit()} disabled={saving || !editCatForm.lineId}>
                    {t('catalog.structure.save')}
                  </button>
                  <button type="button" className="btn btn--ghost" onClick={() => setEditCat(null)} disabled={saving}>
                    {t('catalog.structure.cancel')}
                  </button>
                </div>
              </div>
            )}

            {catsLoading ? (
              <LoadingState />
            ) : categories.length === 0 ? (
              <EmptyState message={t('common.noData')} />
            ) : (
              <table className="table">
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
                      <td>{x.code}</td>
                      <td>{x.name}</td>
                      <td>
                        {x.lineCode} — {x.lineName}
                      </td>
                      <td>
                        <Badge label={x.isActive ? t('common.active') : t('common.inactive')} variant={x.isActive ? 'green' : 'gray'} />
                      </td>
                      <td className="catalog-structure-actions">
                        {canUpdateCategories && (
                          <button type="button" className="btn btn--primary btn-sm" onClick={() => startEditCat(x)} disabled={saving}>
                            {t('catalog.structure.edit')}
                          </button>
                        )}
                        {x.isActive && canDeleteCategories && (
                          <button
                            type="button"
                            className="btn btn--primary btn-sm"
                            onClick={() => {
                              void (async () => {
                                setSaving(true);
                                setError('');
                                try {
                                  await catalogService.disableCategory(x.id);
                                  await loadCategories();
                                } catch (err: unknown) {
                                  setError(errMsg(err, t('common.errorGeneric')));
                                } finally {
                                  setSaving(false);
                                }
                              })();
                            }}
                            disabled={saving}
                          >
                            {t('catalog.structure.disable')}
                          </button>
                        )}
                        {!x.isActive && canUpdateCategories && (
                          <button
                            type="button"
                            className="btn btn--primary btn-sm"
                            onClick={() => {
                              void (async () => {
                                setSaving(true);
                                setError('');
                                try {
                                  await catalogService.enableCategory(x.id);
                                  await loadCategories();
                                } catch (err: unknown) {
                                  setError(errMsg(err, t('common.errorGeneric')));
                                } finally {
                                  setSaving(false);
                                }
                              })();
                            }}
                            disabled={saving}
                          >
                            {t('catalog.structure.enable')}
                          </button>
                        )}
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
            <div className="catalog-structure-filters">
              <label className="field">
                <span className="label">{t('catalog.structure.filterStatus')}</span>
                <select
                  className="input"
                  value={subStatus}
                  onChange={(e) => setSubStatus(e.target.value as CatalogActiveStatus)}
                  disabled={subsLoading}
                >
                  {statusSelect}
                </select>
              </label>
              <label className="field">
                <span className="label">{t('catalog.categories.line')}</span>
                <select className="input" value={subFilterLineId} onChange={(e) => onSubFilterLineChange(e.target.value)} disabled={subsLoading}>
                  <option value="">{t('catalog.structure.allLines')}</option>
                  {linesPick.map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.code} — {l.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span className="label">{t('catalog.subcategories.category')}</span>
                <select
                  className="input"
                  value={subFilterCategoryId}
                  onChange={(e) => setSubFilterCategoryId(e.target.value)}
                  disabled={subsLoading || !subFilterLineId}
                >
                  <option value="">{t('catalog.structure.allCategories')}</option>
                  {subFilterCategories.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.code} — {c.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field" style={{ minWidth: 200, flex: 1 }}>
                <span className="label">{t('catalog.structure.search')}</span>
                <input
                  className="input"
                  value={subSearch}
                  onChange={(e) => setSubSearch(e.target.value)}
                  disabled={subsLoading}
                  placeholder={t('catalog.structure.searchPlaceholder')}
                />
              </label>
            </div>

            {canCreateSubcategories && (
              <div className="form-grid" style={{ marginBottom: 14 }}>
                <label className="field">
                  <span className="label">{t('common.code')}</span>
                  <input className="input" value={subForm.code} onChange={(e) => setSubForm((s) => ({ ...s, code: e.target.value }))} disabled={saving} />
                </label>
                <label className="field">
                  <span className="label">{t('common.name')}</span>
                  <input className="input" value={subForm.name} onChange={(e) => setSubForm((s) => ({ ...s, name: e.target.value }))} disabled={saving} />
                </label>
                <label className="field">
                  <span className="label">{t('catalog.categories.line')}</span>
                  <select
                    className="input"
                    value={subForm.lineId}
                    onChange={(e) => setSubForm((s) => ({ ...s, lineId: e.target.value, categoryId: '' }))}
                    disabled={saving}
                  >
                    <option value="">{t('common.select')}</option>
                    {linesPick.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.code} — {l.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="field">
                  <span className="label">{t('catalog.subcategories.category')}</span>
                  <select className="input" value={subForm.categoryId} onChange={(e) => setSubForm((s) => ({ ...s, categoryId: e.target.value }))} disabled={saving || !subForm.lineId}>
                    <option value="">{t('common.select')}</option>
                    {catsForSubForm.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.code} — {c.name}
                      </option>
                    ))}
                  </select>
                </label>
                <button
                  type="button"
                  className="btn btn--primary"
                  onClick={() => void createSub()}
                  disabled={saving || !subForm.code.trim() || !subForm.name.trim() || !subForm.lineId || !subForm.categoryId}
                >
                  {saving ? t('common.saving') : t('common.create')}
                </button>
              </div>
            )}

            {editSub && (
              <div className="table-card" style={{ marginBottom: 14 }}>
                <div style={{ fontWeight: 700, marginBottom: 8 }}>{t('catalog.structure.editSubcategory')}</div>
                <div className="form-grid">
                  <label className="field">
                    <span className="label">{t('common.code')}</span>
                    <input className="input" value={editSubForm.code} onChange={(e) => setEditSubForm((s) => ({ ...s, code: e.target.value }))} disabled={saving} />
                  </label>
                  <label className="field">
                    <span className="label">{t('common.name')}</span>
                    <input className="input" value={editSubForm.name} onChange={(e) => setEditSubForm((s) => ({ ...s, name: e.target.value }))} disabled={saving} />
                  </label>
                  <label className="field">
                    <span className="label">{t('catalog.categories.line')}</span>
                    <select
                      className="input"
                      value={editSubForm.lineId}
                      onChange={(e) => setEditSubForm((s) => ({ ...s, lineId: e.target.value, categoryId: '' }))}
                      disabled={saving}
                    >
                      {linesPick.map((l) => (
                        <option key={l.id} value={l.id}>
                          {l.code} — {l.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label className="field">
                    <span className="label">{t('catalog.subcategories.category')}</span>
                    <select className="input" value={editSubForm.categoryId} onChange={(e) => setEditSubForm((s) => ({ ...s, categoryId: e.target.value }))} disabled={saving || !editSubForm.lineId}>
                      {editSubCats.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.code} — {c.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  <button type="button" className="btn btn--primary" onClick={() => void saveSubEdit()} disabled={saving || !editSubForm.categoryId}>
                    {t('catalog.structure.save')}
                  </button>
                  <button type="button" className="btn btn--ghost" onClick={() => setEditSub(null)} disabled={saving}>
                    {t('catalog.structure.cancel')}
                  </button>
                </div>
              </div>
            )}

            {subsLoading ? (
              <LoadingState />
            ) : subcategories.length === 0 ? (
              <EmptyState message={t('common.noData')} />
            ) : (
              <table className="table">
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
                      <td>{x.code}</td>
                      <td>{x.name}</td>
                      <td>
                        {x.lineCode} — {x.lineName} → {x.categoryCode} — {x.categoryName}
                      </td>
                      <td>
                        <Badge label={x.isActive ? t('common.active') : t('common.inactive')} variant={x.isActive ? 'green' : 'gray'} />
                      </td>
                      <td className="catalog-structure-actions">
                        {canUpdateSubcategories && (
                          <button type="button" className="btn btn--primary btn-sm" onClick={() => startEditSub(x)} disabled={saving}>
                            {t('catalog.structure.edit')}
                          </button>
                        )}
                        {x.isActive && canDeleteSubcategories && (
                          <button
                            type="button"
                            className="btn btn--primary btn-sm"
                            onClick={() => {
                              void (async () => {
                                setSaving(true);
                                setError('');
                                try {
                                  await catalogService.disableSubcategory(x.id);
                                  await loadSubcategories();
                                } catch (err: unknown) {
                                  setError(errMsg(err, t('common.errorGeneric')));
                                } finally {
                                  setSaving(false);
                                }
                              })();
                            }}
                            disabled={saving}
                          >
                            {t('catalog.structure.disable')}
                          </button>
                        )}
                        {!x.isActive && canUpdateSubcategories && (
                          <button
                            type="button"
                            className="btn btn--primary btn-sm"
                            onClick={() => {
                              void (async () => {
                                setSaving(true);
                                setError('');
                                try {
                                  await catalogService.enableSubcategory(x.id);
                                  await loadSubcategories();
                                } catch (err: unknown) {
                                  setError(errMsg(err, t('common.errorGeneric')));
                                } finally {
                                  setSaving(false);
                                }
                              })();
                            }}
                            disabled={saving}
                          >
                            {t('catalog.structure.enable')}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}
      </TableCard>
    </PageShell>
  );
}
