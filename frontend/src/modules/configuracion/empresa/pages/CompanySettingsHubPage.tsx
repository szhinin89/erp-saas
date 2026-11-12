import { useSearchParams } from "react-router-dom";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { useI18n } from "../../../../i18n/i18n";
import {
  companySettingsTabs,
  type CompanySettingsTabId,
} from "../companySettingsTabs";
import { useCompanySettingsTabAccess } from "../companySettingsTabAccess";
import "../../../../styles/shared/items-catalog.css";
import { ZHTabBar } from "../../../../components/zh/ZHTabBar";

const VALID_TAB_IDS = companySettingsTabs.map((tab) => tab.id);

function isTabId(raw: string | null): raw is CompanySettingsTabId {
  return !!raw && (VALID_TAB_IDS as string[]).includes(raw);
}

export function CompanySettingsHubPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { t } = useI18n();
  const access = useCompanySettingsTabAccess();
  const title = t("settings.company.pageTitle", "Datos de la empresa");

  // NAV-COVERAGE-H2: ninguna pestaña se concede mientras la sesión no resuelve los permisos.
  if (access.status === "loading") return <LoadingState />;

  const visibleTabs = companySettingsTabs.filter((tab) => access.canViewTab(tab.id));
  if (visibleTabs.length === 0) return <NoAccessPage title={title} />;

  const requested = searchParams.get("tab");
  // Una pestaña existente sin permiso responde "sin acceso"; ausente o desconocida → primera visible.
  const deniedTab = isTabId(requested) && !access.canViewTab(requested);
  const activeTab = deniedTab
    ? null
    : (visibleTabs.find((tab) => tab.id === requested) ?? visibleTabs[0]);

  const setTab = (id: CompanySettingsTabId) => {
    setSearchParams({ tab: id });
  };

  const ActiveComponent = activeTab?.component;

  return (
    <ErpPageTemplate
      kicker={t("settings.company.kicker")}
      title={title}
      subtitle={t(
        "settings.company.pageSubtitle",
        "Perfil, marca y parámetros de la empresa.",
      )}
    >
      <ZHTabBar
        tabs={visibleTabs.map((tab) => ({
          id: tab.id,
          label: t(tab.labelKey),
          disabled: !tab.enabled,
          title: tab.enabled ? undefined : t("settings.comingSoon.title"),
        }))}
        activeTab={(activeTab?.id ?? "") as CompanySettingsTabId}
        onChange={setTab}
      />

      {ActiveComponent ? <ActiveComponent /> : <NoAccessPage title={title} />}
    </ErpPageTemplate>
  );
}
