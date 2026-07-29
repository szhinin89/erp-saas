export type ZHTab<TId extends string> = {
  id: TId;
  label: string;
  icon?: string;
};

type Props<TId extends string> = {
  tabs: ZHTab<TId>[];
  activeTab: TId;
  onChange: (id: TId) => void;
  ariaLabel?: string;
  /** true = las pestañas se reparten el ancho completo del contenedor en partes iguales. */
  fill?: boolean;
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
}: Props<TId>) {
  return (
    <div
      className={`prd-tabs${fill ? " prd-tabs--fill" : ""}`}
      role="tablist"
      aria-label={ariaLabel}
    >
      {tabs.map((tab) => (
        <button
          key={tab.id}
          type="button"
          role="tab"
          aria-selected={activeTab === tab.id}
          className={`prd-tab-btn${activeTab === tab.id ? " prd-tab-btn--active" : ""}`}
          onClick={() => onChange(tab.id)}
        >
          {tab.icon && (
            <span className="material-symbols-outlined prd-tab-icon">
              {tab.icon}
            </span>
          )}
          {tab.label}
        </button>
      ))}
    </div>
  );
}
