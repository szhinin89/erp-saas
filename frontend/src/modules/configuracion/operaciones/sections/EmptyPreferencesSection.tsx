import { NoAccessPage } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { useI18n } from "../../../../i18n/i18n";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";

/**
 * Placeholder para secciones del hub sin ninguna preferencia con efecto real conectada todavía
 * (Fase C, ver plan CONFIG-DYNAMIC-OPERATIONS-01). No renderiza campos falsos/decorativos — solo
 * un aviso explícito, hasta que exista un wiring real que justifique mostrar controles editables.
 */
export function EmptyPreferencesSection({ titleKey }: { titleKey: string }) {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("settings.operations.view");

  if (!canView) return <NoAccessPage title={t(titleKey)} />;

  return (
    <div className="pg-section">
      <div className="pg-section-body">
        <ZHPageNotice
          variant="info"
          message={t(titleKey)}
          detail={t("settings.operations.notYetConnected")}
        />
      </div>
    </div>
  );
}
