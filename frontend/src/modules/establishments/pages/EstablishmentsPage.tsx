import { NoAccessPage } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { EstablishmentsManagementSection } from "../components/EstablishmentsManagementSection";

export function EstablishmentsPage() {
  const { canShow } = usePermissionsUi();

  if (!canShow("settings.establishments.view")) {
    return <NoAccessPage title="Establecimientos" />;
  }

  return (
    <ErpPageTemplate
      kicker="Configuración"
      title="Establecimientos"
      subtitle="Códigos fiscales registrados ante el SRI — únicos por empresa."
    >
      <EstablishmentsManagementSection />
    </ErpPageTemplate>
  );
}
