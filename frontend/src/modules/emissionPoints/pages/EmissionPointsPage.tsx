import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { EmissionPointsManagementSection } from "../components/EmissionPointsManagementSection";
import "./emission-points-page.css";

export function EmissionPointsPage() {
  return (
    <ErpPageTemplate kicker="Configuración" title="Puntos de emisión">
      <EmissionPointsManagementSection />
    </ErpPageTemplate>
  );
}
