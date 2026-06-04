import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, type Resolver } from 'react-hook-form';
import { z } from 'zod';
import { useAsync } from '../../../hooks/useAsync';
import { useI18n } from '../../../i18n/i18n';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid, ZHPageNotice } from '../../../components/zh/ZHForm';
import { catalogItemService } from '../api/catalogItemService';
import { formatApiError } from '../../lib/formatApiError';
import type { AttributeGroupDto, AttributeDefinitionDto } from '../../../types/items';

const groupSchema = z.object({
  code: z.string().trim().min(1, 'Código obligatorio').max(50),
  name: z.string().trim().min(1, 'Nombre obligatorio').max(100),
  sortOrder: z.number().int().min(0).default(0),
});
type GroupForm = z.output<typeof groupSchema>;

const defSchema = z.object({
  groupId:      z.string().uuid('Grupo obligatorio'),
  code:         z.string().trim().min(1).max(50),
  name:         z.string().trim().min(1).max(100),
  dataType:     z.enum(['Text', 'Number', 'Boolean', 'Date', 'List', 'Color']),
  isVariantAxis: z.boolean().default(false),
  allowedValues: z.string().nullable().optional(),
  isRequired:   z.boolean().default(false),
});
type DefForm = z.output<typeof defSchema>;

export function AttributesPage() {
  const { t } = useI18n();
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);
  const [showGroupForm, setShowGroupForm] = useState(false);
  const [showDefForm, setShowDefForm] = useState(false);

  const groupsState  = useAsync(catalogItemService.getAttributeGroups);
  const defsState    = useAsync(
    () => catalogItemService.getAttributeDefinitions(selectedGroupId ?? undefined),
    [selectedGroupId]
  );

  const groupForm = useForm<GroupForm>({
    resolver: zodResolver(groupSchema) as Resolver<GroupForm>,
    defaultValues: { code: '', name: '', sortOrder: 0 },
  });

  const defForm = useForm<DefForm>({
    resolver: zodResolver(defSchema) as Resolver<DefForm>,
    defaultValues: { groupId: '', code: '', name: '', dataType: 'Text', isVariantAxis: false, allowedValues: null, isRequired: false },
  });

  const handleCreateGroup = async (values: GroupForm) => {
    setError(null);
    setSaving(true);
    try {
      await catalogItemService.createAttributeGroup(values);
      groupsState.refetch();
      groupForm.reset();
      setShowGroupForm(false);
    } catch (err) { setError(formatApiError(err)); }
    finally { setSaving(false); }
  };

  const handleCreateDef = async (values: DefForm) => {
    setError(null);
    setSaving(true);
    try {
      await catalogItemService.createAttributeDefinition(values);
      defsState.refetch();
      defForm.reset();
      setShowDefForm(false);
    } catch (err) { setError(formatApiError(err)); }
    finally { setSaving(false); }
  };

  const groups = groupsState.data ?? [];
  const defs   = defsState.data ?? [];

  return (
    <ErpPageTemplate
      kicker={t('items.catalog.kicker', 'Catálogo')}
      title={t('items.attributes.title', 'Atributos y Variantes')}
      action={
        <div style={{ display: 'flex', gap: 8 }}>
          <ZHBtn variant="secondary" size="md" type="button" onClick={() => setShowGroupForm(true)}>
            + {t('items.attributes.newGroup', 'Grupo')}
          </ZHBtn>
          <ZHBtn variant="primary" size="md" type="button" onClick={() => setShowDefForm(true)}>
            + {t('items.attributes.newDef', 'Atributo')}
          </ZHBtn>
        </div>
      }
    >
      {error && <ZHPageNotice variant="error" message={error} />}

      <div style={{ display: 'grid', gridTemplateColumns: '280px 1fr', gap: 24 }}>
        {/* Groups panel */}
        <div>
          <h3 style={{ marginBottom: 12, fontSize: 14, fontWeight: 600, color: 'var(--color-text-secondary)' }}>
            {t('items.attributes.groups', 'Grupos')}
          </h3>
          {groupsState.loading ? (
            <p>{t('common.loading', 'Cargando...')}</p>
          ) : (
            <ul style={{ listStyle: 'none', padding: 0, margin: 0 }}>
              <li>
                <button
                  type="button"
                  className={`prd-tab-btn ${!selectedGroupId ? 'prd-tab-btn--active' : ''}`}
                  style={{ width: '100%', textAlign: 'left' }}
                  onClick={() => setSelectedGroupId(null)}
                >
                  {t('common.all', 'Todos')}
                </button>
              </li>
              {groups.map((g) => (
                <li key={g.id}>
                  <button
                    type="button"
                    className={`prd-tab-btn ${selectedGroupId === g.id ? 'prd-tab-btn--active' : ''}`}
                    style={{ width: '100%', textAlign: 'left' }}
                    onClick={() => setSelectedGroupId(g.id)}
                  >
                    {g.name}
                    <span style={{ color: 'var(--color-text-tertiary)', marginLeft: 6, fontSize: 12 }}>({g.code})</span>
                    {!g.isActive && (
                      <span style={{ marginLeft: 6, color: 'var(--color-text-tertiary)', fontSize: 11 }}>
                        {t('common.inactive', 'inactivo')}
                      </span>
                    )}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        {/* Definitions panel */}
        <div>
          <h3 style={{ marginBottom: 12, fontSize: 14, fontWeight: 600, color: 'var(--color-text-secondary)' }}>
            {t('items.attributes.definitions', 'Definiciones de atributo')}
          </h3>
          {defsState.loading ? (
            <p>{t('common.loading', 'Cargando...')}</p>
          ) : defs.length === 0 ? (
            <p style={{ color: 'var(--color-text-tertiary)' }}>
              {t('items.attributes.noDefinitions', 'No hay atributos definidos.')}
            </p>
          ) : (
            <table className="zh-table">
              <thead>
                <tr>
                  <th>{t('common.code', 'Código')}</th>
                  <th>{t('common.name', 'Nombre')}</th>
                  <th>{t('items.attributes.dataType', 'Tipo')}</th>
                  <th>{t('items.attributes.isVariantAxis', 'Eje variante')}</th>
                  <th>{t('common.status', 'Estado')}</th>
                </tr>
              </thead>
              <tbody>
                {defs.map((d) => (
                  <tr key={d.id}>
                    <td><code>{d.code}</code></td>
                    <td>{d.name}</td>
                    <td>{d.dataType}</td>
                    <td>
                      {d.isVariantAxis ? (
                        <span style={{ color: 'var(--color-success)', fontWeight: 600 }}>✓</span>
                      ) : '–'}
                    </td>
                    <td>
                      <span className={d.isActive ? 'zh-badge zh-badge--success' : 'zh-badge zh-badge--neutral'}>
                        {d.isActive ? t('common.active', 'Activo') : t('common.inactive', 'Inactivo')}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* New group form */}
      {showGroupForm && (
        <div className="zh-modal-backdrop">
          <div className="zh-modal">
            <h3>{t('items.attributes.newGroup', 'Nuevo grupo de atributos')}</h3>
            <form onSubmit={groupForm.handleSubmit(handleCreateGroup)}>
              <ZHFormSection>
                <ZHGrid cols={2}>
                  <ZHField label={t('common.code', 'Código')} required fieldError={groupForm.formState.errors.code?.message}>
                    <input {...groupForm.register('code')} placeholder="VARIANT" disabled={saving} />
                  </ZHField>
                  <ZHField label={t('common.name', 'Nombre')} required fieldError={groupForm.formState.errors.name?.message}>
                    <input {...groupForm.register('name')} placeholder="Variantes" disabled={saving} />
                  </ZHField>
                </ZHGrid>
              </ZHFormSection>
              <div className="zh-form-actions-row zh-form-actions-row--end">
                <ZHBtn type="button" variant="ghost" size="md" onClick={() => { setShowGroupForm(false); groupForm.reset(); }} disabled={saving}>
                  {t('common.cancel', 'Cancelar')}
                </ZHBtn>
                <ZHBtn type="submit" variant="primary" size="md" disabled={saving}>
                  {saving ? t('common.saving', 'Guardando...') : t('common.save', 'Guardar')}
                </ZHBtn>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* New definition form */}
      {showDefForm && (
        <div className="zh-modal-backdrop">
          <div className="zh-modal">
            <h3>{t('items.attributes.newDef', 'Nuevo atributo')}</h3>
            <form onSubmit={defForm.handleSubmit(handleCreateDef)}>
              <ZHFormSection>
                <ZHGrid cols={2}>
                  <ZHField label={t('items.attributes.group', 'Grupo')} required fieldError={defForm.formState.errors.groupId?.message}>
                    <select {...defForm.register('groupId')} disabled={saving}>
                      <option value="">{t('common.selectOption', '— Seleccionar —')}</option>
                      {groups.filter(g => g.isActive).map(g => (
                        <option key={g.id} value={g.id}>{g.name}</option>
                      ))}
                    </select>
                  </ZHField>
                  <ZHField label={t('common.code', 'Código')} required fieldError={defForm.formState.errors.code?.message}>
                    <input {...defForm.register('code')} placeholder="COLOR" disabled={saving} />
                  </ZHField>
                  <ZHField label={t('common.name', 'Nombre')} required fieldError={defForm.formState.errors.name?.message}>
                    <input {...defForm.register('name')} placeholder="Color" disabled={saving} />
                  </ZHField>
                  <ZHField label={t('items.attributes.dataType', 'Tipo de dato')} required>
                    <select {...defForm.register('dataType')} disabled={saving}>
                      <option value="Text">Text</option>
                      <option value="Number">Number</option>
                      <option value="Boolean">Boolean</option>
                      <option value="Date">Date</option>
                      <option value="List">List</option>
                      <option value="Color">Color</option>
                    </select>
                  </ZHField>
                </ZHGrid>
                <ZHGrid cols={2}>
                  <ZHField label={t('items.attributes.isVariantAxis', 'Es eje de variante')}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <input type="checkbox" {...defForm.register('isVariantAxis')} disabled={saving} />
                      {t('items.attributes.variantAxisHint', 'Define dimensión de variante (ej: Color, Talla)')}
                    </label>
                  </ZHField>
                  <ZHField label={t('items.attributes.isRequired', 'Obligatorio')}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <input type="checkbox" {...defForm.register('isRequired')} disabled={saving} />
                    </label>
                  </ZHField>
                </ZHGrid>
              </ZHFormSection>
              <div className="zh-form-actions-row zh-form-actions-row--end">
                <ZHBtn type="button" variant="ghost" size="md" onClick={() => { setShowDefForm(false); defForm.reset(); }} disabled={saving}>
                  {t('common.cancel', 'Cancelar')}
                </ZHBtn>
                <ZHBtn type="submit" variant="primary" size="md" disabled={saving}>
                  {saving ? t('common.saving', 'Guardando...') : t('common.save', 'Guardar')}
                </ZHBtn>
              </div>
            </form>
          </div>
        </div>
      )}
    </ErpPageTemplate>
  );
}
