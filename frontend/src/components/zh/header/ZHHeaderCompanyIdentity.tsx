import { RuntimeModeBadge } from "../../RuntimeModeBadge";

function initials(name: string) {
  const parts = name.trim().split(/\s+/).slice(0, 2);
  const init = parts.map((p) => p[0]?.toUpperCase() ?? "").join("");
  return init || "ZH";
}

/** Identidad operativa activa: logo/iniciales, empresa, sucursal y rol. */
export function ZHHeaderCompanyIdentity(props: {
  name: string;
  branchName?: string | null;
  role: string;
  logoSrc?: string | null;
}) {
  const { name, branchName, role, logoSrc } = props;
  const operationalContext = branchName ? `${name} / ${branchName}` : name;
  const title = branchName
    ? `Empresa: ${name}\nSucursal: ${branchName}\nRol: ${role}`
    : `Empresa: ${name}\nRol: ${role}`;

  return (
    <div className="zh-app-header__identity" title={title}>
      <div className="zh-app-header__logo" aria-hidden="true">
        {logoSrc ? (
          <img className="zh-app-header__logoImg" src={logoSrc} alt="" />
        ) : (
          <span className="zh-app-header__initials">{initials(name)}</span>
        )}
      </div>
      <div className="zh-app-header__identityText">
        <div className="zh-app-header__name">{operationalContext}</div>
        <div className="zh-app-header__context">
          <span className="zh-tenant-badge">{role}</span>
          <RuntimeModeBadge />
        </div>
      </div>
    </div>
  );
}
