import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { Badge } from "../../../../components/PageShell";
import { useNavigationAccess } from "../../../../components/navigationAccess";
import { useI18n } from "../../../../i18n/i18n";
import { getOperationalReadinessActionRoute } from "../operationalReadinessActionRoutes";
import type {
  ReadinessActionTarget,
  ReadinessFiscalStatus,
  ReadinessStatus,
} from "../../../../types/companyProfile";
import "./operational-readiness.css";

export function statusBadgeVariant(status: ReadinessStatus): "success" | "warning" | "error" | "neutral" {
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
export function fiscalBadgeVariant(status: ReadinessFiscalStatus): "success" | "warning" | "info" | "neutral" {
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

function messageKeyForStatus(code: string, status: ReadinessStatus): string {
  if (status === "Warning") return `operationalReadiness.items.${code}.warning`;
  if (status === "Missing") return `operationalReadiness.items.${code}.missing`;
  if (status === "NotApplicable") return `operationalReadiness.items.${code}.notApplicable`;
  return `operationalReadiness.items.${code}.ready`;
}

/**
 * «Se corrige en: {pantalla}» según el menú autenticado (useNavigationAccess): un destino del menú
 * implica permiso de vista, no de edición. Sin acceso, se pide asistencia a un administrador.
 */
function ReadinessRequirementAction({
  actionTarget,
  isReady,
}: {
  actionTarget: ReadinessActionTarget | null;
  isReady: boolean;
}) {
  const { t } = useI18n();
  const navigation = useNavigationAccess();
  const route = getOperationalReadinessActionRoute(actionTarget);
  if (!route || !navigation.resolved) return null;

  const destination = navigation.findDestination(route);
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

/**
 * Un requisito de Readiness: qué es (`label`), por qué importa (mensaje según estado) y dónde se
 * corrige. Textos de `operationalReadiness.items.{code}`; el código y el destino vienen del backend.
 * Única presentación de un requisito — la usan Preparación de Datos y la apertura de caja.
 */
export function ReadinessRequirementRow({
  code,
  status,
  actionTarget,
  anchorId,
  children,
}: {
  code: string;
  status: ReadinessStatus;
  actionTarget: ReadinessActionTarget | null;
  anchorId?: string;
  children?: ReactNode;
}) {
  const { t } = useI18n();
  return (
    <div id={anchorId} className="cfg-opreadiness-item-row">
      <Badge
        label={t(`operationalReadiness.statusBadge.${status.toLowerCase()}`, status)}
        variant={statusBadgeVariant(status)}
      />
      <div className="cfg-opreadiness-item-text">
        <p className="cfg-opreadiness-item-label">{t(`operationalReadiness.items.${code}.label`)}</p>
        <p className="cfg-opreadiness-item-message zh-text-muted zh-text-xs">
          {t(messageKeyForStatus(code, status))}
        </p>
        {children}
        <ReadinessRequirementAction actionTarget={actionTarget} isReady={status === "Ready"} />
      </div>
    </div>
  );
}
