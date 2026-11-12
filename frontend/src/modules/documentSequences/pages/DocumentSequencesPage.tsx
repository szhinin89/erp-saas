import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { DocumentSequencesManagementSection } from "../components/DocumentSequencesManagementSection";
import { PhysicalDocumentAuthorizationsSection } from "../components/PhysicalDocumentAuthorizationsSection";

export function DocumentSequencesPage() {
  return (
    <ErpPageTemplate kicker="Configuración" title="Numeración de documentos">
      <DocumentSequencesManagementSection />
      <PhysicalDocumentAuthorizationsSection />
    </ErpPageTemplate>
  );
}
