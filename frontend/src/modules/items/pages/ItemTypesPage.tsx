import { useCallback, useEffect, useState } from 'react';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHBtn, ZHField, ZHGrid } from '../../../components/zh/ZHForm';
import { ZhNumberInput } from '../../../components/zh/inputs/ZhNumberInput';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ConfigTabsLayout } from '../../../components/shared/ConfigTabsLayout';
import { message } from '../../../lib/messages';
import { itemTypeService, type ItemTypeDto } from '../api/itemTypeService';

import '../../../styles/shared/items-catalog.css';

export function ItemTypesPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView   = canShow('items.view');
  const canCreate = canShow('items.create');
  const canEdit   = canShow('items.edit');

  const [activeTab, setActiveTab] = useState<'list' | 'editor'>('list');
  const [items, setItems] = useState<ItemTypeDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');

  const [editing, setEditing] = useState<ItemTypeDto | null>(null);
  const [fCode, setFCode] = useState('');
  const [fName, setFName] = useState('');
  const [fSortOrder, setFSortOrder] = useState(0);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');

  const fetchItems = useCallback(async () => {
    setLoading(true);
    try {
      const all = await itemTypeService.list(false);
      setItems(all);
    } catch { /* */ }
    setLoading(false);
  }, []);

  useEffect(() => { void fetchItems(); }, [fetchItems]);

  const filtered = search
    ? items.filter(it =>
        it.code.toLowerCase().includes(search.toLowerCase()) ||
        it.name.toLowerCase().includes(search.toLowerCase()))
    : items;

  const resetForm = () => {
    setFCode('');
    setFName('');
    setFSortOrder(0);
    setEditing(null);
    setSaveError('');
  };

  const openCreate = () => {
    resetForm();
    setActiveTab('editor');
  };

  const openEdit = (it: ItemTypeDto) => {
    setEditing(it);
    setFCode(it.code);
    setFName(it.name);
    setFSortOrder(it.sortOrder);
    setSaveError('');
    setActiveTab('editor');
  };

  const handleCancel = () => {
    resetForm();
    setActiveTab('list');
  };

  const handleSave = async () => {
    setSaveError('');
    setSaving(true);
    try {
      if (editing) {
        await itemTypeService.update(editing.id, { id: editing.id, name: fName, sortOrder: fSortOrder });
        message.success(t('itemTypes.updated.success', 'Tipo de ítem actualizado correctamente.'));
      } else {
        await itemTypeService.create({ code: fCode, name: fName, sortOrder: fSortOrder });
        message.success(t('itemTypes.created.success', 'Tipo de ítem creado correctamente.'));
      }
      resetForm();
      setActiveTab('list');
      void fetchItems();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { data?: { errors?: Record<string, string[]> }; message?: { user?: string } } } };
      const fieldErrors = err.response?.data?.data?.errors;
      const msg = fieldErrors
        ? Object.values(fieldErrors).flat().join(' ')
        : err.response?.data?.message?.user ?? t('common.saveError', 'Error al guardar. Revisa los datos.');
      setSaveError(msg);
    }
    setSaving(false);
  };

  const handleToggle = async (it: ItemTypeDto) => {
    try {
      await itemTypeService.toggle(it.id);
      message.success(
        it.isActive
          ? t('itemTypes.toggle.disabled', 'Tipo de ítem desactivado.')
          : t('itemTypes.toggle.activated', 'Tipo de ítem activado.')
      );
      void fetchItems();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: { user?: string } } } };
      message.error(err.response?.data?.message?.user ?? t('itemTypes.toggle.error', 'No se pudo cambiar el estado.'));
    }
  };

  if (!canView) return <NoAccessPage title={t('itemTypes.title', 'Tipos de Ítem')} />;

  const editorLabel = editing ? t('itemTypes.editTitle', 'Editar Tipo de Ítem') : t('itemTypes.newTitle', 'Nuevo Tipo de Ítem');

  const listContent = (
    <>
      <div className="prd-filters-bar">
        <input
          className="zh-input prd-filters-bar__search"
          placeholder={t('itemTypes.search', 'Buscar por código o nombre...')}
          value={search}
          onChange={e => setSearch(e.target.value)}
        />
      </div>
      {loading ? (
        <p>{t('common.loading', 'Cargando...')}</p>
      ) : (
        <div className="prd-table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>{t('itemTypes.col.code', 'Código')}</th>
                <th>{t('itemTypes.col.name', 'Nombre')}</th>
                <th>{t('itemTypes.col.sortOrder', 'Orden')}</th>
                <th>{t('common.status', 'Estado')}</th>
                <th>{t('common.actions', 'Acciones')}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(it => (
                <tr key={it.id} className={it.isActive ? undefined : 'prd-row--inactive'}>
                  <td><code className="prd-sku">{it.code}</code></td>
                  <td>{it.name}</td>
                  <td>{it.sortOrder}</td>
                  <td>
                    <span className={it.isActive ? 'prd-status-badge prd-status-badge--active' : 'prd-status-badge prd-status-badge--inactive'}>
                      {it.isActive ? t('common.active', 'Activo') : t('common.inactive', 'Inactivo')}
                    </span>
                  </td>
                  <td>
                    <div className="prd-row-actions">
                      {canEdit && (
                        <ZHBtn type="button" variant="ghost" size="sm"
                          onClick={() => openEdit(it)} title={t('common.edit', 'Editar')}>
                          <span className="material-symbols-outlined zh-icon-lg">edit</span>
                        </ZHBtn>
                      )}
                      {canEdit && (
                        <ZHBtn type="button" variant="ghost" size="sm"
                          onClick={() => void handleToggle(it)}
                          title={it.isActive ? t('common.deactivate', 'Desactivar') : t('common.activate', 'Activar')}>
                          <span className="material-symbols-outlined zh-icon-lg">
                            {it.isActive ? 'toggle_on' : 'toggle_off'}
                          </span>
                        </ZHBtn>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
              {filtered.length === 0 && (
                <tr className="prd-empty-row">
                  <td colSpan={5}>{t('itemTypes.empty', 'Sin tipos de ítem registrados.')}</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </>
  );

  const editorContent = (
    <div className="pg-section-body">
      {saveError && <ZHPageNotice variant="error" message={saveError} />}
      <ZHGrid cols={2}>
        <ZHField label={t('itemTypes.form.code', 'Código')} required>
          <input
            className="zh-input--upper"
            value={fCode}
            onChange={e => setFCode(e.target.value.toUpperCase())}
            maxLength={30}
            placeholder={t('itemTypes.form.codePlaceholder', 'MATERIA_PRIMA')}
            disabled={saving || !!editing}
          />
        </ZHField>
        <ZHField label={t('itemTypes.form.name', 'Nombre')} required>
          <input
            value={fName}
            onChange={e => setFName(e.target.value)}
            maxLength={100}
            placeholder={t('itemTypes.form.namePlaceholder', 'Materia Prima')}
            disabled={saving}
          />
        </ZHField>
        <ZHField label={t('itemTypes.form.sortOrder', 'Orden de visualización')}>
          <ZhNumberInput positiveOnly value={fSortOrder} disabled={saving}
            onChange={e => setFSortOrder(Number(e.target.value) || 0)} />
        </ZHField>
      </ZHGrid>

      <div className="prd-crud-actions">
        <ZHBtn variant="primary" size="md" onClick={() => void handleSave()}
          disabled={saving || !fName.trim() || (!editing && !fCode.trim())}>
          {saving ? t('common.saving', 'Guardando...') : editing ? t('common.update', 'Actualizar') : t('common.create', 'Crear')}
        </ZHBtn>
        <ZHBtn variant="ghost" size="md" onClick={handleCancel}>
          {t('common.cancel', 'Cancelar')}
        </ZHBtn>
      </div>
    </div>
  );

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.inventario', 'Inventario')}
      title={t('itemTypes.title', 'Tipos de Ítem')}
      action={
        canCreate ? (
          <ZHBtn variant="primary" size="md" type="button" onClick={openCreate}>
            <span className="material-symbols-outlined">add</span>
            {t('itemTypes.new', 'Nuevo tipo')}
          </ZHBtn>
        ) : null
      }
    >
      <ConfigTabsLayout
        activeTab={activeTab}
        onTabChange={setActiveTab}
        editorLabel={editorLabel}
        editorIcon={editing ? 'edit' : 'add_box'}
        listContent={listContent}
        editorContent={editorContent}
      />
    </ErpPageTemplate>
  );
}
