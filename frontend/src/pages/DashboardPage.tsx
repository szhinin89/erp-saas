import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { PageShell } from '../components/PageShell';
import {
  ZHActivityPanel,
  ZHChartPanel,
  ZHDashboardScaffold,
  ZHKpiGrid,
  ZHModulesPanel,
  ZHPanelGrid,
  type ZHProgressPct,
  type ZHModuleTone,
} from '../components/zh/ZHDashboard';

export function DashboardPage() {
  const user = useAuthStore((s) => s.user);
  const { t } = useI18n();

  const kpis = [
    { label: t('dashboard.kpis.totalIncome'), value: '—', tone: 'neutral' as const },
    { label: t('dashboard.kpis.monthExpenses'), value: '—', tone: 'danger' as const },
    { label: t('dashboard.kpis.netProfit'), value: '—', tone: 'success' as const },
    { label: t('dashboard.kpis.activeAccounts'), value: '—', tone: 'info' as const },
  ];

  const activity = [
    { title: 'Asiento #A-0248 registrado', meta: 'Contabilidad · hace 12 min' },
    { title: 'Cuenta 1.1.02.003 creada', meta: 'Carlos M. · hace 1 hora' },
    { title: 'Conciliación pendiente: Banco Pichincha', meta: 'Sistema · hace 3 horas' },
  ];

  const modules = [
    { label: 'Contabilidad', value: '—', pct: 78, tone: 'success' },
    { label: 'Inventario', value: '—', pct: 45, tone: 'info' },
    { label: 'Ventas', value: '—', pct: 92, tone: 'warning' },
    { label: 'RRHH', value: '—', pct: 30, tone: 'neutral' },
  ] satisfies { label: string; value: string; pct: ZHProgressPct; tone: ZHModuleTone }[];

  return (
    <PageShell
      kicker={t('app.nav.group.home')}
      title={t('dashboard.title')}
      subtitle={`${t('dashboard.welcome')} ${user?.fullName ?? ''}`}
    >
      <ZHDashboardScaffold>
        <ZHKpiGrid items={kpis} />

        <ZHPanelGrid>
          <ZHChartPanel title="Ingresos vs Egresos" yearLabel="2025" />
          <ZHActivityPanel title="Actividad reciente" items={activity} />
        </ZHPanelGrid>

        <ZHModulesPanel
          title="Módulos del sistema"
          rightLabel="Ejercicio fiscal 2025"
          items={modules}
        />
      </ZHDashboardScaffold>
    </PageShell>
  );
}
