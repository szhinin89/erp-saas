import { useMemo } from "react";
import { useI18n } from "../../../i18n/i18n";
import type { PermissionCatalog } from "../api/adminPermissionsService";
import { summarizeGrants } from "../permissionsAssignmentState";
import "./PermissionGrantSummary.css";

interface Props {
  catalog: PermissionCatalog;
  isGranted: (code: string) => boolean;
  /** Título del bloque (p. ej. «Este perfil concede»). */
  title: string;
}

/**
 * IAM-V1-F2 — resumen de solo lectura de lo que concede un perfil: módulos → pantallas →
 * acciones, derivado del mismo catálogo del Kernel que la edición. Lo reutilizan Asignación de
 * permisos (mientras se edita) y la ficha del usuario (pestaña Accesos).
 */
export function PermissionGrantSummary({ catalog, isGranted, title }: Props) {
  const { t } = useI18n();
  const summary = useMemo(() => summarizeGrants(catalog, isGranted), [catalog, isGranted]);

  return (
    <section className="access-grant-summary" aria-label={title}>
      <header className="access-grant-summary-header">
        <h3 className="access-grant-summary-title">{title}</h3>
        <span className="access-grant-summary-count">
          {t("permissionsSummary.count", {
            screens: summary.screenCount,
            actions: summary.actionCount,
            total: summary.totalActionCount,
          })}
        </span>
      </header>
      {summary.groups.length === 0 ? (
        <p className="subtle">
          {t(
            "permissionsSummary.empty",
            "Sin pantallas concedidas: este perfil no da acceso a ninguna pantalla.",
          )}
        </p>
      ) : (
        summary.groups.map((group) => (
          <div key={group.code} className="access-grant-summary-group">
            <h4 className="access-grant-summary-group-title">{t(group.labelKey)}</h4>
            <ul className="access-grant-summary-list">
              {group.screens.map((screen) => (
                <li key={screen.id} className="access-grant-summary-item">
                  <span className="access-grant-summary-screen">{t(screen.labelKey)}</span>
                  <span className="access-grant-summary-actions">
                    {screen.actionLabels.join(" · ")}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        ))
      )}
    </section>
  );
}
