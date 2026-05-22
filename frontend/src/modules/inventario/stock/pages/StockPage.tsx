import { useMemo, useState } from 'react';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { useAsync } from '../../../../hooks/useAsync';
import { useI18n } from '../../../../i18n/i18n';
import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';
import { useBodegas } from '../../transferencias/hooks/useTransferencias';
import { useStockList } from '../hooks/useStock';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';

interface ProductoOpcion {
  id: string;
  shortName: string;
  saleCode?: string;
  isActive: boolean;
  isService: boolean;
  tracksStock: boolean;
}

function formatQty(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(2);
}

export function StockPage() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canView = canShow('inventory.stock.view');

  const { data: bodegas, loading: loadingBodegas } = useBodegas();
  const { data: productos } = useAsync<ProductoOpcion[]>(async () => {
    const res = await api.get<ApiResponse<ProductoOpcion[]>>('/api/inventory/products');
    return (res.data.responseObject ?? []).filter((p) => p.isActive && !p.isService && p.tracksStock);
  });

  const [warehouseId, setWarehouseId] = useState('');
  const [productId,   setProductId]   = useState('');

  const filter = warehouseId ? { warehouseId, productId: productId || undefined } : null;
  const { items, loading, error, refetch } = useStockList(filter);

  const productMap = useMemo(() => {
    const map = new Map<string, ProductoOpcion>();
    for (const p of productos ?? []) map.set(p.id, p);
    return map;
  }, [productos]);

  const warehouseName = bodegas?.find((b) => b.id === warehouseId)?.name ?? '';

  const totals = useMemo(() => {
    return items.reduce(
      (acc, row) => ({
        quantity: acc.quantity + row.quantity,
        reserved: acc.reserved + row.reservedQty,
        available: acc.available + row.availableQuantity,
      }),
      { quantity: 0, reserved: 0, available: 0 },
    );
  }, [items]);

  if (!canView) {
    return <NoAccessPage title={t('app.nav.item.inventory.stock')} />;
  }

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.inventory')}
      title={t('app.nav.item.inventory.stock')}
      subtitle="Consulte saldos, reservas y disponibilidad por bodega."
      action={
        warehouseId ? (
          <button
            className="zh-btn zh-btn--secondary"
            type="button"
            disabled={loading}
            onClick={() => refetch()}
          >
            <span className="material-symbols-outlined">refresh</span>
            Actualizar
          </button>
        ) : undefined
      }
    >
      {error && <ZHPageNotice variant="error" message="Error al cargar stock" detail={error} />}

      <div className="pg-section">
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <select
              className="zh-input"
              value={warehouseId}
              disabled={loadingBodegas}
              onChange={(e) => {
                setWarehouseId(e.target.value);
                setProductId('');
              }}
            >
              <option value="">Seleccione bodega…</option>
              {(bodegas ?? []).map((b) => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
            <select
              className="zh-input"
              value={productId}
              disabled={!warehouseId}
              onChange={(e) => setProductId(e.target.value)}
            >
              <option value="">Todos los productos</option>
              {(productos ?? []).map((p) => (
                <option key={p.id} value={p.id}>{p.shortName}</option>
              ))}
            </select>
          </div>
          {warehouseId && (
            <div className="pg-table-controls-right">
              <span>{items.length} productos con stock</span>
            </div>
          )}
        </div>

        {!warehouseId ? (
          <div className="pg-pad-40">
            <EmptyState message="Seleccione una bodega para ver el stock actual." />
          </div>
        ) : loading ? (
          <div className="pg-pad-40"><LoadingState /></div>
        ) : items.length === 0 ? (
          <div className="pg-pad-40">
            <EmptyState message="No hay stock registrado para los filtros seleccionados." />
          </div>
        ) : (
          <>
            <div className="pg-kpis">
              <div className="pg-kpi pg-kpi--h">
                <div className="pg-kpi-icon pg-kpi-icon--primary">
                  <span className="material-symbols-outlined">inventory_2</span>
                </div>
                <div className="pg-kpi-bottom">
                  <p className="pg-kpi-label">Cantidad total</p>
                  <p className="pg-kpi-value">{formatQty(totals.quantity)}</p>
                </div>
              </div>
              <div className="pg-kpi pg-kpi--h">
                <div className="pg-kpi-icon pg-kpi-icon--warning">
                  <span className="material-symbols-outlined">lock</span>
                </div>
                <div className="pg-kpi-bottom">
                  <p className="pg-kpi-label">Reservado</p>
                  <p className="pg-kpi-value">{formatQty(totals.reserved)}</p>
                </div>
              </div>
              <div className="pg-kpi pg-kpi--h">
                <div className="pg-kpi-icon pg-kpi-icon--success">
                  <span className="material-symbols-outlined">check_circle</span>
                </div>
                <div className="pg-kpi-bottom">
                  <p className="pg-kpi-label">Disponible</p>
                  <p className="pg-kpi-value">{formatQty(totals.available)}</p>
                </div>
              </div>
            </div>

            <div className="pg-overflow-x">
              <table className="table">
                <thead>
                  <tr>
                    <th>Producto</th>
                    <th>Código</th>
                    <th className="pg-th-right">Cantidad</th>
                    <th className="pg-th-right">Reservado</th>
                    <th className="pg-th-right">Disponible</th>
                    <th>Última actualización</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((row) => {
                    const product = productMap.get(row.productId);
                    return (
                      <tr key={row.id}>
                        <td>{product?.shortName ?? row.productId}</td>
                        <td className="mono">{product?.saleCode ?? '—'}</td>
                        <td className="pg-td-right">{formatQty(row.quantity)}</td>
                        <td className="pg-td-right">{formatQty(row.reservedQty)}</td>
                        <td className="pg-td-right">
                          <strong>{formatQty(row.availableQuantity)}</strong>
                        </td>
                        <td>{new Date(row.ultimaActualizacion).toLocaleString('es')}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {warehouseName && (
              <p className="subtle pg-text-muted-sm pg-mt-16">
                Bodega: <strong>{warehouseName}</strong>
              </p>
            )}
          </>
        )}
      </div>
    </ErpPageTemplate>
  );
}
