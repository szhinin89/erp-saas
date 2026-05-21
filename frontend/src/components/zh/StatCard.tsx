import type { ReactNode } from 'react';

type StatCardTone = 'primary' | 'secondary' | 'tertiary' | 'warning' | 'error';

/** KPI compacto reutilizable (envuelve clases pg-kpi existentes). */
export function StatCard(props: {
  label: string;
  value: string;
  icon?: string;
  tone?: StatCardTone;
  hint?: string;
  badge?: ReactNode;
}) {
  const { label, value, icon, tone = 'primary', hint, badge } = props;

  return (
    <div className="pg-kpi">
      <div className="pg-kpi-top">
        {icon ? (
          <div className={`pg-kpi-icon pg-kpi-icon--${tone}`}>
            <span className="material-symbols-outlined">{icon}</span>
          </div>
        ) : null}
        {badge}
      </div>
      <div className="pg-kpi-bottom">
        <p className="pg-kpi-label">{label}</p>
        <p className="pg-kpi-value">{value}</p>
        {hint ? <p className="subtle">{hint}</p> : null}
      </div>
    </div>
  );
}
