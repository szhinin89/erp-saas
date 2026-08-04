import {
  type NavItem,
  type TranslateFn,
} from "../../../../nav/navConfig";
import { LauncherMenuItem } from "./LauncherMenuItem";
import { LauncherIcon } from "./LauncherIcon";

type LauncherCategoryGroupProps = {
  item: NavItem;
  depth: number;
  currentPath: string;
  onNavigate: (to: string) => void;
  isFavorite: (id: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: TranslateFn;
  moduleId: string;
  expandedGroupId: string | null;
  onToggleGroup: (groupId: string) => void;
};

/**
 * Nivel 3 — Categoría funcional: encabezado en mayúsculas/color secundario, separador
 * superior, colapsable y persistido. Sus hijos son Formularios (Nivel 4) o, si tienen
 * a su vez sub-categorías, otra Categoría anidada (profundidad creciente).
 */
export function LauncherCategoryGroup({
  item,
  depth,
  currentPath,
  onNavigate,
  isFavorite,
  toggleFavorite,
  t,
  moduleId,
  expandedGroupId,
  onToggleGroup,
}: LauncherCategoryGroupProps) {
  const groupKey = `${moduleId}:${item.id}`;
  const open = expandedGroupId === groupKey;
  const contentId = `zh-launcher-category-${groupKey}`;

  return (
    <div className="zh-launcher__category">
      <button
        type="button"
        className="zh-launcher__categoryToggle"
        aria-expanded={open}
        aria-controls={contentId}
        onClick={() => onToggleGroup(groupKey)}
      >
        <span className="zh-launcher__categoryLabel">{item.label}</span>
        <LauncherIcon name="chevron" className="zh-launcher__categoryCaret" />
      </button>
      {open ? (
        <div id={contentId} className="zh-launcher__categoryBody">
          {(item.children ?? []).map((child, idx) =>
            child.children?.length ? (
              <LauncherCategoryGroup
                key={child.id ?? `${groupKey}-c-${idx}`}
                item={child}
                depth={depth + 1}
                currentPath={currentPath}
                onNavigate={onNavigate}
                isFavorite={isFavorite}
                toggleFavorite={toggleFavorite}
                t={t}
                moduleId={moduleId}
                expandedGroupId={expandedGroupId}
                onToggleGroup={onToggleGroup}
              />
            ) : (
              <LauncherMenuItem
                key={child.id ?? `${groupKey}-i-${idx}-${child.to}`}
                item={child}
                depth={depth + 1}
                currentPath={currentPath}
                onNavigate={onNavigate}
                isFavorite={isFavorite}
                toggleFavorite={toggleFavorite}
                t={t}
              />
            ),
          )}
        </div>
      ) : null}
    </div>
  );
}
