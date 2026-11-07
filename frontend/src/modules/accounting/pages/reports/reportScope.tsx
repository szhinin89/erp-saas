import type { ReactNode } from "react";
import { useSessionStore } from "../../../../store/sessionStore";

/**
 * ACCOUNTING-REPORTS-UX-13 — alcance de la consulta YA aplicada (nunca el borrador de los
 * filtros) en una sola línea, usada como título del `ZHCard` de resultados (accounting.md §9:
 * fecha de corte y alcance de Company): empresa activa de la sesión (se omite si no está
 * disponible — no se inventa) · período o fecha de corte · filtros propios del reporte.
 */
export function ReportScopeTitle({ parts }: Readonly<{ parts: string[] }>) {
  const companyName = useSessionStore((s) => s.tenant?.displayName);
  return <>{[companyName, ...parts].filter(Boolean).join(" · ")}</>;
}

/** Par label/valor de `.zh-summary-grid`; `ZHMoneyValue` hereda la tipografía del valor. */
export function ReportSummaryItem({ label, children }: Readonly<{ label: string; children: ReactNode }>) {
  return (
    <div>
      <span className="zh-summary-grid__label">{label}</span>
      <span className="zh-summary-grid__value">{children}</span>
    </div>
  );
}
