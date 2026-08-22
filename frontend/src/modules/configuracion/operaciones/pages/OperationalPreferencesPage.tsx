import { useSearchParams } from "react-router-dom";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { useI18n } from "../../../../i18n/i18n";
import {
  operationalPreferencesTabs,
  type OperationalPreferencesTabId,
} from "../operationalPreferencesTabs";
import "../../../../styles/shared/items-catalog.css";

const VALID_TAB_IDS = operationalPreferencesTabs.map((tab) => tab.id);
const DEFAULT_TAB: OperationalPreferencesTabId = "salesPos";

function parseTab(raw: string | null): OperationalPreferencesTabId {
  if (raw && (VALID_TAB_IDS as string[]).includes(raw))
    return raw as OperationalPreferencesTabId;
  return DEFAULT_TAB;
}

export function OperationalPreferencesPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { t } = useI18n();
  const activeTabId = parseTab(searchParams.get("tab"));

  const setTab = (id: OperationalPreferencesTabId) => {
    setSearchParams({ tab: id });
  };

  const activeTab =
    operationalPreferencesTabs.find((tab) => tab.id === activeTabId) ??
    operationalPreferencesTabs[0];
  const ActiveComponent = activeTab.component;

  return (
    <ErpPageTemplate
      kicker={t("settings.operations.kicker")}
      title={t("settings.operations.pageTitle")}
      subtitle={t("settings.operations.pageSubtitle")}
    >
      <div className="prd-tabs" role="tablist">
        {operationalPreferencesTabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={activeTabId === tab.id}
            className={`prd-tab-btn ${activeTabId === tab.id ? "prd-tab-btn--active" : ""}`}
            onClick={() => setTab(tab.id)}
          >
            {t(tab.labelKey)}
          </button>
        ))}
      </div>

      <ActiveComponent />
    </ErpPageTemplate>
  );
}
