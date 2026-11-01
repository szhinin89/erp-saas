import { useEffect } from "react";
import { Badge } from "../PageShell";
import { useOptionalI18n } from "../../i18n/i18n";
import { formatDateTime } from "../../lib/formatters/dateFormatters";
import { useElectronicInvoicingStatusStore } from "../../store/electronicInvoicingStatusStore";
import type { SriServiceStatusDto } from "../../modules/configuracion/facturacionElectronica/api/electronicInvoicingService";
import {
  SRI_SERVICE_HEALTH_REASON_KEYS,
  SRI_SERVICE_HEALTH_STATE_REGISTRY,
} from "./sriServiceHealthRegistry";

type Props = {
  /** `compact`: badges con el motivo en el tooltip (barras de pantallas emisoras). `detail`: motivo y fecha visibles (Monitor, Configuración SRI). */
  variant?: "compact" | "detail";
};

/**
 * Indicador compartido de salud técnica de los servicios SRI (Recepción / Autorización) en el
 * ambiente de la empresa. Se autoabastece de `useElectronicInvoicingStatusStore.refreshHealth`, que
 * solo lee el monitor central del backend (evidencia de comunicaciones ya ocurridas) — nunca dispara
 * SOAP, ping ni envíos, y no expresa si un comprobante fue autorizado.
 */
export function ZHSriServiceHealth({ variant = "compact" }: Props) {
  const { t } = useOptionalI18n();
  const health = useElectronicInvoicingStatusStore((s) => s.health);
  const refreshHealth = useElectronicInvoicingStatusStore((s) => s.refreshHealth);

  useEffect(() => {
    void refreshHealth();
    const onFocus = () => void refreshHealth();
    window.addEventListener("focus", onFocus);
    return () => window.removeEventListener("focus", onFocus);
  }, [refreshHealth]);

  if (!health) return null;

  const environmentLabel =
    health.environment === "Production"
      ? t("common.sriHealth.environment.production")
      : health.environment === "Test"
        ? t("common.sriHealth.environment.test")
        : null;

  const describe = (status: SriServiceStatusDto) => {
    const visual = SRI_SERVICE_HEALTH_STATE_REGISTRY[status.state];
    const reason = t(SRI_SERVICE_HEALTH_REASON_KEYS[status.reason]);
    const observed = status.lastObservedAtUtc
      ? `${t("common.sriHealth.lastObserved")}: ${formatDateTime(status.lastObservedAtUtc)}`
      : t("common.sriHealth.neverObserved");
    return { visual, label: t(visual.labelKey), reason, observed };
  };

  const services = [
    { key: "reception", name: t("common.sriHealth.reception"), status: health.reception },
    { key: "authorization", name: t("common.sriHealth.authorization"), status: health.authorization },
  ];

  if (variant === "compact") {
    return (
      <span className="zh-sri-health" aria-label={t("common.sriHealth.title")}>
        {environmentLabel && (
          <Badge
            label={environmentLabel}
            variant={health.environment === "Production" ? "info" : "warning"}
            size="md"
          />
        )}
        {services.map(({ key, name, status }) => {
          const d = describe(status);
          return (
            <Badge
              key={key}
              label={`${name}: ${d.label}`}
              variant={d.visual.variant}
              size="md"
              title={`${d.reason} ${d.observed}. ${t("common.sriHealth.disclaimer")}`}
              data-testid={`sri-health-${key}`}
            />
          );
        })}
      </span>
    );
  }

  return (
    <div className="zh-sri-health-detail" aria-label={t("common.sriHealth.title")}>
      <div className="zh-sri-health">
        <span className="zh-text-xs zh-text-muted">{t("common.sriHealth.title")}</span>
        {environmentLabel && (
          <Badge
            label={environmentLabel}
            variant={health.environment === "Production" ? "info" : "warning"}
            size="md"
          />
        )}
      </div>
      {services.map(({ key, name, status }) => {
        const d = describe(status);
        return (
          <div key={key} className="zh-sri-health" data-testid={`sri-health-${key}`}>
            <Badge label={`${name}: ${d.label}`} variant={d.visual.variant} size="md" />
            <span className="zh-text-xs">{d.reason}</span>
            <span className="zh-text-xs zh-text-muted">{d.observed}</span>
          </div>
        );
      })}
      <span className="zh-text-xs zh-text-muted">{t("common.sriHealth.disclaimer")}</span>
    </div>
  );
}
