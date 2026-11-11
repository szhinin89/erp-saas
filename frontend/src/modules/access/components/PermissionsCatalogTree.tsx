import { ZHBtn, ZHToggle } from "../../../components/zh/ZHForm";
import { Badge } from "../../../components/PageShell";
import { useI18n } from "../../../i18n/i18n";
import type {
  PermissionCatalogCategory,
  PermissionCatalogGroup,
  PermissionCatalogItem,
} from "../api/adminPermissionsService";
import type { PermState } from "../permissionsAssignmentState";

interface Props {
  groups: PermissionCatalogGroup[];
  permState: PermState;
  collapsedIds: ReadonlySet<string>;
  onToggleCollapsed: (itemId: string) => void;
  onToggleAction: (item: PermissionCatalogItem, actionCode: string, checked: boolean) => void;
  onToggleCategory: (category: PermissionCatalogCategory) => void;
  categorySelectionState: (category: PermissionCatalogCategory) => "all" | "none" | "partial";
}

/**
 * IAM-V1-F2 — árbol editable Módulo → Categoría → Pantalla → Acciones (extraído de
 * PermissionsAssignmentPage sin cambiar su estado ni sus handlers). Cada pantalla muestra
 * cuántas de sus acciones están concedidas; la ruta técnica ya no se muestra (el código del
 * permiso queda en el tooltip de cada acción).
 */
export function PermissionsCatalogTree({
  groups,
  permState,
  collapsedIds,
  onToggleCollapsed,
  onToggleAction,
  onToggleCategory,
  categorySelectionState,
}: Props) {
  const { t } = useI18n();
  return (
    <>
      {groups.map((group) => (
        <div key={group.code} className="access-pa-group">
          <h4 className="access-pa-group-title">{t(group.labelKey)}</h4>
          {group.categories.map((category) => {
            const selectionState = categorySelectionState(category);
            return (
              <div key={category.id} className="access-pa-category">
                <div className="access-pa-category-header">
                  <span className="access-pa-category-title">{t(category.labelKey)}</span>
                  <ZHBtn
                    variant="ghost"
                    size="xs"
                    type="button"
                    onClick={() => onToggleCategory(category)}
                  >
                    {t(
                      selectionState === "all"
                        ? "permissionsAssignment.category.unmarkAll"
                        : "permissionsAssignment.category.markAll",
                    )}
                  </ZHBtn>
                </div>
                {category.items.map((item) => {
                  const collapsed = collapsedIds.has(item.id);
                  const itemLabel = t(item.labelKey);
                  const grantedCount = item.actions.filter((a) => permState[a.code]).length;
                  return (
                    <div key={item.id} className="pg-section access-pa-item-card">
                      <button
                        type="button"
                        className="pg-section-header access-pa-item-header"
                        onClick={() => onToggleCollapsed(item.id)}
                        aria-expanded={!collapsed}
                        aria-label={t(
                          collapsed
                            ? "permissionsAssignment.expandItem"
                            : "permissionsAssignment.collapseItem",
                          { name: itemLabel },
                        )}
                      >
                        <span className="pg-section-header-left access-pa-item-header-left">
                          <span className="material-symbols-outlined pg-section-icon access-pa-item-chevron">
                            {collapsed ? "chevron_right" : "expand_more"}
                          </span>
                          <span className="access-pa-item-title-wrap">
                            <span className="access-pa-item-title">{itemLabel}</span>
                          </span>
                        </span>
                        <Badge
                          label={t("permissionsAssignment.grantedCount", {
                            granted: grantedCount,
                            total: item.actions.length,
                          })}
                          variant={grantedCount > 0 ? "info" : "neutral"}
                        />
                      </button>
                      {!collapsed && (
                        <div className="pg-section-body access-pa-actions-grid">
                          {item.actions.map((action, index) => (
                            <div
                              key={action.code}
                              title={action.code}
                              className={
                                index === 0
                                  ? "access-pa-action-tile access-pa-action-tile--access"
                                  : "access-pa-action-tile access-pa-action-tile--action"
                              }
                            >
                              <ZHToggle
                                label={action.label}
                                description={action.description}
                                value={!!permState[action.code]}
                                onChange={(checked) => onToggleAction(item, action.code, checked)}
                              />
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            );
          })}
        </div>
      ))}
    </>
  );
}
