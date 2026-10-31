import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { DocumentSequencesManagementSection } from "../components/DocumentSequencesManagementSection";
import { PhysicalDocumentAuthorizationsSection } from "../components/PhysicalDocumentAuthorizationsSection";

export function DocumentSequencesPage() {
  return (
    <ErpPageTemplate kicker="Configuración" title="Secuencias documentales">
      <DocumentSequencesManagementSection />
      <PhysicalDocumentAuthorizationsSection />
    </ErpPageTemplate>
  );
}
