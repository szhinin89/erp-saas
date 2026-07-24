/** Botón de acción del header (icono Material Symbols). Soporta estado "próximamente" (deshabilitado) y un indicador de punto (p. ej. notificaciones pendientes). */
export function ZHHeaderActionButton(props: {
  icon: string;
  label: string;
  comingSoon?: boolean;
  onClick?: () => void;
  active?: boolean;
  dot?: boolean;
}) {
  const { icon, label, comingSoon, onClick, active, dot } = props;

  return (
    <button
      type="button"
      className={`zh-app-header__actionBtn${active ? ' is-active' : ''}`}
      aria-label={label}
      title={comingSoon ? label : undefined}
      disabled={comingSoon}
      onClick={onClick}
    >
      <span className="material-symbols-outlined" aria-hidden="true">{icon}</span>
      {dot ? <span className="zh-app-header__actionDot" aria-hidden="true" /> : null}
    </button>
  );
}
