import { useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useLocation } from "react-router-dom";
import { LauncherFavoritesSection } from "./launcher/LauncherFavoritesSection";
import { LauncherSearchBar } from "./launcher/LauncherSearchBar";
import { LauncherModuleGroup } from "./launcher/LauncherModuleGroup";
import type { MainMenuGroup } from "../../useAppLayoutNavigation";
import type { NavItem, TranslateFn } from "../../../nav/navConfig";
import "./launcher/launcher.css";

type ZHAppLauncherProps = {
  mainMenuGroups: MainMenuGroup[];
  loading: boolean;
  isFavorite: (id: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: TranslateFn;
};

function matchesQuery(label: string, query: string): boolean {
  return label.toLowerCase().includes(query.trim().toLowerCase());
}

function filterItems(items: NavItem[], query: string): NavItem[] {
  if (!query.trim()) return items;
  const out: NavItem[] = [];
  for (const it of items) {
    const children = it.children ? filterItems(it.children, query) : undefined;
    if (matchesQuery(it.label, query) || (children && children.length > 0)) {
      out.push(children && children.length > 0 ? { ...it, children } : it);
    }
  }
  return out;
}

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
 * Jerarquía: Favoritos (Nivel 0) → Buscador (Nivel 1) → Módulos → Categorías → Formularios.
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
  const [query, setQuery] = useState("");
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const panelRef = useRef<HTMLDivElement | null>(null);
  const searchRef = useRef<HTMLInputElement | null>(null);
  const [panelPos, setPanelPos] = useState<{
    top: number;
    left: number;
  } | null>(null);

  const favorites = useMemo(
    () => flattenFavorites(mainMenuGroups, isFavorite),
    [mainMenuGroups, isFavorite],
  );

  useEffect(() => {
    if (!open) return;

    if (window.innerWidth >= 768) {
      const trigger = triggerRef.current;
      if (trigger) {
        const r = trigger.getBoundingClientRect();
        setPanelPos({
          top: Math.round(r.bottom + 8),
          left: Math.round(r.left),
        });
      }
    } else {
      setPanelPos(null);
    }

    const focusTimer = setTimeout(() => searchRef.current?.focus(), 0);

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
      clearTimeout(focusTimer);
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
    setQuery("");
  }, [location.pathname]);

  const closePanel = () => {
    setOpen(false);
    triggerRef.current?.focus();
  };

  const isSearching = query.trim() !== "";

  const modulesContent = mainMenuGroups
    .map((g) => ({ ...g, items: filterItems(g.items, query) }))
    .filter((g) => g.items.length > 0);

  const favoritesContent = filterItems(favorites, query);

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
        <span className="material-symbols-outlined" aria-hidden="true">
          apps
        </span>
        <span className="zh-app-header__launcherLabel" aria-hidden="true">
          {t("app.header.appLauncher")}
        </span>
      </button>

      {open
        ? createPortal(
            <div
              ref={panelRef}
              className="zh-app-header__launcherPanel"
              role="dialog"
              aria-modal="true"
              aria-label={t("app.header.appLauncher")}
              style={
                panelPos
                  ? {
                      position: "fixed",
                      top: panelPos.top,
                      left: panelPos.left,
                    }
                  : undefined
              }
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
                  <span
                    className="material-symbols-outlined"
                    aria-hidden="true"
                  >
                    close
                  </span>
                </button>
              </div>

              <div className="zh-app-header__launcherBody">
                <LauncherSearchBar
                  value={query}
                  onChange={setQuery}
                  inputRef={searchRef}
                  t={t}
                />

                <LauncherFavoritesSection
                  favorites={favoritesContent}
                  currentPath={location.pathname}
                  onNavigate={closePanel}
                  isFavorite={isFavorite}
                  toggleFavorite={toggleFavorite}
                  t={t}
                />

                <div className="zh-launcher__modules">
                  {modulesContent.length > 0 ? (
                    <>
                      {isSearching ? (
                        <div className="zh-launcher__searchResultCount">
                          {modulesContent.reduce((acc, g) => {
                            const countItems = (
                              items: typeof g.items,
                            ): number =>
                              items.reduce(
                                (n, it) =>
                                  n +
                                  (it.children?.length
                                    ? countItems(it.children)
                                    : 1),
                                0,
                              );
                            return acc + countItems(g.items);
                          }, 0)}{" "}
                          {t("common.results")}
                        </div>
                      ) : null}
                      {modulesContent.map((g) => (
                        <LauncherModuleGroup
                          key={g.id}
                          group={g}
                          currentPath={location.pathname}
                          onNavigate={closePanel}
                          isFavorite={isFavorite}
                          toggleFavorite={toggleFavorite}
                          t={t}
                          forceExpanded={isSearching}
                        />
                      ))}
                    </>
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
            </div>,
            document.body,
          )
        : null}
    </div>
  );
}
