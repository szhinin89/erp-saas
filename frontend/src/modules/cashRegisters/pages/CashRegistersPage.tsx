import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { useI18n } from "../../../i18n/i18n";
import { CashRegistersManagementSection } from "../components/CashRegistersManagementSection";

export function CashRegistersPage() {
  const { t } = useI18n();
  return (
    <ErpPageTemplate
      kicker={t("cashRegisters.kicker", "Caja")}
      title={t("cashRegisters.title", "Cajas registradoras")}
    >
      <CashRegistersManagementSection />
    </ErpPageTemplate>
  );
}
