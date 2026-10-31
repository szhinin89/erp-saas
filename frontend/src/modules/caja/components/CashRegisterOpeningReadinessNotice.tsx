import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { useI18n } from "../../../i18n/i18n";
import { ReadinessRequirementRow } from "../../configuracion/facades/operationalReadinessGuidanceFacade";
import type { CashRegisterOpeningReadinessDto } from "../api/cajaService";

/**
 * READINESS-2B-3 — habilitación preventiva de la caja seleccionada, antes de iniciar la jornada:
 * qué falta, por qué importa y dónde se corrige (presentación única de Readiness). Solo presenta el
 * diagnóstico del backend; la apertura lo vuelve a evaluar. NotVerified (punto físico) nunca se
 * presenta como autorización fiscal.
 */
export function CashRegisterOpeningReadinessNotice({
  readiness,
  loading,
  error,
}: {
  readiness: CashRegisterOpeningReadinessDto | null;
  loading: boolean;
  error: boolean;
}) {
  const { t } = useI18n();

  if (loading) return <p role="status" className="zh-text-muted zh-text-xs">{t("caja.openReadiness.checking")}</p>;
  if (error) return <ZHPageNotice variant="warning" message={t("caja.openReadiness.checkError")} />;
  if (!readiness) return null;

  return (
    <section aria-label={t("caja.openReadiness.title")}>
      {!readiness.canOpen && (
        <ZHPageNotice variant="error" message={t("caja.openReadiness.blocked")} detail={t("caja.openReadiness.blockedDetail")} />
      )}
      {readiness.requirements.length > 0 && (
        <div className="cfg-opreadiness-item-list">
          {readiness.requirements.map((r) => (
            <ReadinessRequirementRow
              key={r.code}
              code={r.code}
              status={r.severity === "Required" ? "Missing" : "Warning"}
              actionTarget={r.actionTarget}
            />
          ))}
        </div>
      )}
      {readiness.fiscalStatus === "NotVerified" && (
        <ZHPageNotice variant="info" message={t("operationalReadiness.fiscalStatus.notverified.detail")} />
      )}
    </section>
  );
}
