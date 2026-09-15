import { useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useLocation } from "react-router-dom";
import { LauncherFavoritesSection } from "./launcher/LauncherFavoritesSection";
import { LauncherModuleGroup } from "./launcher/LauncherModuleGroup";
import { LauncherIcon } from "./launcher/LauncherIcon";
import type { MainMenuGroup } from "../../useAppLayoutNavigation";
import {
  collectActiveTrailGroupKeys,
  type NavItem,
  type TranslateFn,
} from "../../../nav/navConfig";
import "./launcher/launcher.css";

type ZHAppLauncherProps = {
  mainMenuGroups: MainMenuGroup[];
  loading: boolean;
  isFavorite: (id: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: TranslateFn;
};

function flattenFavorites(
  groups: MainMenuGroup[],
  isFavorite: (id: string) => boolean,
): NavItem[] {
  const out: NavItem[] = [];
  const visit = (items: NavItem[]) => {
    for (const it of items) {
      if (it.to && isFavorite(it.id)) out.push(it);
      if (it.children?.length) visit(it.children);
    }
  };
  for (const g of groups) visit(g.items);
  return out;
}

/**
 * App Launcher: punto único de navegación entre módulos.
 * Jerarquía: Favoritos (Nivel 0) → Módulos → Categorías → Formularios.
 */
export function ZHAppLauncher({
  mainMenuGroups,
  loading,
  isFavorite,
  toggleFavorite,
  t,
}: ZHAppLauncherProps) {
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const [expandedModuleId, setExpandedModuleId] = useState<string | null>(
    null,
  );
  // ZH-MENU-N-LEVEL-EXPAND-FIX-01: antes era un único `string | null` compartido por TODAS las
  // categorías del árbol (cualquier profundidad) — abrir una categoría hija forzaba a cerrar a su
  // padre (mismo estado, solo un id a la vez), así que un camino de 2+ categorías anidadas
  // (Configuración > Destinos contables) nunca podía quedar abierto completo: el cuerpo del padre
  // deja de renderizarse al cerrarse, así que el hijo tampoco se veía. Un Set permite que
  // cualquier cantidad de categorías, a cualquier profundidad, estén abiertas simultáneamente,
  // cada una independiente de sus hermanas/ancestros.
  const [expandedGroupIds, setExpandedGroupIds] = useState<Set<string>>(
    () => new Set(),
  );
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const panelRef = useRef<HTMLDivElement | null>(null);

  const favorites = useMemo(
    () => flattenFavorites(mainMenuGroups, isFavorite),
    [mainMenuGroups, isFavorite],
  );

  useEffect(() => {
    if (!open) return;

    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setOpen(false);
        triggerRef.current?.focus();
      }
    };
    const onDown = (e: MouseEvent | TouchEvent) => {
      const target = e.target;
      if (!(target instanceof Node)) return;
      if (triggerRef.current?.contains(target)) return;
      if (panelRef.current?.contains(target)) return;
      setOpen(false);
    };
    window.addEventListener("keydown", onKey);
    window.addEventListener(
      "pointerdown",
      onDown as unknown as EventListener,
      true,
    );
    return () => {
      window.removeEventListener("keydown", onKey);
      window.removeEventListener(
        "pointerdown",
        onDown as unknown as EventListener,
        true,
      );
    };
  }, [open]);

  useEffect(() => {
    setOpen(false);
  }, [location.pathname]);

  // Conserva la expansión automática del módulo de la ruta actual (accordion exclusivo entre
  // módulos) y además auto-expande el "active trail" completo de categorías anidadas dentro de
  // ese módulo (cualquier profundidad, ver collectActiveTrailGroupKeys) — así, al navegar
  // directo a /accounting/configuration/sales-collection-destinations, Configuración y Destinos
  // contables quedan ambas abiertas sin que el usuario tenga que expandirlas a mano.
  useEffect(() => {
    const activeModule = mainMenuGroups.find((group) => group.isActive);
    if (activeModule) {
      setExpandedModuleId(activeModule.id);
      setExpandedGroupIds(
        new Set(
          collectActiveTrailGroupKeys(
            activeModule.items,
            activeModule.id,
            location.pathname,
          ),
        ),
      );
    }
  }, [location.pathname, mainMenuGroups]);

  const closePanel = () => {
    setOpen(false);
    triggerRef.current?.focus();
  };

  const toggleModule = (moduleId: string) => {
    setExpandedModuleId((current) => {
      const next = current === moduleId ? null : moduleId;
      setExpandedGroupIds(new Set());
      return next;
    });
  };

  // Cada categoría se abre/cierra de forma independiente de sus hermanas y ancestros — no es un
  // accordion exclusivo, así que cualquier combinación de niveles puede quedar abierta a la vez.
  const toggleGroup = (groupId: string) => {
    setExpandedGroupIds((current) => {
      const next = new Set(current);
      if (next.has(groupId)) next.delete(groupId);
      else next.add(groupId);
      return next;
    });
  };

  const modulesContent = mainMenuGroups;

  return (
    <div className="zh-app-header__launcher">
      <button
        ref={triggerRef}
        type="button"
        className={`zh-app-header__launcherTrigger${open ? " is-open" : ""}`}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-label={t("app.header.appLauncher")}
        aria-busy={loading}
        onClick={() => setOpen((s) => !s)}
      >
        <LauncherIcon name="apps" className="zh-app-header__launcherTriggerIcon" />
        <span className="zh-app-header__launcherLabel" aria-hidden="true">
          {t("app.header.appLauncher")}
        </span>
      </button>

      {open
        ? createPortal(
            <div className="zh-app-header__launcherOverlay" onPointerDown={closePanel}>
            <div
              ref={panelRef}
              className="zh-app-header__launcherPanel"
              role="dialog"
              aria-modal="true"
              aria-label={t("app.header.appLauncher")}
              onPointerDown={(event) => event.stopPropagation()}
            >
              <div className="zh-app-header__launcherHeader">
                <span className="zh-app-header__launcherTitle">
                  {t("app.header.appLauncher")}
                </span>
                <button
                  type="button"
                  className="zh-app-header__launcherClose"
                  aria-label={t("app.layout.menuClose")}
                  onClick={closePanel}
                >
                  <LauncherIcon name="close" className="zh-app-header__launcherCloseIcon" />
                </button>
              </div>

              <div className="zh-app-header__launcherBody">
                <LauncherFavoritesSection
                  favorites={favorites}
                  currentPath={location.pathname}
                  onNavigate={closePanel}
                  isFavorite={isFavorite}
                  toggleFavorite={toggleFavorite}
                  t={t}
                />

                <div className="zh-launcher__modules">
                  {modulesContent.length > 0 ? (
                    modulesContent.map((g) => (
                      <LauncherModuleGroup
                        key={g.id}
                        group={g}
                        currentPath={location.pathname}
                        onNavigate={closePanel}
                        isFavorite={isFavorite}
                        toggleFavorite={toggleFavorite}
                        t={t}
                        expandedModuleId={expandedModuleId}
                        onToggleModule={toggleModule}
                        expandedGroupIds={expandedGroupIds}
                        onToggleGroup={toggleGroup}
                      />
                    ))
                  ) : loading ? (
                    <div
                      className="zh-launcher__skeleton"
                      aria-label={t("common.loading")}
                    >
                      {Array.from({ length: 6 }, (_, i) => (
                        <div key={i} className="zh-launcher__skeletonRow" />
                      ))}
                    </div>
                  ) : (
                    <div className="zh-app-header__launcherEmpty">
                      {t("app.header.appLauncher.empty")}
                    </div>
                  )}
              </div>
            </div>
            </div>
            </div>,
            document.body,
          )
        : null}
    </div>
  );
}
