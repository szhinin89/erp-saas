import type { MainMenuGroup } from '../../../useAppLayoutNavigation';
import type { NavItem, TranslateFn } from '../../../../nav/navConfig';
import { LauncherCategoryGroup } from './LauncherCategoryGroup';
import { LauncherMenuItem } from './LauncherMenuItem';
import { useLauncherExpansion } from './useLauncherExpansion';

function ModuleIcon({ icon }: { icon: string }) {
  const isMaterialSymbol = /^[a-z][a-z0-9_]*$/.test(icon);
  return isMaterialSymbol
    ? <span className="material-symbols-outlined zh-launcher__moduleIcon" aria-hidden="true">{icon}</span>
    : <span className="zh-launcher__moduleIcon" aria-hidden="true">{icon}</span>;
}

type LauncherModuleGroupProps = {
  group: MainMenuGroup;
  currentPath: string;
  onNavigate: (to: string) => void;
  isFavorite: (id: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: TranslateFn;
  forceExpanded: boolean;
};

/**
 * Nivel 2 — Módulo del ERP: bloque de mayor jerarquía visual (icono grande, label en
 * negrita, separador). Si el módulo tiene un solo ítem sin sub-categorías (p. ej.
 * "Inicio"), se renderiza como enlace directo sin acordeón.
 */
export function LauncherModuleGroup({ group, currentPath, onNavigate, isFavorite, toggleFavorite, t, forceExpanded }: LauncherModuleGroupProps) {
  const storageKey = `module:${group.id}`;
  const { isExpanded, toggle } = useLauncherExpansion(group.isActive ? [storageKey] : []);

  const isSingleLink = group.items.length === 1 && !group.items[0].children?.length;

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

  const open = isExpanded(storageKey) || forceExpanded;
  const contentId = `zh-launcher-module-${group.id}`;

  return (
    <div className="zh-launcher__module">
      <button
        type="button"
        className={`zh-launcher__moduleToggle${group.isActive ? ' is-active' : ''}`}
        aria-expanded={open}
        aria-controls={contentId}
        onClick={() => toggle(storageKey)}
      >
        {group.icon ? <ModuleIcon icon={group.icon} /> : null}
        <span className="zh-launcher__moduleLabel">{group.label}</span>
        <span className="material-symbols-outlined zh-launcher__moduleCaret" aria-hidden="true">chevron_right</span>
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
                forceExpanded={forceExpanded}
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
