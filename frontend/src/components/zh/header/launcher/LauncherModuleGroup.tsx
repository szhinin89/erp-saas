import type { MainMenuGroup } from "../../../useAppLayoutNavigation";
import type { NavItem, TranslateFn } from "../../../../nav/navConfig";
import { LauncherCategoryGroup } from "./LauncherCategoryGroup";
import { LauncherMenuItem } from "./LauncherMenuItem";
import { LauncherIcon, LauncherModuleIcon } from "./LauncherIcon";

type LauncherModuleGroupProps = {
  group: MainMenuGroup;
  currentPath: string;
  onNavigate: (to: string) => void;
  isFavorite: (id: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: TranslateFn;
  expandedModuleId: string | null;
  onToggleModule: (moduleId: string) => void;
  expandedGroupId: string | null;
  onToggleGroup: (groupId: string) => void;
};

/**
 * Nivel 2 — Módulo del ERP: bloque de mayor jerarquía visual (icono grande, label en
 * negrita, separador). Si el módulo tiene un solo ítem sin sub-categorías (p. ej.
 * "Inicio"), se renderiza como enlace directo sin acordeón.
 */
export function LauncherModuleGroup({
  group,
  currentPath,
  onNavigate,
  isFavorite,
  toggleFavorite,
  t,
  expandedModuleId,
  onToggleModule,
  expandedGroupId,
  onToggleGroup,
}: LauncherModuleGroupProps) {
  const isSingleLink =
    group.items.length === 1 && !group.items[0].children?.length;

  if (isSingleLink) {
    return (
      <div className="zh-launcher__module zh-launcher__module--single">
        <LauncherMenuItem
          item={group.items[0]}
          depth={0}
          currentPath={currentPath}
          onNavigate={onNavigate}
          isFavorite={isFavorite}
          toggleFavorite={toggleFavorite}
          t={t}
        />
      </div>
    );
  }

  const open = expandedModuleId === group.id;
  const contentId = `zh-launcher-module-${group.id}`;

  return (
    <div className={`zh-launcher__module${open ? " is-open" : ""}`}>
      <button
        type="button"
        className={`zh-launcher__moduleToggle${group.isActive ? " is-active" : ""}`}
        aria-expanded={open}
        aria-controls={contentId}
        onClick={() => onToggleModule(group.id)}
      >
        <LauncherModuleIcon
          moduleId={group.id}
          className="zh-launcher__moduleIcon"
        />
        <span className="zh-launcher__moduleLabel">{group.label}</span>
        <LauncherIcon name="chevron" className="zh-launcher__moduleCaret" />
      </button>
      {open ? (
        <div id={contentId} className="zh-launcher__moduleBody">
          {group.items.map((item, idx) =>
            item.children?.length ? (
              <LauncherCategoryGroup
                key={item.id ?? `${group.id}-c-${idx}`}
                item={item}
                depth={0}
                currentPath={currentPath}
                onNavigate={onNavigate}
                isFavorite={isFavorite}
                toggleFavorite={toggleFavorite}
                t={t}
                moduleId={group.id}
                expandedGroupId={expandedGroupId}
                onToggleGroup={onToggleGroup}
              />
            ) : (
              <LauncherMenuItem
                key={item.id ?? `${group.id}-i-${idx}-${item.to}`}
                item={item}
                depth={0}
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
