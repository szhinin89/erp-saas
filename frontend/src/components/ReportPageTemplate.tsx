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
import { useDocumentTitle } from "../hooks/useDocumentTitle";
import { ZHBtn } from "./zh/ZHForm";
import { ZHIconButton } from "./zh/ZHIconButton";
import { Badge, type BadgeVariant } from "./PageShell";
import "./ReportPageTemplate.css";

/* ── Types ─────────────────────────────────────────────────── */

export type RptKpiTone =
  | "primary"
  | "secondary"
  | "tertiary"
  | "success"
  | "warning"
  | "error"
  | "neutral"
  | "info";
export type RptBadgeTone = "success" | "warning" | "error" | "neutral";
export type RptKpiTrendTone = "success" | "warning" | "neutral";
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
  useDocumentTitle(title);

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
  layout = "vertical",
  badge,
  badgeTone = "neutral",
  trend,
  label,
  value,
  valueTone = "default",
  unit,
  sub,
  onClick,
  active,
}: {
  icon?: string;
  tone?: RptKpiTone;
  /** `horizontal` = ícono a la izquierda, texto a la derecha (`.pg-kpi--h`). */
  layout?: "vertical" | "horizontal";
  /** String → se envuelve en <Badge>; ReactNode → se usa tal cual (p.ej. un <Badge> ya construido por el caller). */
  badge?: ReactNode;
  badgeTone?: BadgeVariant;
  trend?: { icon: string; label: string; tone: RptKpiTrendTone };
  label: string;
  value: string;
  /** Color semántico del valor numérico (`.pg-kpi-value--success`/`--danger`).
   * `default` mantiene el color neutro actual — no confundir con `trend.tone`
   * (esa es la etiqueta de tendencia debajo del valor, no el número en sí). */
  valueTone?: "default" | "success" | "danger";
  unit?: string;
  /** String → se envuelve en <p className="rpt-kpi-sub"> (texto verde/success); ReactNode → se usa tal cual (p.ej. un <p className="subtle"> para texto neutro). */
  sub?: ReactNode;
  /** Si se define, la tarjeta completa (ícono + badge + label + value + trend) se
   * renderiza como <button type="button"> clicable en vez de <div> estático. */
  onClick?: () => void;
  /** Solo tiene efecto junto a `onClick` — aplica `.pg-kpi--active` y `aria-pressed`
   * para reflejar si esta tarjeta es el filtro actualmente seleccionado. */
  active?: boolean;
}) {
  const badgeNode =
    typeof badge === "string" ? (
      <Badge label={badge} variant={badgeTone} />
    ) : (
      badge
    );

  const subNode =
    typeof sub === "string" ? <p className="rpt-kpi-sub">{sub}</p> : sub;

  const iconNode = icon && (
    <div className={`pg-kpi-icon pg-kpi-icon--${tone}`}>
      <span className="material-symbols-outlined">{icon}</span>
    </div>
  );

  const bottomNode = (
    <div className="pg-kpi-bottom">
      <p className="pg-kpi-label">{label}</p>
      <p
        className={`pg-kpi-value${valueTone !== "default" ? ` pg-kpi-value--${valueTone}` : ""}`}
      >
        {value}
        {unit && <span className="pg-kpi-unit">{unit}</span>}
      </p>
      {trend && (
        <div className={`pg-kpi-trend pg-kpi-trend--${trend.tone}`}>
          <span className="material-symbols-outlined">{trend.icon}</span>
          <span>{trend.label}</span>
        </div>
      )}
      {subNode}
    </div>
  );

  const rootClassName = [
    "pg-kpi",
    layout === "horizontal" ? "pg-kpi--h" : "",
    onClick ? "pg-kpi--interactive" : "",
    onClick && active ? "pg-kpi--active" : "",
  ]
    .filter(Boolean)
    .join(" ");

  const content =
    layout === "horizontal" ? (
      <>
        {iconNode}
        {bottomNode}
      </>
    ) : (
      <>
        {(iconNode || badgeNode) && (
          <div className="pg-kpi-top">
            {iconNode}
            {badgeNode}
          </div>
        )}
        {bottomNode}
      </>
    );

  if (onClick) {
    return (
      <button
        type="button"
        className={rootClassName}
        onClick={onClick}
        aria-pressed={active}
      >
        {content}
      </button>
    );
  }

  return <div className={rootClassName}>{content}</div>;
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

      <div className="table-scroll">{children}</div>

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
    <ZHIconButton
      icon="more_vert"
      variant="ghost"
      shape="circle"
      title="Más acciones"
      onClick={onClick}
    />
  );
}
