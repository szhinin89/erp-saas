import { useNavigate } from "react-router-dom";
import {
  isActivePath,
  type NavItem,
  type TranslateFn,
} from "../../../../nav/navConfig";
import { FavoriteButton } from "./FavoriteButton";

function ItemIcon({ icon }: { icon: string }) {
  const isMaterialSymbol = /^[a-z][a-z0-9_]*$/.test(icon);
  return isMaterialSymbol ? (
    <span
      className="zh-launcher__itemIcon material-symbols-outlined"
      aria-hidden="true"
    >
      {icon}
    </span>
  ) : (
    <span className="zh-launcher__itemIcon" aria-hidden="true">
      {icon}
    </span>
  );
}

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
  const navigate = useNavigate();
  const active = item.to ? isActivePath(item.to, currentPath) : false;

  // Cap visual a 2 niveles de indentación (Módulo > Categoría > Formulario = depth 0/1;
  // categorías anidadas adicionales reusan el indent de depth 2). Ver docs/ARCHITECTURE.md#app-launcher.
  return (
    <div
      className={`zh-launcher__item zh-launcher__item--depth-${Math.min(depth, 2)}${active ? " is-active" : ""}`}
    >
      {item.to ? (
        <button
          type="button"
          className="zh-launcher__itemLink"
          onClick={() => {
            navigate(item.to);
            onNavigate(item.to);
          }}
        >
          {item.icon ? <ItemIcon icon={item.icon} /> : null}
          <span className="zh-launcher__itemLabel">{item.label}</span>
        </button>
      ) : (
        <span
          className="zh-launcher__itemLink zh-launcher__itemLink--disabled"
          title={t("app.layout.menuMissingRoute")}
        >
          {item.icon ? <ItemIcon icon={item.icon} /> : null}
          <span className="zh-launcher__itemLabel">{item.label}</span>
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
