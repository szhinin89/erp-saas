import { useState } from 'react';
import {
  ReportPage,
  ReportKpiCard,
  ReportFiltersBar,
  ReportFilterField,
  ReportChartPanel,
  ReportChartDates,
  ReportChartTooltip,
  ReportTablePanel,
  ReportTable,
  ReportTableHead,
  ReportTh,
  ReportTd,
  ReportClientAvatar,
  ReportRowId,
  ReportStatusBadge,
  ReportRowActions,
  type RptPeriod,
} from '../components/ReportPageTemplate';
import { ZHBtn } from '../components/zh/ZHForm';

const CHART_DATES = ['Oct 01', 'Oct 07', 'Oct 14', 'Oct 21', 'Oct 28', 'Oct 31'];

const DEMO_ROWS = [
  { id: '#ZH-49210', date: '31 Oct 2024, 14:22', client: 'Global Connect S.A.',  amount: '$4,250.00',  status: 'success' as const, statusLabel: 'Completado' },
  { id: '#ZH-49209', date: '31 Oct 2024, 12:05', client: 'Tech Rentals Ltd',     amount: '$1,120.00',  status: 'info'    as const, statusLabel: 'Procesando' },
  { id: '#ZH-49208', date: '31 Oct 2024, 10:15', client: 'Innova Matrix',         amount: '$12,800.50', status: 'success' as const, statusLabel: 'Completado' },
  { id: '#ZH-49207', date: '30 Oct 2024, 17:42', client: 'Distribuidora Central', amount: '$3,100.00',  status: 'warning' as const, statusLabel: 'Pendiente'  },
  { id: '#ZH-49206', date: '30 Oct 2024, 09:30', client: 'Inversiones Delta',     amount: '$560.00',    status: 'error'   as const, statusLabel: 'Cancelado'  },
];

export function SalesReportPage() {
  const [period, setPeriod] = useState<RptPeriod>('day');
  const [search, setSearch] = useState('');

  const filtered = DEMO_ROWS.filter(
    (r) => !search || r.client.toLowerCase().includes(search.toLowerCase()) || r.id.includes(search),
  );

  return (
    <ReportPage
      breadcrumb={['ERP', 'REPORTES']}
      title="Reporte de Ventas"
      subtitle="Análisis detallado de rendimiento comercial y transacciones."
      actions={
        <>
          <ZHBtn variant="secondary" size="md" type="button">
            <span className="material-symbols-outlined">download</span>
            Exportar PDF
          </ZHBtn>
          <ZHBtn variant="primary" size="md" type="button">
            <span className="material-symbols-outlined">share</span>
            Compartir Reporte
          </ZHBtn>
        </>
      }
    >
      {/* KPIs */}
      <div className="pg-kpis">
        <ReportKpiCard
          icon="payments"
          tone="primary"
          badge="+12.4%"
          badgeTone="success"
          label="Total Ventas"
          value="$248,500.00"
        />
        <ReportKpiCard
          icon="analytics"
          tone="secondary"
          badge="Estable"
          badgeTone="warning"
          label="Margen Promedio"
          value="32.4%"
        />
        <ReportKpiCard
          icon="inventory"
          tone="tertiary"
          badge="+5%"
          badgeTone="success"
          label="Pedidos Procesados"
          value="1,429"
          unit="pedidos"
        />
        <ReportKpiCard
          icon="star"
          tone="tertiary"
          label="Top Producto"
          value="Servidor Xeon E5 v4"
          sub="42 unidades vendidas"
        />
      </div>

      {/* Filters */}
      <ReportFiltersBar onClear={() => {}} onApply={() => {}}>
        <ReportFilterField label="Rango de Fechas" icon="calendar_today">
          <select className="zh-input">
            <option>Últimos 30 días (Oct 1 - Oct 31)</option>
            <option>Mes Anterior</option>
            <option>Trimestre Actual</option>
          </select>
        </ReportFilterField>
        <ReportFilterField label="Canal de Venta">
          <select className="zh-input">
            <option>Todos los canales</option>
            <option>Venta Directa</option>
            <option>E-commerce</option>
          </select>
        </ReportFilterField>
        <ReportFilterField label="Categoría">
          <select className="zh-input">
            <option>Todas las categorías</option>
            <option>Hardware</option>
            <option>Suscripciones SaaS</option>
          </select>
        </ReportFilterField>
      </ReportFiltersBar>

      {/* Chart */}
      <ReportChartPanel
        title="Tendencia de Ventas Diarias"
        period={period}
        onPeriod={setPeriod}
      >
        <svg viewBox="0 0 1000 280" preserveAspectRatio="none" aria-hidden>
          <line x1="0" y1="46"  x2="1000" y2="46"  stroke="rgba(0,0,0,0.05)" strokeWidth="1" />
          <line x1="0" y1="140" x2="1000" y2="140" stroke="rgba(0,0,0,0.05)" strokeWidth="1" />
          <line x1="0" y1="233" x2="1000" y2="233" stroke="rgba(0,0,0,0.05)" strokeWidth="1" />
          <defs>
            <linearGradient id="rpt-chart-fill" x1="0" x2="0" y1="0" y2="1">
              <stop offset="0%" stopColor="#3a5f84" stopOpacity="0.18" />
              <stop offset="100%" stopColor="#3a5f84" stopOpacity="0" />
            </linearGradient>
          </defs>
          <path
            d="M0,280 L0,233 L100,204 L200,222 L300,140 L400,167 L500,74 L600,111 L700,56 L800,93 L900,37 L1000,65 L1000,280 Z"
            fill="url(#rpt-chart-fill)"
          />
          <polyline
            points="0,233 100,204 200,222 300,140 400,167 500,74 600,111 700,56 800,93 900,37 1000,65"
            fill="none"
            stroke="#3a5f84"
            strokeWidth="3"
            strokeLinejoin="round"
          />
          <circle cx="500" cy="74"  r="5" fill="#3a5f84" stroke="white" strokeWidth="2" />
          <circle cx="900" cy="37"  r="5" fill="#3a5f84" stroke="white" strokeWidth="2" />
        </svg>

        <div style={{ position: 'absolute', top: 40, left: '48%', transform: 'translateX(-50%)' }}>
          <ReportChartTooltip
            date="15 Octubre"
            rows={[
              { label: 'Ventas:', value: '$12,450.00' },
              { label: 'Vs ayer:', value: '+18%', tone: 'success' },
            ]}
          />
        </div>

        <ReportChartDates labels={CHART_DATES} />
      </ReportChartPanel>

      {/* Table */}
      <ReportTablePanel
        searchPlaceholder="Filtrar ventas…"
        searchValue={search}
        onSearch={setSearch}
        total={1429}
        showing={`1-${filtered.length}`}
        hasNext
        footerLeft="Mostrando registros de los últimos 30 días"
        footerRight="Última actualización: hace 5 minutos"
      >
        <ReportTable>
          <ReportTableHead>
            <ReportTh>ID Pedido</ReportTh>
            <ReportTh>Fecha</ReportTh>
            <ReportTh>Cliente</ReportTh>
            <ReportTh align="right">Monto</ReportTh>
            <ReportTh>Estado</ReportTh>
            <ReportTh align="right">Acciones</ReportTh>
          </ReportTableHead>
          <tbody>
            {filtered.map((row) => (
              <tr key={row.id}>
                <ReportTd><ReportRowId id={row.id} /></ReportTd>
                <ReportTd className="subtle">{row.date}</ReportTd>
                <ReportTd><ReportClientAvatar name={row.client} /></ReportTd>
                <ReportTd align="right">{row.amount}</ReportTd>
                <ReportTd>
                  <ReportStatusBadge label={row.statusLabel} tone={row.status} />
                </ReportTd>
                <ReportTd align="right"><ReportRowActions /></ReportTd>
              </tr>
            ))}
          </tbody>
        </ReportTable>
      </ReportTablePanel>

    </ReportPage>
  );
}
