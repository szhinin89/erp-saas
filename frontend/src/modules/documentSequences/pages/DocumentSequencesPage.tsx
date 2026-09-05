import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { DocumentSequencesManagementSection } from "../components/DocumentSequencesManagementSection";

export function DocumentSequencesPage() {
  return (
    <ErpPageTemplate kicker="Configuración" title="Secuencias documentales">
      <DocumentSequencesManagementSection />
    </ErpPageTemplate>
  );
}
