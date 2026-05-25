import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid } from '../zh/ZHForm';
import { ZHPageNotice } from '../zh/ZHPageNotice';
import { useConfig } from '../../modules/config';
import { configService } from '../../modules/config/configService';
import type { ConfigDataType, ConfigEntry, ConfigScope } from '../../modules/config/types';

type ConfigFormValues = {
  scope: ConfigScope;
  key: string;
  module: string;
  feature: string;
  dataType: ConfigDataType;
  value: string;
};

const defaultValues: ConfigFormValues = {
  scope: 'global',
  key: '',
  module: '',
  feature: '',
  dataType: 'string',
  value: '',
};

type ConfigManagementPanelProps = {
  subscriberId: string;
  canManage: boolean;
  title?: string;
  subtitle?: string;
};

function normalize(value: string | null | undefined): string {
  return (value ?? '').trim().toLowerCase();
}

function sortEntries(entries: ConfigEntry[]): ConfigEntry[] {
  return [...entries].sort((a, b) => {
    if (a.scope !== b.scope) return a.scope.localeCompare(b.scope);
    if ((a.module ?? '') !== (b.module ?? '')) return (a.module ?? '').localeCompare(b.module ?? '');
    if ((a.feature ?? '') !== (b.feature ?? '')) return (a.feature ?? '').localeCompare(b.feature ?? '');
    return a.key.localeCompare(b.key);
  });
}

function parseTypedPreview(entry: ConfigEntry): string {
  const dt = normalize(entry.dataType);
  if (dt === 'json') {
    try {
      return JSON.stringify(JSON.parse(entry.value), null, 2);
    } catch {
      return entry.value;
    }
  }
  return entry.value;
}

function validateValueByType(value: string, dataType: ConfigDataType): string | null {
  if (dataType === 'number') {
    const n = Number(value);
    if (!Number.isFinite(n)) return 'El valor debe ser numérico.';
    return null;
  }
  if (dataType === 'boolean') {
    const v = normalize(value);
    if (v !== 'true' && v !== 'false') return "El valor booleano debe ser 'true' o 'false'.";
    return null;
  }
  if (dataType === 'json') {
    try {
      JSON.parse(value);
      return null;
    } catch {
      return 'El valor JSON no es válido.';
    }
  }
  return null;
}

export function ConfigManagementPanel(props: ConfigManagementPanelProps) {
  const { subscriberId, canManage, title = 'Configuración jerárquica', subtitle } = props;
  const config = useConfig();
  const sameTenantAsContext = normalize(config.subscriberId) === normalize(subscriberId);

  const [localEntries, setLocalEntries] = useState<ConfigEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [ok, setOk] = useState('');
  const [search, setSearch] = useState('');
  const [scopeFilter, setScopeFilter] = useState<'all' | ConfigScope>('all');

  const {
    register,
    watch,
    reset,
    setValue,
    handleSubmit,
    formState: { errors },
  } = useForm<ConfigFormValues>({ defaultValues });

  const selectedScope = watch('scope');
  const selectedDataType = watch('dataType');

  const entries = useMemo(() => {
    const source = sameTenantAsContext ? config.entries : localEntries;
    const sorted = sortEntries(source);
    const query = normalize(search);
    return sorted.filter((entry) => {
      if (scopeFilter !== 'all' && entry.scope !== scopeFilter) return false;
      if (!query) return true;
      const haystack = `${entry.scope} ${entry.module ?? ''} ${entry.feature ?? ''} ${entry.key} ${entry.value} ${entry.dataType}`.toLowerCase();
      return haystack.includes(query);
    });
  }, [sameTenantAsContext, config.entries, localEntries, scopeFilter, search]);

  const refresh = async () => {
    setError('');
    setOk('');
    setLoading(true);
    try {
      if (sameTenantAsContext) {
        await config.refresh();
      } else {
        const loaded = await configService.loadSubscriberConfig(subscriberId);
        setLocalEntries(loaded);
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo cargar la configuración.');
    } finally {
      setLoading(false);
    }
  };

  const saveConfig = handleSubmit(async (values) => {
    const key = values.key.trim().toLowerCase();
    const moduleValue = values.module.trim().toLowerCase();
    const featureValue = values.feature.trim().toLowerCase();
    const valueError = validateValueByType(values.value, values.dataType);

    if (!key) {
      setError('La clave es obligatoria.');
      return;
    }
    if (values.scope === 'module' && !moduleValue) {
      setError('Debe seleccionar un módulo para alcance module.');
      return;
    }
    if (values.scope === 'feature' && !featureValue) {
      setError('Debe seleccionar una feature para alcance feature.');
      return;
    }
    if (valueError) {
      setError(valueError);
      return;
    }

    setError('');
    setOk('');
    setSaving(true);
    try {
      const payload = {
        scope: values.scope,
        key,
        module: values.scope === 'module' ? moduleValue : undefined,
        feature: values.scope === 'feature' ? featureValue : undefined,
        dataType: values.dataType,
        value: values.value,
      } as const;

      if (sameTenantAsContext) {
        await config.upsertConfig(payload);
      } else {
        const saved = await configService.upsertConfig(subscriberId, payload);
        setLocalEntries((prev) => {
          const idx = prev.findIndex(
            (x) =>
              x.scope === saved.scope &&
              normalize(x.key) === normalize(saved.key) &&
              normalize(x.module) === normalize(saved.module) &&
              normalize(x.feature) === normalize(saved.feature),
          );
          if (idx < 0) return [...prev, saved];
          const copy = [...prev];
          copy[idx] = saved;
          return copy;
        });
      }

      setOk('Configuración guardada correctamente.');
      reset(defaultValues);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar la configuración.');
    } finally {
      setSaving(false);
    }
  });

  const removeConfig = async (entry: ConfigEntry) => {
    if (!canManage || saving) return;
    setError('');
    setOk('');
    setSaving(true);
    try {
      const payload = {
        scope: entry.scope,
        key: entry.key,
        module: entry.module,
        feature: entry.feature,
      } as const;
      if (sameTenantAsContext) {
        await config.deleteConfig(payload);
      } else {
        await configService.deleteConfig(subscriberId, payload);
        setLocalEntries((prev) =>
          prev.filter(
            (x) =>
              !(
                x.scope === entry.scope &&
                normalize(x.key) === normalize(entry.key) &&
                normalize(x.module) === normalize(entry.module) &&
                normalize(x.feature) === normalize(entry.feature)
              ),
          ),
        );
      }
      setOk('Configuración eliminada.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo eliminar la configuración.');
    } finally {
      setSaving(false);
    }
  };

  const editConfig = (entry: ConfigEntry) => {
    setValue('scope', entry.scope);
    setValue('key', entry.key);
    setValue('module', entry.module ?? '');
    setValue('feature', entry.feature ?? '');
    const dt = normalize(entry.dataType);
    if (dt === 'number' || dt === 'boolean' || dt === 'json' || dt === 'string') {
      setValue('dataType', dt);
    } else {
      setValue('dataType', 'string');
    }
    setValue('value', entry.value);
  };

  return (
    <div className="companies-inner-stack">
      <ZHFormSection title={title}>
        {subtitle ? <p className="companies-config-subtitle">{subtitle}</p> : null}
        <div className="companies-config-toolbar">
          <input
            type="search"
            placeholder="Buscar por clave, módulo, feature o valor…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="companies-config-search"
          />
          <select value={scopeFilter} onChange={(e) => setScopeFilter(e.target.value as 'all' | ConfigScope)}>
            <option value="all">Todos los alcances</option>
            <option value="global">Global</option>
            <option value="module">Module</option>
            <option value="feature">Feature</option>
          </select>
          <ZHBtn variant="secondary" size="md" type="button" onClick={() => void refresh()} disabled={loading || saving}>
            {loading ? 'Cargando…' : 'Recargar'}
          </ZHBtn>
        </div>
      </ZHFormSection>

      {ok ? <ZHPageNotice variant="success" message={ok} /> : null}
      {error ? <ZHPageNotice variant="error" message="Error" detail={error} /> : null}
      {config.error && sameTenantAsContext ? <ZHPageNotice variant="warning" message={config.error} /> : null}

      {canManage ? (
        <form onSubmit={saveConfig} className="companies-config-form">
          <ZHFormSection title="Crear / editar configuración">
            <ZHGrid cols={3}>
              <ZHField label="Alcance" required fieldError={errors.scope?.message}>
                <select {...register('scope')}>
                  <option value="global">Global</option>
                  <option value="module">Module</option>
                  <option value="feature">Feature</option>
                </select>
              </ZHField>
              <ZHField label="Clave estructurada" required fieldError={errors.key?.message} hint="Ej: ventas.stock.permitir_negativo">
                <input {...register('key', { required: true })} />
              </ZHField>
              <ZHField label="Tipo de dato" required fieldError={errors.dataType?.message}>
                <select {...register('dataType')}>
                  <option value="string">string</option>
                  <option value="number">number</option>
                  <option value="boolean">boolean</option>
                  <option value="json">json</option>
                </select>
              </ZHField>
              {selectedScope === 'module' ? (
                <ZHField label="Módulo" required hint="Clave técnica de módulo, por ejemplo: ventas">
                  <input {...register('module', { required: true })} />
                </ZHField>
              ) : null}
              {selectedScope === 'feature' ? (
                <ZHField label="Feature" required hint="Código técnico de feature, por ejemplo: ui.cliente.mostrar_ruc">
                  <input {...register('feature', { required: true })} />
                </ZHField>
              ) : null}
              <ZHField
                label="Valor"
                required
                hint={
                  selectedDataType === 'boolean'
                    ? "Use true o false"
                    : selectedDataType === 'json'
                      ? 'Ingrese JSON válido'
                      : 'Ingrese el valor para esta clave'
                }
              >
                {selectedDataType === 'json' ? (
                  <textarea rows={6} {...register('value', { required: true })} />
                ) : selectedDataType === 'boolean' ? (
                  <select {...register('value', { required: true })}>
                    <option value="true">true</option>
                    <option value="false">false</option>
                  </select>
                ) : (
                  <input {...register('value', { required: true })} />
                )}
              </ZHField>
            </ZHGrid>
          </ZHFormSection>
          <div className="zh-form-actions-row zh-form-actions-row--end">
            <ZHBtn variant="secondary" size="md" type="button" onClick={() => reset(defaultValues)} disabled={saving}>
              Limpiar
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="submit" disabled={saving}>
              {saving ? 'Guardando…' : 'Guardar configuración'}
            </ZHBtn>
          </div>
        </form>
      ) : null}

      <div className="companies-config-list">
        {entries.length === 0 ? (
          <p className="companies-config-empty">No hay configuraciones cargadas para este subscriber.</p>
        ) : (
          <table className="companies-table companies-table--embedded">
            <thead>
              <tr>
                <th>Alcance</th>
                <th>Módulo/Feature</th>
                <th>Clave</th>
                <th>Tipo</th>
                <th>Valor</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {entries.map((entry) => (
                <tr key={`${entry.scope}:${entry.module ?? ''}:${entry.feature ?? ''}:${entry.key}`}>
                  <td>{entry.scope}</td>
                  <td>{entry.scope === 'module' ? entry.module : entry.scope === 'feature' ? entry.feature : '—'}</td>
                  <td className="mono companies-mono">{entry.key}</td>
                  <td>{entry.dataType}</td>
                  <td>
                    <pre className="companies-config-value">{parseTypedPreview(entry)}</pre>
                  </td>
                  <td>
                    <div className="companies-actions-row">
                      {canManage ? (
                        <>
                          <ZHBtn variant="secondary" size="sm" type="button" onClick={() => editConfig(entry)} disabled={saving}>
                            Editar
                          </ZHBtn>
                          <ZHBtn variant="destructive" size="sm" type="button" onClick={() => void removeConfig(entry)} disabled={saving}>
                            Eliminar
                          </ZHBtn>
                        </>
                      ) : (
                        <span>Solo lectura</span>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

