import type { ReactNode } from "react";
import { NavLink } from "react-router-dom";
import { useAuthStore } from "../../store/authStore";
import { useActiveBranchStore } from "../../store/activeBranchStore";
import { useSessionStore } from "../../store/sessionStore";
import { useI18n } from "../../i18n/i18n";
import { useAuthenticatedImage } from "../../hooks/useAuthenticatedImage";
import type { MainMenuGroup } from "../useAppLayoutNavigation";
import type { NavItem } from "../../nav/navConfig";
import { ZHHeaderCompanyIdentity } from "./header/ZHHeaderCompanyIdentity";
import { ZHAppLauncher } from "./header/ZHAppLauncher";
import { ZHHeaderActionButton } from "./header/ZHHeaderActionButton";
import { ZHHeaderUserMenu } from "./header/ZHHeaderUserMenu";
import "./ZHAppTenantHeader.css";

export type ZHAppTenantHeaderNavigation = {
  mainMenuGroups: MainMenuGroup[];
  sessionMenuResolved: boolean;
  /** Identidad por `NavItem.id` (estable ante cambios de ruta/idioma/label). */
  isFavorite: (id: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
};

export function ZHAppTenantHeader(props: {
  navigation?: ZHAppTenantHeaderNavigation;
  rightExtra?: ReactNode;
  onLogout?: () => void;
}) {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);
  const tenant = useSessionStore((s) => s.tenant);
  const branch = useActiveBranchStore((s) => s.branch);
  const logoSrc = useAuthenticatedImage(tenant?.logo?.url);
  const { navigation, rightExtra, onLogout } = props;

  if (!user) return null;

  const tenantName = tenant?.displayName || t("app.tenant.defaultName");
  const branchName = user.companyId
    ? (branch?.name ?? t("app.header.branch.select", "Seleccionar sucursal"))
    : null;

  return (
    <div className="zh-tenant-header zh-app-tenantHeader">
      <div className="zh-app-header__bar">
        <div className="zh-app-header__left">
          {navigation ? (
            <div className="zh-app-header__nav">
              <ZHAppLauncher
                mainMenuGroups={navigation.mainMenuGroups}
                loading={!navigation.sessionMenuResolved}
                isFavorite={navigation.isFavorite}
                toggleFavorite={navigation.toggleFavorite}
                t={t}
              />
              <NavLink
                to="/dashboard"
                end
                className={({ isActive }) =>
                  `zh-app-header__launcherTrigger zh-app-header__home${isActive ? " is-active" : ""}`
                }
                title={t("app.nav.group.home")}
                aria-label={t("app.nav.group.home")}
              >
                <svg
                  width="20"
                  height="20"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="1.8"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  aria-hidden="true"
                >
                  <path d="m3 10 9-7 9 7v10a1 1 0 0 1-1 1h-5v-7H9v7H4a1 1 0 0 1-1-1Z" />
                </svg>
              </NavLink>
            </div>
          ) : null}

          <ZHHeaderCompanyIdentity
            name={tenantName || user.fullName || "ZH"}
            branchName={branchName}
            role={user.role}
            logoSrc={logoSrc}
          />
        </div>

        {/* Espacio reservado para futuras capacidades (breadcrumbs, buscador global, contexto del módulo). */}
        <div className="zh-app-header__center" aria-hidden="true" />

        <div
          className="zh-app-header__actions"
          aria-label={t("app.header.actions")}
        >
          <ZHHeaderActionButton
            icon="search"
            label={t("app.header.search.comingSoon")}
            comingSoon
          />
          <ZHHeaderActionButton
            icon="notifications"
            label={t("app.header.notifications.comingSoon")}
            comingSoon
            dot
          />
          {rightExtra ? (
            <>
              <span className="zh-app-header__divider" aria-hidden="true" />
              <div className="zh-app-tenantRightExtra">{rightExtra}</div>
            </>
          ) : null}
          <span className="zh-app-header__divider" aria-hidden="true" />
          <ZHHeaderUserMenu
            fullName={user.fullName}
            username={user.username}
            email={user.email}
            onLogout={onLogout}
            t={t}
          />
        </div>
      </div>
    </div>
  );
}
