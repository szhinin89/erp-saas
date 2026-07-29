import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { useI18n } from "../../../i18n/i18n";
import { BranchesManagementSection } from "../components/BranchesManagementSection";

export function BranchesPage() {
  const { t } = useI18n();

  return (
    <ErpPageTemplate
      kicker={t("app.nav.group.configuracion")}
      title={t("branches.title")}
    >
      <BranchesManagementSection />
    </ErpPageTemplate>
  );
}
