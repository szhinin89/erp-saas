import {
  isActivePath,
  type NavItem,
  type TranslateFn,
} from "../../../../nav/navConfig";
import { FavoriteButton } from "./FavoriteButton";
import { LauncherIcon } from "./LauncherIcon";

type LauncherMenuItemProps = {
  item: NavItem;
  depth: number;
  currentPath: string;
  onNavigate: (to: string) => void;
  isFavorite: (id: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: TranslateFn;
};

/** Nivel 4 — Formulario final: fila hoja con icono, label, estado activo y favorito alineado a la derecha. */
export function LauncherMenuItem({
  item,
  depth,
  currentPath,
  onNavigate,
  isFavorite,
  toggleFavorite,
  t,
}: LauncherMenuItemProps) {
  const active = item.to ? isActivePath(item.to, currentPath) : false;

  // Cap visual a 2 niveles de indentación (Módulo > Categoría > Formulario = depth 0/1;
  // categorías anidadas adicionales reusan el indent de depth 2). Ver docs/ARCHITECTURE.md#app-launcher.
  return (
    <div
      className={`zh-launcher__item zh-launcher__item--depth-${Math.min(depth, 2)}${active ? " is-active" : ""}`}
    >
      {item.to ? (
        <a
          href={item.to}
          target="_blank"
          rel="noopener noreferrer"
          className="zh-launcher__itemLink"
          title={item.description}
          aria-label={
            item.description ? `${item.label}: ${item.description}` : item.label
          }
          aria-current={active ? "page" : undefined}
          onClick={() => onNavigate(item.to!)}
        >
          <LauncherIcon
            name="document"
            className="zh-launcher__itemIcon"
          />
          <span className="zh-launcher__itemContent">
            <span className="zh-launcher__itemLabel">{item.label}</span>
          </span>
        </a>
      ) : (
        <span
          className="zh-launcher__itemLink zh-launcher__itemLink--disabled"
          title={t("app.layout.menuMissingRoute")}
        >
          <LauncherIcon
            name="document"
            className="zh-launcher__itemIcon"
          />
          <span className="zh-launcher__itemContent">
            <span className="zh-launcher__itemLabel">{item.label}</span>
          </span>
        </span>
      )}
      <FavoriteButton
        item={item}
        isFavorite={isFavorite}
        toggleFavorite={toggleFavorite}
        t={t}
      />
    </div>
  );
}
