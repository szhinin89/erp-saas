/**
 * ReportPageTemplate — plantilla reutilizable para cualquier reporte.
 *
 * Composición recomendada:
 *   <ReportPage breadcrumb={['ERP','REPORTES']} title="Reporte de Ventas" subtitle="…" actions={…}>
 *     <div className="pg-kpis">
 *       <ReportKpiCard icon="payments" tone="primary" badge="+12.4%" badgeTone="success" label="Total Ventas" value="$248,500" />
 *       …
 *     </div>
 *     <ReportFiltersBar onClear={…} onApply={…}>
 *       <ReportFilterField label="Rango de Fechas" icon="calendar_today">
 *         <select className="zh-input">…</select>
 *       </ReportFilterField>
 *       …
 *     </ReportFiltersBar>
 *     <ReportChartPanel title="Tendencia de Ventas Diarias" period={period} onPeriod={setPeriod}>
 *       <svg …>…</svg>
 *       <ReportChartDates labels={['Oct 01','Oct 07',…]} />
 *     </ReportChartPanel>
 *     <ReportTablePanel
 *       searchPlaceholder="Filtrar ventas…"
 *       total={1429} showing="1-5"
 *       footerLeft="Últimos 30 días"
 *       footerRight="Última actualización: hace 5 min"
 *     >
 *       <table className="rpt-table">…</table>
 *     </ReportTablePanel>
 *   </ReportPage>
 */

import type { ReactNode } from "react";
import { ZHBtn } from "./zh/ZHForm";
import { Badge, type BadgeVariant } from "./PageShell";
import "./ReportPageTemplate.css";

/* ── Types ─────────────────────────────────────────────────── */

export type RptKpiTone =
  "primary" | "secondary" | "tertiary" | "success" | "warning" | "error";
export type RptBadgeTone = "success" | "warning" | "error" | "neutral";
export type RptPeriod = "day" | "week" | "month";
export type RptStatusTone =
  "success" | "info" | "warning" | "error" | "neutral";

/* ── ReportPage ─────────────────────────────────────────────── */

export function ReportPage({
  breadcrumb = [],
  title,
  subtitle,
  actions,
  children,
}: {
  breadcrumb?: string[];
  title: string;
  subtitle?: string;
  actions?: ReactNode;
  children?: ReactNode;
}) {
  return (
    <div className="pg-page">
      <div className="pg-header-row">
        <div className="pg-header-left">
          {breadcrumb.length > 0 && (
            <nav className="pg-breadcrumb" aria-label="Ruta de navegación">
              {breadcrumb.map((item, i) => (
                <span key={i} className="pg-breadcrumb-item">
                  {i > 0 && (
                    <span
                      className="material-symbols-outlined pg-breadcrumb-sep"
                      aria-hidden
                    >
                      chevron_right
                    </span>
                  )}
                  {item}
                </span>
              ))}
            </nav>
          )}
          <h1 className="pg-title">{title}</h1>
          {subtitle && <p className="pg-subtitle">{subtitle}</p>}
        </div>
        {actions && <div className="pg-header-right">{actions}</div>}
      </div>

      {children}
    </div>
  );
}

/* ── ReportKpiCard ──────────────────────────────────────────── */

export function ReportKpiCard({
  icon,
  tone = "primary",
  badge,
  badgeTone = "neutral",
  label,
  value,
  unit,
  sub,
}: {
  icon: string;
  tone?: RptKpiTone;
  badge?: string;
  badgeTone?: RptBadgeTone;
  label: string;
  value: string;
  unit?: string;
  sub?: string;
}) {
  return (
    <div className="pg-kpi">
      <div className="pg-kpi-top">
        <div className={`pg-kpi-icon pg-kpi-icon--${tone}`}>
          <span className="material-symbols-outlined">{icon}</span>
        </div>
        {badge && (
          <span className={`pg-kpi-badge pg-kpi-badge--${badgeTone}`}>
            {badge}
          </span>
        )}
      </div>
      <div className="pg-kpi-bottom">
        <p className="pg-kpi-label">{label}</p>
        <p className="pg-kpi-value">
          {value}
          {unit && <span className="pg-kpi-unit">{unit}</span>}
        </p>
        {sub && <p className="rpt-kpi-sub">{sub}</p>}
      </div>
    </div>
  );
}

/* ── ReportFiltersBar ───────────────────────────────────────── */

export function ReportFiltersBar({
  onClear,
  onApply,
  clearLabel = "Limpiar",
  applyLabel = "Aplicar Filtros",
  children,
}: {
  onClear?: () => void;
  onApply?: () => void;
  clearLabel?: string;
  applyLabel?: string;
  children?: ReactNode;
}) {
  return (
    <div className="rpt-filters">
      <div className="rpt-filters-fields">{children}</div>
      <div className="rpt-filters-actions">
        <ZHBtn type="button" variant="ghost" size="md" onClick={onClear}>
          {clearLabel}
        </ZHBtn>
        <ZHBtn type="button" variant="primary" size="md" onClick={onApply}>
          {applyLabel}
        </ZHBtn>
      </div>
    </div>
  );
}

/* ── ReportFilterField ──────────────────────────────────────── */

export function ReportFilterField({
  label,
  icon,
  children,
}: {
  label: string;
  icon?: string;
  children: ReactNode;
}) {
  return (
    <div className="rpt-filter-field">
      <label className="rpt-filter-label">{label}</label>
      {icon ? (
        <div className="rpt-filter-icon-wrap">
          <span className="material-symbols-outlined rpt-filter-icon">
            {icon}
          </span>
          {children}
        </div>
      ) : (
        children
      )}
    </div>
  );
}

/* ── ReportChartPanel ───────────────────────────────────────── */

export function ReportChartPanel({
  title,
  period,
  onPeriod,
  periodLabels = { day: "Día", week: "Semana", month: "Mes" },
  children,
}: {
  title: string;
  period?: RptPeriod;
  onPeriod?: (p: RptPeriod) => void;
  periodLabels?: { day: string; week: string; month: string };
  children?: ReactNode;
}) {
  const periods: RptPeriod[] = ["day", "week", "month"];
  return (
    <div className="pg-section rpt-chart-panel">
      <div className="pg-section-header">
        <h3 className="pg-section-label">{title}</h3>
        {period !== undefined && onPeriod && (
          <div
            className="rpt-period-toggle"
            role="group"
            aria-label="Período del gráfico"
          >
            {periods.map((p) => (
              <button
                key={p}
                type="button"
                className={`rpt-period-btn${period === p ? " is-active" : ""}`}
                onClick={() => onPeriod(p)}
              >
                {periodLabels[p]}
              </button>
            ))}
          </div>
        )}
      </div>
      <div className="rpt-chart-area">{children}</div>
    </div>
  );
}

/* ── ReportChartDates ───────────────────────────────────────── */

export function ReportChartDates({ labels }: { labels: string[] }) {
  return (
    <div className="rpt-chart-dates" aria-hidden>
      {labels.map((l, i) => (
        <span key={i}>{l}</span>
      ))}
    </div>
  );
}

/* ── ReportChartTooltip ─────────────────────────────────────── */

export function ReportChartTooltip({
  date,
  rows,
}: {
  date: string;
  rows: {
    label: string;
    value: string;
    tone?: "success" | "error" | "default";
  }[];
}) {
  return (
    <div className="rpt-chart-tooltip" role="tooltip">
      <p className="rpt-chart-tooltip-date">{date}</p>
      {rows.map((r, i) => (
        <p key={i} className="rpt-chart-tooltip-row">
          <span>{r.label}</span>
          <span
            className={r.tone ? `rpt-chart-tooltip-val--${r.tone}` : undefined}
          >
            {r.value}
          </span>
        </p>
      ))}
    </div>
  );
}

/* ── ReportTablePanel ───────────────────────────────────────── */

export function ReportTablePanel({
  searchPlaceholder = "Buscar…",
  searchValue,
  onSearch,
  total,
  showing,
  onPrev,
  onNext,
  hasPrev = false,
  hasNext = true,
  footerLeft,
  footerRight,
  children,
}: {
  searchPlaceholder?: string;
  searchValue?: string;
  onSearch?: (v: string) => void;
  total?: number;
  showing?: string;
  onPrev?: () => void;
  onNext?: () => void;
  hasPrev?: boolean;
  hasNext?: boolean;
  footerLeft?: string;
  footerRight?: string;
  children?: ReactNode;
}) {
  return (
    <div className="pg-section">
      <div className="pg-table-controls">
        <div className="pg-table-controls-left">
          <div className="pg-search">
            <span className="material-symbols-outlined">search</span>
            <input
              className="zh-input"
              type="search"
              placeholder={searchPlaceholder}
              value={searchValue ?? ""}
              onChange={(e) => onSearch?.(e.target.value)}
              aria-label={searchPlaceholder}
            />
          </div>
          <ZHBtn
            type="button"
            variant="ghost"
            size="md"
            className="rpt-filter-btn"
            aria-label="Filtros"
          >
            <span className="material-symbols-outlined">filter_list</span>
            <span className="rpt-filter-btn-label">Filtros</span>
          </ZHBtn>
        </div>
        <div className="pg-table-controls-right">
          {showing && total !== undefined && (
            <span>
              Mostrando {showing} de {total}
            </span>
          )}
          <div className="pg-pagination-controls">
            <button
              type="button"
              className="pg-pagination-btn"
              onClick={onPrev}
              disabled={!hasPrev}
              aria-label="Página anterior"
            >
              <span className="material-symbols-outlined">chevron_left</span>
            </button>
            <button
              type="button"
              className="pg-pagination-btn"
              onClick={onNext}
              disabled={!hasNext}
              aria-label="Página siguiente"
            >
              <span className="material-symbols-outlined">chevron_right</span>
            </button>
          </div>
        </div>
      </div>

      <div className="rpt-table-wrap">{children}</div>

      {(footerLeft || footerRight) && (
        <div className="pg-table-footer">
          {footerLeft && <p className="rpt-footer-note">{footerLeft}</p>}
          {footerRight && <p className="pg-table-timestamp">{footerRight}</p>}
        </div>
      )}
    </div>
  );
}

/* ── ReportTable ────────────────────────────────────────────── */

export function ReportTable({ children }: { children: ReactNode }) {
  return <table className="table">{children}</table>;
}

export function ReportTableHead({ children }: { children: ReactNode }) {
  return (
    <thead>
      <tr>{children}</tr>
    </thead>
  );
}

export function ReportTh({
  children,
  align = "left",
}: {
  children?: ReactNode;
  align?: "left" | "right" | "center";
}) {
  return (
    <th className={align !== "left" ? `rpt-th--${align}` : undefined}>
      {children}
    </th>
  );
}

export function ReportTd({
  children,
  align = "left",
  className,
}: {
  children?: ReactNode;
  align?: "left" | "right" | "center";
  className?: string;
}) {
  return (
    <td
      className={
        [align !== "left" ? `rpt-td--${align}` : "", className]
          .filter(Boolean)
          .join(" ") || undefined
      }
    >
      {children}
    </td>
  );
}

/* ── ReportClientAvatar ─────────────────────────────────────── */

export function ReportClientAvatar({
  name,
  initials,
}: {
  name: string;
  initials?: string;
}) {
  const auto = (name ?? "")
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((w) => w[0]?.toUpperCase() ?? "")
    .join("");
  return (
    <div className="rpt-client-row">
      <div className="zh-avatar zh-avatar--square" aria-hidden>
        {initials ?? auto}
      </div>
      <span className="rpt-client-name">{name}</span>
    </div>
  );
}

/* ── ReportRowId ────────────────────────────────────────────── */

export function ReportRowId({ id }: { id: string }) {
  return <span className="rpt-row-id">{id}</span>;
}

/* ── ReportStatusBadge ──────────────────────────────────────── */

export function ReportStatusBadge({
  label,
  tone,
}: {
  label: string;
  tone: RptStatusTone;
}) {
  const variantMap: Record<RptStatusTone, BadgeVariant> = {
    success: "green",
    info: "blue",
    warning: "orange",
    error: "red",
    neutral: "gray",
  };
  return <Badge label={label} variant={variantMap[tone]} size="md" />;
}

/* ── ReportRowActions ───────────────────────────────────────── */

export function ReportRowActions({ onClick }: { onClick?: () => void }) {
  return (
    <button
      type="button"
      className="rpt-row-actions-btn"
      onClick={onClick}
      aria-label="Más acciones"
    >
      <span className="material-symbols-outlined">more_vert</span>
    </button>
  );
}
