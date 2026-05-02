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
    { title: t('dashboard.activity.demo1.title'), meta: t('dashboard.activity.demo1.meta') },
    { title: t('dashboard.activity.demo2.title'), meta: t('dashboard.activity.demo2.meta') },
    { title: t('dashboard.activity.demo3.title'), meta: t('dashboard.activity.demo3.meta') },
  ];

  const modules = [
    { label: t('dashboard.modules.demo.accounting'), value: '—', pct: 78, tone: 'success' },
    { label: t('dashboard.modules.demo.inventory'), value: '—', pct: 45, tone: 'info' },
    { label: t('dashboard.modules.demo.sales'), value: '—', pct: 92, tone: 'warning' },
    { label: t('dashboard.modules.demo.hr'), value: '—', pct: 30, tone: 'neutral' },
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
          <ZHChartPanel title={t('dashboard.chart.incomeVsExpenses')} yearLabel={t('dashboard.chart.year')} />
          <ZHActivityPanel title={t('dashboard.activity.title')} items={activity} />
        </ZHPanelGrid>

        <ZHModulesPanel
          title={t('dashboard.modules.title')}
          rightLabel={t('dashboard.modules.fiscalLine')}
          items={modules}
        />
      </ZHDashboardScaffold>
    </PageShell>
  );
}
