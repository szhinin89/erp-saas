import { navItemStorageKey, navSubtreeMatchesPath, type NavItem, type TranslateFn } from '../../../../nav/navConfig';
import { LauncherMenuItem } from './LauncherMenuItem';
import { useLauncherExpansion } from './useLauncherExpansion';

type LauncherCategoryGroupProps = {
  item: NavItem;
  depth: number;
  currentPath: string;
  onNavigate: (to: string) => void;
  isFavorite: (id: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: TranslateFn;
  forceExpanded: boolean;
};

/**
 * Nivel 3 — Categoría funcional: encabezado en mayúsculas/color secundario, separador
 * superior, colapsable y persistido. Sus hijos son Formularios (Nivel 4) o, si tienen
 * a su vez sub-categorías, otra Categoría anidada (profundidad creciente).
 */
export function LauncherCategoryGroup({
  item, depth, currentPath, onNavigate, isFavorite, toggleFavorite, t, forceExpanded,
}: LauncherCategoryGroupProps) {
  const storageKey = `category:${navItemStorageKey(item)}`;
  const autoExpand = navSubtreeMatchesPath(item, currentPath) ? [storageKey] : [];
  const { isExpanded, toggle } = useLauncherExpansion(autoExpand);
  const open = isExpanded(storageKey) || forceExpanded;
  const contentId = `zh-launcher-category-${storageKey}`;

  return (
    <div className="zh-launcher__category">
      <button
        type="button"
        className="zh-launcher__categoryToggle"
        aria-expanded={open}
        aria-controls={contentId}
        onClick={() => toggle(storageKey)}
      >
        <span className="zh-launcher__categoryLabel">{item.label}</span>
        <span className="material-symbols-outlined zh-launcher__categoryCaret" aria-hidden="true">chevron_right</span>
      </button>
      {open ? (
        <div id={contentId} className="zh-launcher__categoryBody">
          {(item.children ?? []).map((child, idx) =>
            child.children?.length ? (
              <LauncherCategoryGroup
                key={child.id ?? `${storageKey}-c-${idx}`}
                item={child}
                depth={depth + 1}
                currentPath={currentPath}
                onNavigate={onNavigate}
                isFavorite={isFavorite}
                toggleFavorite={toggleFavorite}
                t={t}
                forceExpanded={forceExpanded}
              />
            ) : (
              <LauncherMenuItem
                key={child.id ?? `${storageKey}-i-${idx}-${child.to}`}
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
