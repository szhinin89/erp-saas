import { useCallback, useEffect, useMemo, useState } from 'react';
import { Badge, EmptyState, LoadingState, NoAccessPage, PageShell } from '../components/PageShell';
import { Card } from '../components/ui/Card';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid } from '../components/zh/ZHForm';
import { SearchBar } from '../components/ui';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { EntityAuditPanel } from '../components/EntityAuditPanel';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import { branchService, type BranchDto } from '../services/branchService';
import { bodegaService, type BodegaPayload, type BodegaDto } from '../services/bodegaService';
import './BodegasPage.css';

type FormState = {
  sucursalId: string;
  nombre: string;
  ubicacion: string;
  encargado: string;
};

const emptyForm: FormState = {
  sucursalId: '',
  nombre: '',
  ubicacion: '',
  encargado: '',
};

function toPayload(form: FormState): BodegaPayload {
  return {
    sucursalId: form.sucursalId.trim(),
    nombre: form.nombre.trim(),
    ubicacion: form.ubicacion.trim() || null,
    encargado: form.encargado.trim() || null,
  };
}

export function BodegasPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';

  const canView = isAdmin || hasPerm('inventario.bodegas.view');
  const canCreate = isAdmin || hasPerm('inventario.bodegas.create');
  const canUpdate = isAdmin || hasPerm('inventario.bodegas.update');
  const canDelete = isAdmin || hasPerm('inventario.bodegas.delete');

  const [items, setItems] = useState<BodegaDto[]>([]);
  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [searchApplied, setSearchApplied] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [auditEntityId, setAuditEntityId] = useState<string | null>(null);
  const [auditRefreshKey, setAuditRefreshKey] = useState(0);

  useEffect(() => {
    const id = window.setTimeout(() => setSearchApplied(searchQuery.trim()), 280);
    return () => window.clearTimeout(id);
  }, [searchQuery]);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [rows, branchRows] = await Promise.all([
        bodegaService.list('all', searchApplied || undefined),
        branchService.list('active'),
      ]);
      setItems(rows ?? []);
      setBranches(branchRows ?? []);
      if (!editingId && !form.sucursalId && branchRows.length > 0) {
        setForm((prev) => ({ ...prev, sucursalId: branchRows[0]!.id }));
      }
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric');
      setError(msg);
      setItems([]);
      setBranches([]);
    } finally {
      setLoading(false);
    }
  }, [editingId, form.sucursalId, searchApplied, t]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const branchNameById = useMemo(() => {
    const map = new Map<string, string>();
    for (const b of branches) map.set(b.id, b.name);
    return map;
  }, [branches]);

  const resetDraft = () => {
    setEditingId(null);
    setForm({
      ...emptyForm,
      sucursalId: branches[0]?.id ?? '',
    });
  };

  const openEdit = async (row: BodegaDto) => {
    setEditingId(row.id);
    setAuditEntityId(row.id);
    setForm({
      sucursalId: row.sucursalId,
      nombre: row.nombre,
      ubicacion: row.ubicacion ?? '',
      encargado: row.encargado ?? '',
    });
  };

  const save = async () => {
    if (saving) return;
    setError('');
    const payload = toPayload(form);
    if (!payload.sucursalId || !payload.nombre) {
      setError('Completa sucursal y nombre.');
      return;
    }
    setSaving(true);
    try {
      if (editingId) {
        await bodegaService.update(editingId, { id: editingId, ...payload });
        setAuditEntityId(editingId);
      } else {
        const created = await bodegaService.create(payload);
        setAuditEntityId(created.id);
      }
      setAuditRefreshKey((k) => k + 1);
      await refresh();
      resetDraft();
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric');
      setError(msg);
    } finally {
      setSaving(false);
    }
  };

  const toggleActive = async (row: BodegaDto) => {
    setError('');
    try {
      if (row.isActive) {
        if (!canDelete) return;
        await bodegaService.disable(row.id);
      } else {
        if (!canUpdate) return;
        await bodegaService.enable(row.id);
      }
      await refresh();
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric');
      setError(msg);
    }
  };

  if (!canView) return <NoAccessPage title="Bodegas" />;

  return (
    <PageShell
      kicker={t('app.nav.group.inventario')}
      title="Bodegas"
      action={
        <ZHBtn variant="primary" size="md" type="button" onClick={save} disabled={saving || !canCreate && !editingId}>
          {saving ? t('common.saving') : editingId ? t('common.saveChanges') : 'Crear bodega'}
        </ZHBtn>
      }
    >
      <Card className="bodegas-card">
        {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}

        <ZHFormSection title={editingId ? 'Editar bodega' : 'Nueva bodega'}>
          <ZHGrid cols={2}>
            <ZHField label="Sucursal" required>
              <select
                value={form.sucursalId}
                onChange={(e) => setForm((prev) => ({ ...prev, sucursalId: e.target.value }))}
                disabled={saving || (!canCreate && !editingId)}
              >
                <option value="">{t('common.select')}</option>
                {branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
              </select>
            </ZHField>
            <ZHField label="Nombre" required>
              <input
                value={form.nombre}
                onChange={(e) => setForm((prev) => ({ ...prev, nombre: e.target.value }))}
                disabled={saving || (editingId ? !canUpdate : !canCreate)}
              />
            </ZHField>
            <ZHField label="Ubicación">
              <input
                value={form.ubicacion}
                onChange={(e) => setForm((prev) => ({ ...prev, ubicacion: e.target.value }))}
                disabled={saving || (editingId ? !canUpdate : !canCreate)}
              />
            </ZHField>
            <ZHField label="Encargado">
              <input
                value={form.encargado}
                onChange={(e) => setForm((prev) => ({ ...prev, encargado: e.target.value }))}
                disabled={saving || (editingId ? !canUpdate : !canCreate)}
              />
            </ZHField>
          </ZHGrid>
          <div className="zh-form-actions-row">
            <ZHBtn variant="ghost" size="sm" type="button" onClick={resetDraft} disabled={saving}>
              {editingId ? 'Cancelar edición' : 'Limpiar'}
            </ZHBtn>
          </div>
        </ZHFormSection>

        <div className="bodegas-search-row">
          <SearchBar
            searchQuery={searchQuery}
            onSearch={setSearchQuery}
            onClearAll={() => {
              setSearchQuery('');
              setSearchApplied('');
            }}
            filterValues={{}}
            placeholder="Buscar bodega..."
            resultCount={items.length}
            entityLabel="bodegas"
            loading={loading}
            actionLabel={canCreate ? 'Nueva bodega' : undefined}
            onAction={canCreate ? resetDraft : undefined}
          />
        </div>

        {loading ? (
          <LoadingState />
        ) : items.length === 0 ? (
          <EmptyState message={t('common.noData')} />
        ) : (
          <table className="table bodegas-responsive-table">
            <thead>
              <tr>
                <th>Nombre</th>
                <th>Sucursal</th>
                <th>Ubicación</th>
                <th>Encargado</th>
                <th>{t('common.status')}</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {items.map((row) => (
                <tr key={row.id}>
                  <td data-label="Nombre">{row.nombre}</td>
                  <td data-label="Sucursal">{branchNameById.get(row.sucursalId) ?? row.sucursalId}</td>
                  <td data-label="Ubicación">{row.ubicacion ?? '—'}</td>
                  <td data-label="Encargado">{row.encargado ?? '—'}</td>
                  <td data-label={t('common.status')}>
                    <Badge label={row.isActive ? t('common.active') : t('common.inactive')} variant={row.isActive ? 'green' : 'gray'} />
                  </td>
                  <td data-label="Acciones">
                    <div className="bodegas-actions-cell">
                      {canUpdate ? (
                        <ZHBtn variant="secondary" size="xs" type="button" onClick={() => void openEdit(row)}>
                          {t('common.edit')}
                        </ZHBtn>
                      ) : null}
                      {row.isActive ? (
                        canDelete ? (
                          <ZHBtn variant="ghost" size="xs" type="button" onClick={() => void toggleActive(row)}>
                            Deshabilitar
                          </ZHBtn>
                        ) : null
                      ) : canUpdate ? (
                        <ZHBtn variant="ghost" size="xs" type="button" onClick={() => void toggleActive(row)}>
                          Habilitar
                        </ZHBtn>
                      ) : null}
                      <ZHBtn
                        variant="ghost"
                        size="xs"
                        type="button"
                        onClick={() => {
                          setAuditEntityId(row.id);
                          setAuditRefreshKey((k) => k + 1);
                        }}
                      >
                        Auditoría
                      </ZHBtn>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        <ZHFormSection title="Auditoría">
          {auditEntityId ? (
            <EntityAuditPanel entityType="Bodega" entityId={auditEntityId} take={10} refreshKey={auditRefreshKey} />
          ) : (
            <EmptyState message="Selecciona una bodega para ver su auditoría." />
          )}
        </ZHFormSection>
      </Card>
    </PageShell>
  );
}

