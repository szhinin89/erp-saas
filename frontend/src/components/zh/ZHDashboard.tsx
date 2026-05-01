import { TableCard } from '../PageShell';
import { ZHGrid } from './ZHForm';
import { ZHCardSection, ZHSection, ZHStack } from './ZHLayout';
import './ZHDashboard.css';

export type ZHKpiTone = 'neutral' | 'danger' | 'success' | 'info';
export type ZHModuleTone = 'neutral' | 'success' | 'warning' | 'info';
export type ZHProgressPct = 30 | 45 | 78 | 92;

export function ZHDashboardScaffold(props: {
  breadcrumb?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="zh-dashboard">
      {props.breadcrumb ? (
        <ZHSection bottom={12}>
          <div className="zh-dash-breadcrumb">{props.breadcrumb}</div>
        </ZHSection>
      ) : null}
      {props.children}
    </div>
  );
}

export function ZHKpiGrid(props: {
  items: { label: string; value: string; tone?: ZHKpiTone }[];
}) {
  return (
    <ZHSection bottom={16}>
      <div className="zh-dash-kpiGrid">
        {props.items.map((k) => (
          <div key={k.label} className={`zh-dash-kpiCard zh-dash-kpiCard--${k.tone ?? 'neutral'}`}>
            <div className="zh-dash-kpiValue">{k.value}</div>
            <div className="zh-dash-kpiLabel">{k.label}</div>
          </div>
        ))}
      </div>
    </ZHSection>
  );
}

export function ZHKpiPanel(props: {
  title: string;
  items: { label: string; value: string; tone?: ZHKpiTone }[];
}) {
  return (
    <TableCard>
      <ZHCardSection title={props.title}>
        <div className="zh-dash-kpiGrid">
          {props.items.map((k) => (
            <div key={k.label} className={`zh-dash-kpiCard zh-dash-kpiCard--${k.tone ?? 'neutral'}`}>
              <div className="zh-dash-kpiValue">{k.value}</div>
              <div className="zh-dash-kpiLabel">{k.label}</div>
            </div>
          ))}
        </div>
      </ZHCardSection>
    </TableCard>
  );
}

export function ZHPanelGrid(props: { children: React.ReactNode; className?: string }) {
  return <div className={['zh-dash-panels', props.className].filter(Boolean).join(' ')}>{props.children}</div>;
}

export function ZHChartPanel(props: {
  title: string;
  yearLabel?: string;
  bars?: number;
}) {
  const bars = props.bars ?? 5;
  return (
    <TableCard>
      <ZHCardSection
        title={props.title}
        right={props.yearLabel ? <div className="zh-dash-pill">{props.yearLabel}</div> : undefined}
      >
        <div className="zh-dash-chart">
          {Array.from({ length: bars }).map((_, i) => (
            <div key={i} className="zh-skeleton zh-dash-chartBar" />
          ))}
        </div>
        <div className="zh-dash-chartLegend">
          <span className="zh-dash-legendItem">
            <span className="zh-dash-legendDot zh-dash-legendDot--income" /> Ingresos
          </span>
          <span className="zh-dash-legendItem">
            <span className="zh-dash-legendDot zh-dash-legendDot--expense" /> Egresos
          </span>
        </div>
      </ZHCardSection>
    </TableCard>
  );
}

export function ZHActivityPanel(props: {
  title: string;
  items: { title: string; meta: string }[];
}) {
  return (
    <TableCard>
      <ZHCardSection title={props.title}>
        <ZHStack gap={10}>
          {props.items.map((a) => (
            <div key={a.title} className="zh-dash-activityItem">
              <div className="zh-dash-activityTitle">{a.title}</div>
              <div className="zh-dash-activityMeta">{a.meta}</div>
            </div>
          ))}
        </ZHStack>
      </ZHCardSection>
    </TableCard>
  );
}

export function ZHModulesPanel(props: {
  title: string;
  rightLabel?: string;
  items: { label: string; value: string; pct: ZHProgressPct; tone?: ZHModuleTone }[];
}) {
  return (
    <TableCard>
      <ZHCardSection
        title={props.title}
        right={props.rightLabel ? <div className="zh-dash-muted">{props.rightLabel}</div> : undefined}
      >
        <ZHGrid cols={2}>
          {props.items.map((m) => (
            <div key={m.label} className="zh-dash-moduleCard">
              <div className="zh-dash-moduleHeader">
                <div className="zh-dash-moduleTitle">{m.label}</div>
                <div className="zh-dash-moduleMeta">{m.value}</div>
              </div>
              <div className="zh-dash-progress">
                <div
                  className={[
                    'zh-dash-progressFill',
                    `zh-dash-progressFill--pct${m.pct}`,
                    `zh-dash-progressFill--${m.tone ?? 'neutral'}`,
                  ].join(' ')}
                />
              </div>
              <div className="zh-dash-moduleFooter">{m.pct}% uso</div>
            </div>
          ))}
        </ZHGrid>
      </ZHCardSection>
    </TableCard>
  );
}

