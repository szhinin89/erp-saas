import { useSearchParams } from "react-router-dom";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { useI18n } from "../../../../i18n/i18n";
import {
  companySettingsTabs,
  type CompanySettingsTabId,
} from "../companySettingsTabs";
import "../../../../styles/shared/items-catalog.css";

const VALID_TAB_IDS = companySettingsTabs.map((tab) => tab.id);
const DEFAULT_TAB: CompanySettingsTabId = "profile";

function parseTab(raw: string | null): CompanySettingsTabId {
  if (raw && (VALID_TAB_IDS as string[]).includes(raw))
    return raw as CompanySettingsTabId;
  return DEFAULT_TAB;
}

export function CompanySettingsHubPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { t } = useI18n();
  const activeTabId = parseTab(searchParams.get("tab"));

  const setTab = (id: CompanySettingsTabId) => {
    setSearchParams({ tab: id });
  };

  const activeTab =
    companySettingsTabs.find((tab) => tab.id === activeTabId) ??
    companySettingsTabs[0];
  const ActiveComponent = activeTab.component;

  return (
    <ErpPageTemplate
      kicker={t("settings.company.kicker")}
      title="Configuración Empresarial"
      subtitle="Perfil, marca y parámetros de la empresa."
    >
      <div className="prd-tabs" role="tablist">
        {companySettingsTabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={activeTabId === tab.id}
            className={`prd-tab-btn ${activeTabId === tab.id ? "prd-tab-btn--active" : ""}`}
            disabled={!tab.enabled}
            title={tab.enabled ? undefined : t("settings.comingSoon.title")}
            onClick={() => tab.enabled && setTab(tab.id)}
          >
            {t(tab.labelKey)}
          </button>
        ))}
      </div>

      <ActiveComponent />
    </ErpPageTemplate>
  );
}
