import { useEffect, useRef } from "react";

export type ZHTab<TId extends string> = {
  id: TId;
  label: string;
  /** Etiqueta visual móvil; conserva label como nombre accesible completo. */
  mobileLabel?: string;
  icon?: string;
  /** true = se renderiza deshabilitado, aria-disabled, no dispara onChange. */
  disabled?: boolean;
  /**
   * true = tab que puede estar activo pero no navega: se ve como un tab normal
   * (incluso activo), pero el click no dispara onChange. Útil para representar
   * la vista actual fija (p.ej. "Nueva/Editar Factura") junto a tabs que sí
   * navegan (p.ej. "Historial").
   */
  inert?: boolean;
  /** Tooltip nativo de la pestaña (p. ej. el motivo por el que está deshabilitada). */
  title?: string;
  /** Contador opcional junto a la etiqueta (p. ej. "Roles 3"); no sustituye al nombre accesible. */
  count?: number;
};

type Props<TId extends string> = {
  tabs: ZHTab<TId>[];
  activeTab: TId;
  onChange: (id: TId) => void;
  ariaLabel?: string;
  /** true = las pestañas se reparten el ancho completo del contenedor en partes iguales. */
  fill?: boolean;
  /**
   * "auto" (default): con exactamente 3 pestañas aplica la distribución móvil de tres
   * columnas (UIUX-GLOBAL-TABS-DEFAULT-01); con otra cantidad, el render estándar.
   * "standard": exclusión explícita para barras funcionalmente distintas; siempre el
   * render estándar. Todo consumidor que la use documenta el motivo junto al prop.
   */
  variant?: "auto" | "standard";
  /**
   * Prefijo para el `id` de cada pestaña (`${idPrefix}-${tab.id}`). Junto con `panelIdPrefix`
   * permite enlazar el tablist con sus `role="tabpanel"` (`aria-labelledby`/`aria-controls`).
   */
  idPrefix?: string;
  /** Prefijo de los paneles controlados: `aria-controls="${panelIdPrefix}-${tab.id}"`. */
  panelIdPrefix?: string;
  /** Clase extra del contenedor `.prd-tabs` (solo ajustes de layout del consumidor). */
  className?: string;
};

/**
 * Barra de pestañas genérica (N pestañas dinámicas). Distinta de ConfigTabsLayout
 * (FROZEN), que resuelve el patrón fijo de 2 pestañas Lista/Editor de los módulos
 * de configuración — este componente es para pestañas de contenido arbitrarias
 * dentro de una misma página (formularios, vistas de detalle, etc.).
 */
export function ZHTabBar<TId extends string>({
  tabs,
  activeTab,
  onChange,
  ariaLabel,
  fill = false,
  variant = "auto",
  idPrefix,
  panelIdPrefix,
  className,
}: Props<TId>) {
  const responsiveThree = variant === "auto" && tabs.length === 3;
  const listRef = useRef<HTMLDivElement>(null);
  // ZH-DS-TABBAR-RESPONSIVE-01: con la barra desplazable (móvil) la pestaña activa y la enfocada
  // por teclado quedan completas a la vista. Solo mueve el scroll horizontal de la propia barra
  // (nunca la página); sin desbordamiento (escritorio) no hace nada.
  const reveal = (tab: HTMLElement | null | undefined) => {
    const list = listRef.current;
    if (!list || !tab || list.scrollWidth <= list.clientWidth) return;
    const start = tab.offsetLeft;
    const end = start + tab.offsetWidth;
    if (start < list.scrollLeft) list.scrollLeft = start;
    else if (end > list.scrollLeft + list.clientWidth) list.scrollLeft = end - list.clientWidth;
  };
  useEffect(() => {
    reveal(listRef.current?.querySelector<HTMLElement>('[aria-selected="true"]'));
  }, [activeTab]);
  return (
    <div
      ref={listRef}
      className={`prd-tabs${fill ? " prd-tabs--fill" : ""}${responsiveThree ? " prd-tabs--responsive-three" : ""}${className ? ` ${className}` : ""}`}
      role="tablist"
      aria-label={ariaLabel}
    >
      {tabs.map((tab) => (
        <button
          key={tab.id}
          id={idPrefix ? `${idPrefix}-${tab.id}` : undefined}
          type="button"
          role="tab"
          title={tab.title}
          aria-controls={panelIdPrefix ? `${panelIdPrefix}-${tab.id}` : undefined}
          aria-label={responsiveThree ? tab.label : undefined}
          aria-selected={activeTab === tab.id}
          aria-disabled={tab.disabled || undefined}
          disabled={tab.disabled}
          className={`prd-tab-btn${activeTab === tab.id ? " prd-tab-btn--active" : ""}`}
          onFocus={(e) => reveal(e.currentTarget)}
          onClick={() => {
            if (tab.disabled || tab.inert) return;
            onChange(tab.id);
          }}
        >
          {tab.icon && (
            <span className="material-symbols-outlined prd-tab-icon" aria-hidden={responsiveThree || undefined}>
              {tab.icon}
            </span>
          )}
          {responsiveThree ? (
            <>
              <span className={tab.mobileLabel ? "prd-tab-label--full" : undefined}>{tab.label}</span>
              {tab.mobileLabel && <span className="prd-tab-label--mobile" aria-hidden="true">{tab.mobileLabel}</span>}
            </>
          ) : tab.label}
          {tab.count !== undefined && <span className="prd-tab-count">{tab.count}</span>}
        </button>
      ))}
    </div>
  );
}
