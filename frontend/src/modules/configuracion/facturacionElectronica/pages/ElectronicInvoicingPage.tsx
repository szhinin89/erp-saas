import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { useI18n } from "../../../../i18n/i18n";
import { SriConfigurationSection } from "../sections/SriConfigurationSection";

/**
 * Pantalla única de configuración SRI para facturación electrónica. Sin Hub, sin tabs:
 * este módulo administra exclusivamente la configuración propia del motor de facturación
 * electrónica (SriSettings) — certificados, numeración y RIDE pertenecen a otros dominios
 * (o, en el caso de RIDE, no existen todavía en ningún lugar del ERP).
 */
export function ElectronicInvoicingPage() {
  const { t } = useI18n();

  return (
    <ErpPageTemplate
      kicker={t("settings.electronicInvoicing.kicker")}
      title={t("settings.electronicInvoicing.title")}
      subtitle={t("settings.electronicInvoicing.subtitle")}
    >
      <SriConfigurationSection />
    </ErpPageTemplate>
  );
}
