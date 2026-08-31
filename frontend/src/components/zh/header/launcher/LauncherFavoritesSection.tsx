import { useEffect, useState } from "react";
import type { NavItem, TranslateFn } from "../../../../nav/navConfig";
import { LauncherMenuItem } from "./LauncherMenuItem";
import { LauncherIcon } from "./LauncherIcon";

const MAX_VISIBLE = 6;
const EXPANDED_STORAGE_KEY = "zh.ui.launcher.favorites.expanded";

function loadInitialExpanded(): boolean {
  try {
    return localStorage.getItem(EXPANDED_STORAGE_KEY) === "true";
  } catch {
    return false; // Primera carga: colapsado
  }
}

type LauncherFavoritesSectionProps = {
  favorites: NavItem[];
  currentPath: string;
  onNavigate: (to: string) => void;
  isFavorite: (id: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: TranslateFn;
};

/**
 * Nivel 0 — Favoritos: colapsable (persistido en localStorage), counter visible en header.
 * Primera carga: colapsado. Cargas siguientes: restaura última preferencia.
 */
export function LauncherFavoritesSection({
  favorites,
  currentPath,
  onNavigate,
  isFavorite,
  toggleFavorite,
  t,
}: LauncherFavoritesSectionProps) {
  const [isExpanded, setIsExpanded] = useState(loadInitialExpanded);
  const [showAll, setShowAll] = useState(false);

  useEffect(() => {
    try {
      localStorage.setItem(EXPANDED_STORAGE_KEY, isExpanded ? "true" : "false");
    } catch {
      // ignore
    }
  }, [isExpanded]);

  const hasOverflow = favorites.length > MAX_VISIBLE;
  const visible = showAll ? favorites : favorites.slice(0, MAX_VISIBLE);
  const count = favorites.length;

  return (
    <div className="zh-launcher__favorites">
      <button
        type="button"
        className="zh-launcher__favoritesHeader"
        aria-expanded={isExpanded}
        onClick={() => setIsExpanded((v) => !v)}
      >
        <LauncherIcon name="star" className="zh-launcher__favoritesIcon" />
        <span className="zh-launcher__favoritesTitle">
          {t("app.header.appLauncher.myShortcuts")}
        </span>
        {count > 0 ? (
          <span className="zh-launcher__favoritesCount">{count}</span>
        ) : null}
        <LauncherIcon name="chevron" className="zh-launcher__favoritesCaret" />
      </button>

      <div
        className={`zh-launcher__favoritesContent${isExpanded ? " is-expanded" : ""}${isExpanded && showAll ? " is-full" : ""}`}
      >
        {favorites.length === 0 ? (
          <div className="zh-launcher__favoritesEmpty">
            <p className="zh-launcher__favoritesEmptyTitle">
              {t("app.favorites.empty")}
            </p>
            <p className="zh-launcher__favoritesEmptyHint">
              {t("app.favorites.emptyHint")}
            </p>
          </div>
        ) : (
          <>
            <div className="zh-launcher__favoritesBody">
              {visible.map((item, idx) => (
                <LauncherMenuItem
                  key={item.id ?? `fav-${idx}-${item.to}`}
                  item={item}
                  depth={0}
                  currentPath={currentPath}
                  onNavigate={onNavigate}
                  isFavorite={isFavorite}
                  toggleFavorite={toggleFavorite}
                  t={t}
                />
              ))}
            </div>
            {hasOverflow ? (
              <button
                type="button"
                className="zh-launcher__favoritesToggle"
                onClick={() => setShowAll((v) => !v)}
              >
                {showAll
                  ? t("app.header.appLauncher.showLess")
                  : t("app.header.appLauncher.manageFavorites")}
              </button>
            ) : null}
          </>
        )}
      </div>
    </div>
  );
}
