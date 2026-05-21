import { useMemo, useState } from 'react';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { useAsync } from '../../../../hooks/useAsync';
import { useI18n } from '../../../../i18n/i18n';
import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useBodegas } from '../../transferencias/hooks/useTransferencias';
import { useKardexConsulta } from '../hooks/useKardex';
import type { KardexFilter } from '../api/kardexService';

interface ProductoOpcion {
  id: string;
  shortName: string;
  saleCode?: string;
  isActive: boolean;
  isService: boolean;
  tracksStock: boolean;
}

function firstDayOfMonth(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-01`;
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

function formatQty(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(2);
}

function formatMoney(value: number): string {
  return value.toLocaleString('es-EC', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

export function KardexPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const canView = hasPerm('inventory.kardex.view');

  const { data: bodegas, loading: loadingBodegas } = useBodegas();
  const { data: productos } = useAsync<ProductoOpcion[]>(async () => {
    const res = await api.get<ApiResponse<ProductoOpcion[]>>('/api/inventory/products');
    return (res.data.responseObject ?? []).filter((p) => p.isActive && !p.isService && p.tracksStock);
  });

  const [warehouseId, setWarehouseId] = useState('');
  const [productId,   setProductId]   = useState('');
  const [fechaInicio, setFechaInicio] = useState(firstDayOfMonth);
  const [fechaFin,    setFechaFin]    = useState(todayIso);

  const { data, loading, error, progress, exporting, consultar, exportFile } = useKardexConsulta();

  const filter: KardexFilter | null = useMemo(() => {
    if (!warehouseId || !productId) return null;
    return { warehouseId, productId, fechaInicio, fechaFin };
  }, [warehouseId, productId, fechaInicio, fechaFin]);

  const canConsult = Boolean(filter) && !loading;
  const canExport  = Boolean(filter && data);

  if (!canView) {
    return <NoAccessPage title={t('app.nav.item.inventory.kardex')} />;
  }

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.inventory')}
      title={t('app.nav.item.inventory.kardex')}
      subtitle="Kardex valorizado por producto y bodega (promedio ponderado móvil)."
      action={
        canExport && filter ? (
          <>
            <ZHBtn
              variant="secondary"
              size="md"
              type="button"
              disabled={!!exporting}
              onClick={() => void exportFile(filter, 'excel')}
            >
              <span className="material-symbols-outlined">table</span>
              {exporting === 'excel' ? 'Exportando…' : 'Excel'}
            </ZHBtn>
            <ZHBtn
              variant="secondary"
              size="md"
              type="button"
              disabled={!!exporting}
              onClick={() => void exportFile(filter, 'pdf')}
            >
              <span className="material-symbols-outlined">picture_as_pdf</span>
              {exporting === 'pdf' ? 'Exportando…' : 'PDF'}
            </ZHBtn>
          </>
        ) : undefined
      }
    >
      {error && <ZHPageNotice variant="error" message="Error al consultar kardex" detail={error} />}
      {progress && <ZHPageNotice variant="info" message={progress} />}

      <div className="pg-section">
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <select
              className="zh-input"
              value={warehouseId}
              disabled={loadingBodegas || loading}
              onChange={(e) => setWarehouseId(e.target.value)}
            >
              <option value="">Seleccione bodega…</option>
              {(bodegas ?? []).map((b) => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
            <select
              className="zh-input"
              value={productId}
              disabled={loading || !warehouseId}
              onChange={(e) => setProductId(e.target.value)}
            >
              <option value="">Seleccione producto…</option>
              {(productos ?? []).map((p) => (
                <option key={p.id} value={p.id}>{p.shortName}</option>
              ))}
            </select>
            <input
              className="zh-input"
              type="date"
              value={fechaInicio}
              disabled={loading}
              onChange={(e) => setFechaInicio(e.target.value)}
            />
            <input
              className="zh-input"
              type="date"
              value={fechaFin}
              disabled={loading}
              onChange={(e) => setFechaFin(e.target.value)}
            />
            <ZHBtn
              variant="primary"
              size="md"
              type="button"
              disabled={!canConsult}
              onClick={() => filter && void consultar(filter)}
            >
              <span className="material-symbols-outlined">search</span>
              Consultar
            </ZHBtn>
          </div>
          {data && (
            <div className="pg-table-controls-right">
              <span>{data.rows.length} movimientos</span>
            </div>
          )}
        </div>

        {!data && !loading ? (
          <div className="pg-pad-40">
            <EmptyState message="Seleccione bodega, producto y rango de fechas, luego pulse Consultar." />
          </div>
        ) : loading ? (
          <div className="pg-pad-40"><LoadingState /></div>
        ) : data ? (
          <>
            <div className="pg-kpis">
              <div className="pg-kpi pg-kpi--h">
                <div className="pg-kpi-icon pg-kpi-icon--primary">
                  <span className="material-symbols-outlined">inventory_2</span>
                </div>
                <div className="pg-kpi-bottom">
                  <p className="pg-kpi-label">Saldo inicial</p>
                  <p className="pg-kpi-value">{formatQty(data.resumen.openingQuantity)}</p>
                  <p className="subtle pg-text-muted-sm">${formatMoney(data.resumen.openingValue)}</p>
                </div>
              </div>
              <div className="pg-kpi pg-kpi--h">
                <div className="pg-kpi-icon pg-kpi-icon--success">
                  <span className="material-symbols-outlined">arrow_downward</span>
                </div>
                <div className="pg-kpi-bottom">
                  <p className="pg-kpi-label">Entradas</p>
                  <p className="pg-kpi-value">{formatQty(data.resumen.totalInboundQuantity)}</p>
                  <p className="subtle pg-text-muted-sm">${formatMoney(data.resumen.totalInboundValue)}</p>
                </div>
              </div>
              <div className="pg-kpi pg-kpi--h">
                <div className="pg-kpi-icon pg-kpi-icon--warning">
                  <span className="material-symbols-outlined">arrow_upward</span>
                </div>
                <div className="pg-kpi-bottom">
                  <p className="pg-kpi-label">Salidas</p>
                  <p className="pg-kpi-value">{formatQty(data.resumen.totalOutboundQuantity)}</p>
                  <p className="subtle pg-text-muted-sm">${formatMoney(data.resumen.totalOutboundValue)}</p>
                </div>
              </div>
              <div className="pg-kpi pg-kpi--h">
                <div className="pg-kpi-icon pg-kpi-icon--primary">
                  <span className="material-symbols-outlined">account_balance_wallet</span>
                </div>
                <div className="pg-kpi-bottom">
                  <p className="pg-kpi-label">Saldo final</p>
                  <p className="pg-kpi-value">{formatQty(data.resumen.closingQuantity)}</p>
                  <p className="subtle pg-text-muted-sm">
                    CPP ${formatMoney(data.resumen.finalAverageCost)}
                  </p>
                </div>
              </div>
            </div>

            <p className="subtle pg-text-muted-sm pg-mb-16">
              <strong>{data.producto.codigo}</strong> — {data.producto.name}
              {' · '}
              Bodega: <strong>{data.warehouse.name}</strong>
            </p>

            {data.rows.length === 0 ? (
              <div className="pg-pad-40">
                <EmptyState message="No hay movimientos en el período seleccionado." />
              </div>
            ) : (
              <div className="pg-overflow-x">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Fecha</th>
                      <th>Tipo</th>
                      <th>Referencia</th>
                      <th className="pg-th-right">Ent. cant.</th>
                      <th className="pg-th-right">Ent. valor</th>
                      <th className="pg-th-right">Sal. cant.</th>
                      <th className="pg-th-right">Sal. valor</th>
                      <th className="pg-th-right">Saldo cant.</th>
                      <th className="pg-th-right">Saldo valor</th>
                      <th className="pg-th-right">CPP</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.rows.map((row, idx) => (
                      <tr key={`${row.fecha}-${row.movementType}-${idx}`}>
                        <td>{new Date(row.fecha).toLocaleString('es')}</td>
                        <td>{row.movementType}</td>
                        <td>{row.referencia ?? '—'}</td>
                        <td className="pg-td-right">{row.inboundQuantity ? formatQty(row.inboundQuantity) : '—'}</td>
                        <td className="pg-td-right">{row.inboundValue ? `$${formatMoney(row.inboundValue)}` : '—'}</td>
                        <td className="pg-td-right">{row.outboundQuantity ? formatQty(row.outboundQuantity) : '—'}</td>
                        <td className="pg-td-right">{row.outboundValue ? `$${formatMoney(row.outboundValue)}` : '—'}</td>
                        <td className="pg-td-right"><strong>{formatQty(row.balanceQuantity)}</strong></td>
                        <td className="pg-td-right">${formatMoney(row.balanceValue)}</td>
                        <td className="pg-td-right">${formatMoney(row.averageUnitCost)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        ) : null}
      </div>
    </ErpPageTemplate>
  );
}
