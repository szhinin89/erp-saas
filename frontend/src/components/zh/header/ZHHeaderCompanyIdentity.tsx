import { RuntimeModeBadge } from '../../RuntimeModeBadge';

function initials(name: string) {
  const parts = name.trim().split(/\s+/).slice(0, 2);
  const init = parts.map((p) => p[0]?.toUpperCase() ?? '').join('');
  return init || 'ZH';
}

/** Identidad de empresa activa: logo/iniciales, nombre, rol y contexto de sesión (Company/SuperAdmin). */
export function ZHHeaderCompanyIdentity(props: {
  name: string;
  role: string;
  logoSrc?: string | null;
}) {
  const { name, role, logoSrc } = props;

  return (
    <div className="zh-app-header__identity">
      <div className="zh-app-header__logo" aria-hidden="true">
        {logoSrc ? (
          <img className="zh-app-header__logoImg" src={logoSrc} alt="" />
        ) : (
          <span className="zh-app-header__initials">{initials(name)}</span>
        )}
      </div>
      <div className="zh-app-header__name">{name}</div>
      <div className="zh-app-header__context">
        <span className="zh-tenant-badge">{role}</span>
        <RuntimeModeBadge />
      </div>
    </div>
  );
}
