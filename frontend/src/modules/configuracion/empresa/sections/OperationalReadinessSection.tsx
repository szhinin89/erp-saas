import { Link } from "react-router-dom";
import { Badge, LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { ReportKpiCard } from "../../../../components/ReportPageTemplate";
import { useNavigationAccess } from "../../../../components/navigationAccess";
import { useI18n } from "../../../../i18n/i18n";
import { useAsync } from "../../../../hooks/useAsync";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { useActiveBranchStore } from "../../../../store/activeBranchStore";
import { companyProfileService } from "../api/companyProfileService";
import { getOperationalReadinessActionRoute } from "../operationalReadinessActionRoutes";
import type {
  CompanyOperationalReadinessCashRegister,
  CompanyOperationalReadinessItem,
  CompanyOperationalReadinessSection,
  ReadinessBlockingArea,
  ReadinessFiscalStatus,
  ReadinessStatus,
} from "../../../../types/companyProfile";
import "../../../../styles/shared/items-catalog.css";
import "./operational-readiness.css";

const SECTION_ICONS: Record<string, string> = {
  identity: "badge",
  structure: "account_tree",
  accounting: "account_balance",
  electronicInvoicing: "receipt_long",
  sales: "sell",
  inventory: "inventory_2",
  cashRegister: "point_of_sale",
  documents: "description",
};

/** Etiqueta de cada área bloqueada: reutiliza la de la capacidad que el backend calcula para ella. */
const AREA_CAPABILITY_KEY: Record<ReadinessBlockingArea, string> = {
  Sales: "canSell",
  ElectronicInvoicing: "canIssueElectronicInvoices",
  Inventory: "canUseInventory",
  CashRegister: "canUseCashRegister",
};

type Register = CompanyOperationalReadinessCashRegister;

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

/** NotVerified (punto físico, brechas F-1..F-5) nunca se pinta como éxito ni como bloqueo. */
function fiscalBadgeVariant(status: ReadinessFiscalStatus): "success" | "warning" | "info" | "neutral" {
  switch (status) {
    case "Verified":
      return "success";
    case "Pending":
      return "warning";
    case "NotVerified":
      return "info";
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
  if (status === "NotApplicable") return `operationalReadiness.items.${code}.notApplicable`;
  return `operationalReadiness.items.${code}.ready`;
}

function itemAnchorId(code: string): string {
  return `readiness-item-${code}`;
}

function registerPoint(register: Register): string | null {
  if (!register.establishmentCode || !register.emissionPointCode) return null;
  return `${register.establishmentCode}-${register.emissionPointCode}`;
}

/** Inversa de cashRegisters[].requirementCodes: qué cajas referencian cada requisito. */
function registersByRequirement(registers: Register[]): Map<string, Register[]> {
  const map = new Map<string, Register[]>();
  for (const register of registers) {
    for (const code of register.requirementCodes) {
      const list = map.get(code) ?? [];
      list.push(register);
      map.set(code, list);
    }
  }
  return map;
}

function ReadinessItemAction({ item }: { item: CompanyOperationalReadinessItem }) {
  const { t } = useI18n();
  const navigation = useNavigationAccess();
  const route = getOperationalReadinessActionRoute(item.actionTarget);
  if (!route || !navigation.resolved) return null;

  const destination = navigation.findDestination(route);
  const isReady = item.status === "Ready";
  if (!destination) {
    // Un requisito ya listo no necesita acción: el aviso solo aparece si hay algo que corregir.
    if (isReady) return null;
    return (
      <p className="cfg-opreadiness-item-message zh-text-muted zh-text-xs">
        {t("operationalReadiness.item.noAccess")}
      </p>
    );
  }

  return (
    <p className="cfg-opreadiness-item-message zh-text-xs">
      {t(isReady ? "operationalReadiness.item.reviewIn" : "operationalReadiness.item.fixIn")}{" "}
      <Link to={destination.to} className="zh-link">
        {destination.label}
      </Link>
    </p>
  );
}

function ReadinessItemRow({
  item,
  affectedRegisters,
}: {
  item: CompanyOperationalReadinessItem;
  affectedRegisters: Register[];
}) {
  const { t } = useI18n();
  const label = t(`operationalReadiness.items.${item.code}.label`);
  const message = t(messageKeyForStatus(item.code, item.status));
  const pending = item.status === "Missing" || item.status === "Warning";
  const areas = item.blockingAreas
    .map((area) => t(`operationalReadiness.capabilities.${AREA_CAPABILITY_KEY[area]}`))
    .join(", ");
  const registers = affectedRegisters
    .map((r) => {
      const point = registerPoint(r);
      return point ? `${r.code} (${point})` : r.code;
    })
    .join(", ");

  return (
    <div id={itemAnchorId(item.code)} className="cfg-opreadiness-item-row">
      <Badge
        label={t(`operationalReadiness.statusBadge.${item.status.toLowerCase()}`, item.status)}
        variant={statusBadgeVariant(item.status)}
      />
      <div className="cfg-opreadiness-item-text">
        <p className="cfg-opreadiness-item-label">{label}</p>
        <p className="cfg-opreadiness-item-message zh-text-muted zh-text-xs">{message}</p>
        {pending && (
          <p className="cfg-opreadiness-item-message zh-text-xs">
            {areas
              ? t("operationalReadiness.item.blocks", { areas })
              : t("operationalReadiness.item.blocksNone")}
          </p>
        )}
        {registers && (
          <p className="cfg-opreadiness-item-message zh-text-xs">
            {t("operationalReadiness.item.affectedRegisters", { registers })}
          </p>
        )}
        <ReadinessItemAction item={item} />
      </div>
    </div>
  );
}

function ReadinessSectionCard({
  section,
  affected,
}: {
  section: CompanyOperationalReadinessSection;
  affected: Map<string, Register[]>;
}) {
  const { t } = useI18n();
  return (
    <div className="pg-section cfg-opreadiness-section">
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
      <div className="pg-section-body cfg-opreadiness-item-list">
        {section.items.map((item) => (
          <ReadinessItemRow
            key={item.code}
            item={item}
            affectedRegisters={affected.get(item.code) ?? []}
          />
        ))}
      </div>
    </div>
  );
}

function ReadinessCashRegistersCard({ registers }: { registers: Register[] }) {
  const { t } = useI18n();
  const total = registers.length;
  const ready = registers.filter((r) => r.operationalStatus === "Ready").length;
  const pending = total - ready;

  const columns: ZHDataTableColumn<Register>[] = [
    {
      key: "register",
      header: t("operationalReadiness.cashRegisters.columns.register"),
      render: (r) => (
        <div className="cfg-opreadiness-item-text">
          <p className="cfg-opreadiness-item-label">{r.code}</p>
          <p className="cfg-opreadiness-item-message zh-text-muted zh-text-xs">{r.name}</p>
        </div>
      ),
    },
    {
      key: "emissionPoint",
      header: t("operationalReadiness.cashRegisters.columns.emissionPoint"),
      render: (r) => registerPoint(r) ?? t("operationalReadiness.cashRegisters.noEmissionPoint"),
    },
    {
      key: "emissionType",
      header: t("operationalReadiness.cashRegisters.columns.emissionType"),
      render: (r) =>
        r.emissionType
          ? t(`operationalReadiness.emissionType.${r.emissionType.toLowerCase()}`, r.emissionType)
          : "—",
    },
    {
      key: "operationalStatus",
      header: t("operationalReadiness.cashRegisters.columns.operationalStatus"),
      render: (r) => (
        <Badge
          label={t(`operationalReadiness.statusBadge.${r.operationalStatus.toLowerCase()}`, r.operationalStatus)}
          variant={statusBadgeVariant(r.operationalStatus)}
        />
      ),
    },
    {
      key: "fiscalStatus",
      header: t("operationalReadiness.cashRegisters.columns.fiscalStatus"),
      render: (r) => {
        const key = `operationalReadiness.fiscalStatus.${r.fiscalStatus.toLowerCase()}`;
        return (
          <div className="cfg-opreadiness-item-text">
            <Badge label={t(`${key}.label`, r.fiscalStatus)} variant={fiscalBadgeVariant(r.fiscalStatus)} />
            <p className="cfg-opreadiness-item-message zh-text-muted zh-text-xs">{t(`${key}.detail`)}</p>
          </div>
        );
      },
    },
    {
      key: "requirements",
      header: t("operationalReadiness.cashRegisters.columns.requirements"),
      render: (r) =>
        r.requirementCodes.length === 0 ? (
          t("operationalReadiness.cashRegisters.noRequirements")
        ) : (
          <ul className="cfg-opreadiness-requirement-list">
            {r.requirementCodes.map((code) => (
              <li key={code}>
                <a href={`#${itemAnchorId(code)}`} className="zh-link">
                  {t(`operationalReadiness.items.${code}.label`)}
                </a>
              </li>
            ))}
          </ul>
        ),
    },
  ];

  return (
    <div className="pg-section cfg-opreadiness-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">point_of_sale</span>
          <p className="pg-section-label">{t("operationalReadiness.cashRegisters.title")}</p>
        </div>
      </div>
      <div className="pg-section-body cfg-opreadiness-cash-registers">
        {total > 0 && (
          <div className="pg-kpis cfg-opreadiness-capabilities">
            <ReportKpiCard
              icon="check_circle"
              tone={ready > 0 ? "primary" : "neutral"}
              label={t("operationalReadiness.cashRegisters.ready")}
              value={t("operationalReadiness.cashRegisters.countOf", { count: ready, total })}
            />
            <ReportKpiCard
              icon="pending"
              tone="neutral"
              label={t("operationalReadiness.cashRegisters.pending")}
              value={t("operationalReadiness.cashRegisters.countOf", { count: pending, total })}
              valueTone={pending > 0 ? "danger" : "default"}
            />
          </div>
        )}
        {ready > 0 && pending > 0 && (
          <ZHPageNotice
            variant="warning"
            message={t("operationalReadiness.cashRegisters.partial", { count: pending, total })}
          />
        )}
        <ZHDataTable
          columns={columns}
          rows={registers}
          rowKey={(r) => r.cashRegisterId}
          tableClassName="table--compact table--align-top"
          emptyMessage={t("operationalReadiness.cashRegisters.empty")}
        />
      </div>
    </div>
  );
}

export function OperationalReadinessSection() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canView = canShow("erp.companies.view");
  // El diagnóstico es de la sucursal activa (X-Branch-Id): sin sucursal no se consulta ni se
  // elige otra — el selector de sucursal (useBranchGate) es el único que la define.
  const branch = useActiveBranchStore((s) => s.branch);
  const branchId = branch?.id ?? null;

  // Un montaje nuevo (volver de la pantalla donde se corrigió un dato) consulta de nuevo; el
  // estado vive solo en useAsync, sin almacenamiento paralelo.
  const readinessState = useAsync(
    () => companyProfileService.getOperationalReadiness(),
    canView && branchId !== null,
    [branchId],
  );

  if (!canView)
    return <NoAccessPage title={t("settings.company.tabs.readiness")} />;
  if (branch === null)
    return <ZHPageNotice variant="warning" message={t("operationalReadiness.branchRequired")} />;
  if (readinessState.error)
    return <ZHPageNotice variant="error" message={t("operationalReadiness.loadError")} />;

  // Una respuesta de otra sucursal (cambio de sucursal en curso) nunca se muestra.
  const readiness =
    readinessState.data?.branchId === branch.id ? readinessState.data : null;
  if (!readiness) return <LoadingState />;

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
  const showPartialHint =
    readiness.overallStatus !== "Ready" && capabilities.some((cap) => cap.enabled);
  const affected = registersByRequirement(readiness.cashRegisters);

  return (
    <div className="cfg-opreadiness-root">
      <div className="cfg-opreadiness-header">
        <p className="pg-section-label">
          {t("operationalReadiness.branchHeader", { branch: branch.name })}
        </p>
        <ZHBtn
          type="button"
          variant="secondary"
          size="sm"
          icon="refresh"
          disabled={readinessState.loading}
          onClick={readinessState.refetch}
        >
          {t(readinessState.loading ? "operationalReadiness.rechecking" : "operationalReadiness.recheck")}
        </ZHBtn>
      </div>

      <ZHPageNotice
        variant={overallNoticeVariant(readiness.overallStatus)}
        message={t(`operationalReadiness.overallStatus.${readiness.overallStatus.toLowerCase()}`)}
        detail={t("operationalReadiness.subtitle")}
      />

      <div className="pg-kpis cfg-opreadiness-capabilities">
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
      {showPartialHint && (
        <p className="zh-text-muted zh-text-xs cfg-opreadiness-hint">
          {t("operationalReadiness.capabilities.partialHint")}
        </p>
      )}

      <ReadinessCashRegistersCard registers={readiness.cashRegisters} />

      <div className="cfg-opreadiness-sections">
        {readiness.sections.map((section) => (
          <ReadinessSectionCard key={section.code} section={section} affected={affected} />
        ))}
      </div>
    </div>
  );
}
