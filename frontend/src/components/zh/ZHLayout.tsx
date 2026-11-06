/** `compact` (default) o `stacked`: diseño apilado anterior, solo como excepción justificada. */
export type ZHScreenHeadingVariant = "compact" | "stacked";

/** Título de pantalla compacto, pegado al cabezal/menú (sin barra azul ZHFormHeader). */
export function ZHScreenHeading(props: {
  /** Se conserva temporalmente en la interfaz pública para no modificar consumidores;
   * no se renderiza (UIUX-GLOBAL-HEADER-DEFAULT-01). */
  kicker?: string;
  title: string;
  subtitle?: string;
  right?: React.ReactNode;
  /** Compact (default): título y descripción alineados, apilados hasta 768 px. */
  variant?: ZHScreenHeadingVariant;
}) {
  const { title, subtitle, right, variant = "compact" } = props;
  return (
    <header
      className={variant === "compact"
        ? "zh-screen-heading zh-screen-heading--compact"
        : "zh-screen-heading"}
    >
      <div className="zh-screen-heading-row">
        <div className="zh-screen-heading-main">
          <span className="zh-screen-heading-chip" aria-hidden="true" />
          <div className="zh-screen-heading-copy">
            <h1 className="zh-screen-heading-title">{title}</h1>
            {subtitle ? (
              <p className="zh-screen-heading-sub">{subtitle}</p>
            ) : null}
          </div>
        </div>
        {right ? <div className="zh-screen-heading-right">{right}</div> : null}
      </div>
    </header>
  );
}
