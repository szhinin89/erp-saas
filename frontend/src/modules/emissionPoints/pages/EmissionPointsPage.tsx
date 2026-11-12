import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { EmissionPointsManagementSection } from "../components/EmissionPointsManagementSection";

export function EmissionPointsPage() {
  return (
    <ErpPageTemplate kicker="Configuración" title="Puntos de emisión">
      <EmissionPointsManagementSection />
    </ErpPageTemplate>
  );
}
