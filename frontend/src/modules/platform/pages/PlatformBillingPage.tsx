import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { platformService } from '../api/platformService';
import { PlatformCrudTemplate } from '../../../templates/PlatformCrudTemplate';
import { StatCard } from '../../../components/zh/StatCard';
import { LoadingState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';

type BillingSummary = NonNullable<Awaited<ReturnType<typeof platformService.getPlatformBillingSummary>>>;

export function PlatformBillingPage() {
  const navigate = useNavigate();
  const [summary, setSummary] = useState<BillingSummary | null>(null);
  const [invoices, setInvoices] = useState<Array<Record<string, unknown>>>([]);
  const [overdue, setOverdue] = useState<Array<Record<string, unknown>>>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([
      platformService.getPlatformBillingSummary(),
      platformService.getPlatformBillingInvoices(30),
      platformService.getPlatformBillingOverdue(),
    ])
      .then(([s, inv, od]) => {
        setSummary(s ?? null);
        setInvoices(inv);
        setOverdue(od);
      })
      .catch(() => setError('No se pudo cargar billing platform.'))
      .finally(() => setLoading(false));
  }, []);

  const t = summary?.totals;

  return (
    <PlatformCrudTemplate
      title="Platform billing"
      subtitle="Facturación SaaS agregada · grace, overdue, facturas recientes"
    >
      {error && <ZHPageNotice variant="error" message={error} />}
      {loading ? <LoadingState /> : t ? (
        <>
          <div className="pg-stat-grid">
            <StatCard label="Suscriptores" value={String(t.subscribers)} />
            <StatCard label="Suspendidos" value={String(t.suspended)} />
            <StatCard label="Gracia" value={String(t.gracePeriod)} />
            <StatCard label="Trial" value={String(t.trial)} />
            <StatCard label="Cuentas vencidas" value={String(t.overdueAccounts)} />
          </div>
          <p className="subtle">{summary?.note}</p>

          <div className="pg-section">
            <h3 className="pg-section-label">Cuentas en mora ({overdue.length})</h3>
            {overdue.length === 0 ? (
              <p className="subtle">Sin cuentas past due.</p>
            ) : (
              <ul className="subtle">
                {overdue.slice(0, 10).map((a) => (
                  <li key={String(a.subscriberId)}>
                    {String(a.subscriberName ?? a.subscriberId)} · {String(a.status)} · {String(a.lifecycle ?? '')}
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="pg-section">
            <h3 className="pg-section-label">Facturas SaaS recientes</h3>
            <div className="pg-overflow-x">
              <table className="table">
                <thead>
                  <tr>
                    <th>Suscriptor</th>
                    <th>Número</th>
                    <th>Estado</th>
                    <th>Total</th>
                    <th>Vencimiento</th>
                  </tr>
                </thead>
                <tbody>
                  {invoices.map((inv) => (
                    <tr key={String(inv.id)}>
                      <td>{String(inv.subscriberName ?? inv.subscriberId ?? '—')}</td>
                      <td>{String(inv.invoiceNumber ?? '—')}</td>
                      <td>{String(inv.status ?? '—')}</td>
                      <td>{String(inv.total ?? inv.totalAmount ?? '—')} {String(inv.currencyCode ?? '')}</td>
                      <td>{String(inv.dueAtUtc ?? '—')}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {invoices.length === 0 && <p className="subtle">Sin facturas emitidas.</p>}
            </div>
          </div>

          <button type="button" className="zh-btn zh-btn--ghost zh-btn--md" onClick={() => navigate('/platform/subscribers')}>
            Ir a suscriptores
          </button>
        </>
      ) : (
        <p className="subtle">Sin datos.</p>
      )}
    </PlatformCrudTemplate>
  );
}
