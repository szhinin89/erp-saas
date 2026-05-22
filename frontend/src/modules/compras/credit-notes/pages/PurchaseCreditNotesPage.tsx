import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../../i18n/i18n';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import {
  purchaseCreditNotesService,
  type PurchaseCreditNote,
} from '../api/purchaseCreditNotesService';

function statusBadge(status: string): string {
  const s = status.toLowerCase();
  const base = 'badge badge--md badge--upper';
  if (s === 'approved' || s === 'aprobado' || s === 'autorizado') return `${base} badge--green`;
  if (s === 'pending' || s === 'pendiente' || s === 'imported') return `${base} badge--orange`;
  return `${base} badge--gray`;
}

function noteTypeLabel(noteType: string): string {
  const nt = noteType.toUpperCase();
  if (nt.includes('CRED')) return 'Crédito';
  if (nt.includes('DEB')) return 'Débito';
  return noteType;
}

export function PurchaseCreditNotesPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView    = canShow('purchases.credit-notes.view');
  const canImport  = canShow('purchases.credit-notes.create');
  const canApprove = canShow('purchases.credit-notes.approve');

  const fileRef = useRef<HTMLInputElement>(null);
  const [rows,         setRows]         = useState<PurchaseCreditNote[]>([]);
  const [loading,      setLoading]      = useState(true);
  const [importing,    setImporting]    = useState(false);
  const [approvingId,  setApprovingId]  = useState<string | null>(null);
  const [error,        setError]        = useState<string | null>(null);
  const [actionError,  setActionError]  = useState<string | null>(null);
  const [q,            setQ]            = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setRows(await purchaseCreditNotesService.list());
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { if (canView) void load(); }, [canView, load]);

  const filtered = useMemo(() => {
    const query = q.trim().toLowerCase();
    if (!query) return rows;
    return rows.filter(
      (r) => r.accessKey.toLowerCase().includes(query) || r.sequential.toLowerCase().includes(query),
    );
  }, [rows, q]);

  const onImport = async (file: File) => {
    setActionError(null);
    setImporting(true);
    try {
      await purchaseCreditNotesService.importXml(file);
      await load();
    } catch (e) {
      setActionError(e instanceof Error ? e.message : 'Error al importar XML');
    } finally {
      setImporting(false);
      if (fileRef.current) fileRef.current.value = '';
    }
  };

  const onApprove = async (id: string) => {
    setActionError(null);
    setApprovingId(id);
    try {
      await purchaseCreditNotesService.approve(id);
      await load();
    } catch (e) {
      setActionError(e instanceof Error ? e.message : 'Error al aprobar');
    } finally {
      setApprovingId(null);
    }
  };

  if (!canView) return <NoAccessPage title={t('app.nav.item.purchases.credit-notes')} />;

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.purchases')}
      title={t('app.nav.item.purchases.credit-notes')}
      subtitle="Notas de crédito y débito recibidas de proveedores (XML SRI)."
      action={
        <>
          <button className="zh-btn zh-btn--secondary" type="button" disabled={loading} onClick={() => void load()}>
            <span className="material-symbols-outlined">refresh</span>
            Actualizar
          </button>
          {canImport && (
            <>
              <input
                ref={fileRef}
                type="file"
                accept=".xml,application/xml,text/xml"
                className="sr-only"
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (file) void onImport(file);
                }}
              />
              <button
                className="zh-btn zh-btn--primary"
                type="button"
                disabled={importing}
                onClick={() => fileRef.current?.click()}
              >
                <span className="material-symbols-outlined">upload_file</span>
                {importing ? 'Importando…' : 'Importar XML'}
              </button>
            </>
          )}
        </>
      }
    >
      {error       && <ZHPageNotice variant="error" message="Error al cargar" detail={error} />}
      {actionError && <ZHPageNotice variant="error" message="Error en la operación" detail={actionError} />}

      <div className="pg-section">
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <div className="pg-search">
              <span className="material-symbols-outlined">search</span>
              <input
                type="text"
                placeholder="Buscar por clave o secuencial…"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                disabled={loading}
              />
            </div>
          </div>
          <div className="pg-table-controls-right">
            <span>{filtered.length} de {rows.length}</span>
          </div>
        </div>

        {loading ? (
          <div className="pg-pad-40"><LoadingState /></div>
        ) : rows.length === 0 ? (
          <div className="pg-pad-40"><EmptyState message="No hay notas de proveedor importadas." /></div>
        ) : filtered.length === 0 ? (
          <div className="pg-pad-40"><EmptyState message="Sin resultados para la búsqueda." /></div>
        ) : (
          <div className="pg-overflow-x">
            <table className="table">
              <thead>
                <tr>
                  <th>Tipo</th>
                  <th>Secuencial</th>
                  <th>Clave de acceso</th>
                  <th>Estado</th>
                  <th className="pg-th-right">Total</th>
                  <th>Fecha</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((row) => {
                  const pending = !['approved', 'aprobado', 'autorizado'].includes(row.status.toLowerCase());
                  return (
                    <tr key={row.id}>
                      <td>{noteTypeLabel(row.noteType)}</td>
                      <td className="mono">{row.sequential || '—'}</td>
                      <td className="mono" title={row.accessKey}>{row.accessKey || '—'}</td>
                      <td><span className={statusBadge(row.status)}>{row.status}</span></td>
                      <td className="pg-td-right">${row.total.toFixed(2)}</td>
                      <td>{new Date(row.issueDate).toLocaleDateString('es')}</td>
                      <td>
                        {pending && canApprove && (
                          <button
                            className="zh-btn zh-btn--primary zh-btn--sm"
                            type="button"
                            disabled={approvingId === row.id}
                            onClick={() => void onApprove(row.id)}
                          >
                            {approvingId === row.id ? 'Aprobando…' : 'Aprobar'}
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </ErpPageTemplate>
  );
}
