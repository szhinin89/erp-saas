/** Título de pantalla compacto, pegado al cabezal/menú (sin barra azul ZHFormHeader). */
export function ZHScreenHeading(props: {
  kicker?: string;
  title: string;
  subtitle?: string;
  right?: React.ReactNode;
}) {
  const { kicker, title, subtitle, right } = props;
  return (
    <header className="zh-screen-heading">
      {kicker ? <div className="zh-screen-heading-kicker">{kicker}</div> : null}
      <div className="zh-screen-heading-row">
        <div className="zh-screen-heading-main">
          <span className="zh-screen-heading-chip" aria-hidden="true" />
          <div className="zh-screen-heading-copy">
            <h1 className="zh-screen-heading-title">{title}</h1>
            {subtitle ? <p className="zh-screen-heading-sub">{subtitle}</p> : null}
          </div>
        </div>
        {right ? <div className="zh-screen-heading-right">{right}</div> : null}
      </div>
    </header>
  );
}
