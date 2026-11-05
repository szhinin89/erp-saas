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
};

type Props<TId extends string> = {
  tabs: ZHTab<TId>[];
  activeTab: TId;
  onChange: (id: TId) => void;
  ariaLabel?: string;
  /** true = las pestañas se reparten el ancho completo del contenedor en partes iguales. */
  fill?: boolean;
  /** Opt-in: tres columnas móviles únicamente cuando tabs.length === 3. */
  variant?: "default" | "responsive-three";
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
  variant = "default",
}: Props<TId>) {
  const responsiveThree = variant === "responsive-three" && tabs.length === 3;
  return (
    <div
      className={`prd-tabs${fill ? " prd-tabs--fill" : ""}${responsiveThree ? " prd-tabs--responsive-three" : ""}`}
      role="tablist"
      aria-label={ariaLabel}
    >
      {tabs.map((tab) => (
        <button
          key={tab.id}
          type="button"
          role="tab"
          aria-label={responsiveThree ? tab.label : undefined}
          aria-selected={activeTab === tab.id}
          aria-disabled={tab.disabled || undefined}
          disabled={tab.disabled}
          className={`prd-tab-btn${activeTab === tab.id ? " prd-tab-btn--active" : ""}`}
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
        </button>
      ))}
    </div>
  );
}
