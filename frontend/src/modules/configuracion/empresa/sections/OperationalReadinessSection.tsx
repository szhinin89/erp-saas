import { Link } from "react-router-dom";
import { Badge, LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ReportKpiCard } from "../../../../components/ReportPageTemplate";
import { useI18n } from "../../../../i18n/i18n";
import { useAsync } from "../../../../hooks/useAsync";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { companyProfileService } from "../api/companyProfileService";
import { getOperationalReadinessActionRoute } from "../operationalReadinessActionRoutes";
import type {
  CompanyOperationalReadinessItem,
  CompanyOperationalReadinessSection,
  ReadinessStatus,
} from "../../../../types/companyProfile";
import "../../../../styles/shared/items-catalog.css";
import "./operational-readiness.css";

const SECTION_ICONS: Record<string, string> = {
  identity: "badge",
  structure: "account_tree",
  electronicInvoicing: "receipt_long",
  sales: "sell",
  inventory: "inventory_2",
  cashRegister: "point_of_sale",
  documents: "description",
};

function statusBadgeVariant(status: ReadinessStatus): "success" | "warning" | "error" | "neutral" {
  switch (status) {
    case "Ready":
      return "success";
    case "Warning":
      return "warning";
    case "Missing":
      return "error";
    default:
      return "neutral";
  }
}

function overallNoticeVariant(status: ReadinessStatus): "success" | "warning" | "error" {
  if (status === "Ready") return "success";
  if (status === "Warning") return "warning";
  return "error";
}

function messageKeyForStatus(code: string, status: ReadinessStatus): string {
  if (status === "Warning") return `operationalReadiness.items.${code}.warning`;
  if (status === "Missing") return `operationalReadiness.items.${code}.missing`;
  return `operationalReadiness.items.${code}.ready`;
}

function ReadinessItemRow({ item }: { item: CompanyOperationalReadinessItem }) {
  const { t } = useI18n();
  const label = t(`operationalReadiness.items.${item.code}.label`);
  const message = t(messageKeyForStatus(item.code, item.status));
  const actionLabelKey = item.status === "Ready" ? "common.review" : "common.configure";
  const actionRoute = getOperationalReadinessActionRoute(item.actionTarget);

  return (
    <div className="opreadiness-item-row">
      <Badge
        label={t(`operationalReadiness.statusBadge.${item.status.toLowerCase()}`, item.status)}
        variant={statusBadgeVariant(item.status)}
      />
      <div className="opreadiness-item-text">
        <p className="opreadiness-item-label">{label}</p>
        <p className="opreadiness-item-message zh-text-muted zh-text-xs">{message}</p>
      </div>
      {actionRoute && (
        <Link to={actionRoute} className="zh-link opreadiness-item-action">
          {t(actionLabelKey)}
        </Link>
      )}
    </div>
  );
}

function ReadinessSectionCard({ section }: { section: CompanyOperationalReadinessSection }) {
  const { t } = useI18n();
  return (
    <div className="pg-section opreadiness-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">
            {SECTION_ICONS[section.code] ?? "checklist"}
          </span>
          <p className="pg-section-label">
            {t(`operationalReadiness.sections.${section.code}.title`)}
          </p>
        </div>
        <Badge
          label={t(`operationalReadiness.statusBadge.${section.status.toLowerCase()}`, section.status)}
          variant={statusBadgeVariant(section.status)}
        />
      </div>
      <div className="pg-section-body opreadiness-item-list">
        {section.items.map((item) => (
          <ReadinessItemRow key={item.code} item={item} />
        ))}
      </div>
    </div>
  );
}

export function OperationalReadinessSection() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canView = canShow("erp.companies.view");

  const readinessState = useAsync(
    () => companyProfileService.getOperationalReadiness(),
    canView,
  );

  if (!canView)
    return <NoAccessPage title={t("settings.company.tabs.readiness")} />;
  if (readinessState.loading) return <LoadingState />;
  if (readinessState.error || !readinessState.data)
    return <ZHPageNotice variant="error" message={t("operationalReadiness.loadError")} />;

  const readiness = readinessState.data;

  const capabilities: Array<{
    key: string;
    icon: string;
    enabled: boolean;
  }> = [
    { key: "canSell", icon: "sell", enabled: readiness.canSell },
    {
      key: "canIssueElectronicInvoices",
      icon: "receipt_long",
      enabled: readiness.canIssueElectronicInvoices,
    },
    { key: "canUseInventory", icon: "inventory_2", enabled: readiness.canUseInventory },
    { key: "canUseCashRegister", icon: "point_of_sale", enabled: readiness.canUseCashRegister },
  ];

  return (
    <div className="opreadiness-root">
      <ZHPageNotice
        variant={overallNoticeVariant(readiness.overallStatus)}
        message={t(`operationalReadiness.overallStatus.${readiness.overallStatus.toLowerCase()}`)}
        detail={t("operationalReadiness.subtitle")}
      />

      <div className="pg-kpis opreadiness-capabilities">
        {capabilities.map((cap) => (
          <ReportKpiCard
            key={cap.key}
            icon={cap.icon}
            tone={cap.enabled ? "primary" : "neutral"}
            label={t(`operationalReadiness.capabilities.${cap.key}`)}
            value={t(
              cap.enabled
                ? "operationalReadiness.capabilities.enabled"
                : "operationalReadiness.capabilities.disabled",
            )}
            valueTone={cap.enabled ? "default" : "danger"}
          />
        ))}
      </div>

      <div className="opreadiness-sections">
        {readiness.sections.map((section) => (
          <ReadinessSectionCard key={section.code} section={section} />
        ))}
      </div>
    </div>
  );
}
