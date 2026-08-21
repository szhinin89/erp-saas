import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { useI18n } from "../../../../i18n/i18n";
import { CommunicationsEmailSettingsSection } from "../sections/CommunicationsEmailSettingsSection";

/**
 * Configuración → Comunicaciones → Correo SMTP. Pantalla única (sin Hub, sin tabs), mismo
 * patrón que /settings/electronic-invoicing: administra exclusivamente OrgSettings
 * communications.email.* de la empresa actual.
 */
export function CommunicationsEmailSettingsPage() {
  const { t } = useI18n();

  return (
    <ErpPageTemplate
      kicker={t("settings.communications.email.kicker")}
      title={t("settings.communications.email.title")}
      subtitle={t("settings.communications.email.subtitle")}
    >
      <CommunicationsEmailSettingsSection />
    </ErpPageTemplate>
  );
}
