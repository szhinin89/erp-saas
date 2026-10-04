import { useEffect, useState } from "react";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHTabBar, type ZHTab } from "../../../components/zh/ZHTabBar";
import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { ELECTRONIC_INVOICING_STATUS_REGISTRY } from "../../../components/zh/electronicInvoicingStatusRegistry";
import type { ZHFormAlertType } from "../../../components/zh/ZHForm";
import { useI18n } from "../../../i18n/i18n";
import { useElectronicInvoicingStatusStore } from "../../../store/electronicInvoicingStatusStore";
import {
  cashRegisterLookupFacade,
  type CashRegisterDto,
} from "../../caja/facades/cashRegisterLookupFacade";
import type { SalesPageContext } from "../hooks/useSalesPage";
import { resolveSalesOperationalContext } from "../utils/salesOperationalContext";
import { SalesEmissionConfigModal } from "./SalesEmissionConfigSection";

/** Traducción de la severidad visual del registro de facturación electrónica a variante de Badge. */
const ALERT_TO_BADGE: Record<ZHFormAlertType, BadgeVariant> = {
  success: "success",
  warning: "warning",
  attention: "warning",
  error: "error",
  info: "info",
  neutral: "neutral",
};

/** Ambiente SRI compacto — misma fuente y misma semántica visual que ZHElectronicEnvironmentBanner
 * (ELECTRONIC_INVOICING_STATUS_REGISTRY); solo se muestra en ventas electrónicas. */
function ElectronicEnvironmentBadge() {
  const { t } = useI18n();
  const status = useElectronicInvoicingStatusStore((s) => s.status);
  const isLoaded = useElectronicInvoicingStatusStore((s) => s.isLoaded);
  const isLoading = useElectronicInvoicingStatusStore((s) => s.isLoading);
  const refresh = useElectronicInvoicingStatusStore((s) => s.refresh);

  useEffect(() => {
    if (!isLoaded && !isLoading) void refresh();
  }, [isLoaded, isLoading, refresh]);

  if (!status) return null;
  const visual = ELECTRONIC_INVOICING_STATUS_REGISTRY[status.status];
  // Estado desconocido (respuesta inesperada): no se muestra — nunca tumba el POS.
  if (!visual) return null;
  const label = status.environmentName
    ? `Ambiente ${status.environmentName}`
    : t(visual.messageKey);
  return (
    <Badge
      variant={ALERT_TO_BADGE[visual.variant] ?? "neutral"}
      label={label}
      size="md"
      title={`${t(visual.messageKey)} — ${t(visual.detailKey)}`}
    />
  );
}

/** Caja de la sesión abierta (solo para leer su establecimiento) — fachada pública read-only de
 * Caja; si falla (permiso/red) el header muestra "—", nunca bloquea la venta. */
function useSessionRegisters(cashRegisterId: string | null | undefined): CashRegisterDto[] {
  const [registers, setRegisters] = useState<CashRegisterDto[]>([]);
  useEffect(() => {
    if (!cashRegisterId) return;
    let cancelled = false;
    cashRegisterLookupFacade
      .getCashRegisters(true)
      .then((list) => {
        // Respuesta inesperada (no-arreglo) = sin dato: el header nunca debe tumbar la venta.
        if (!cancelled) setRegisters(Array.isArray(list) ? list : []);
      })
      .catch(() => {
        if (!cancelled) setRegisters([]);
      });
    return () => {
      cancelled = true;
    };
  }, [cashRegisterId]);
  return registers;
}

function ContextItem({ label, value, code }: { label: string; value: string | null; code?: boolean }) {
  return (
    <span className="sf-ophead__item">
      <span className="sf-ophead__label">{label}</span>
      <span className={`sf-ophead__value${code ? " zh-font-mono" : ""}`}>{value ?? "—"}</span>
    </span>
  );
}

/**
 * POS-OPERATIONAL-HEADER-01 — header operativo compacto del POS (una franja sobre el layout):
 * pestañas Nueva Factura / Historial + contexto fiscal de la venta (sucursal, establecimiento,
 * punto de emisión, tipo de emisión, ambiente SRI si es electrónica) + estado de configuración +
 * botón Configuración (abre el mismo modal de siempre). Reemplaza la tarjeta grande del panel
 * izquierdo, que repetía estos datos y ocupaba alto que el cobro necesita.
 *
 * Solo presentación: tipo de emisión (ctx.emissionType), estado de configuración
 * (ctx.configStatus) y contexto (resolveSalesOperationalContext) vienen ya resueltos.
 */
export function SalesOperationalHeader({ ctx }: { ctx: SalesPageContext }) {
  const [configOpen, setConfigOpen] = useState(false);
  const registers = useSessionRegisters(ctx.myCashSession?.cashRegisterId);
  const operational = resolveSalesOperationalContext(ctx.editing, ctx.myCashSession, registers);
  const status = ctx.configStatus;
  const loading = ctx.hasCashSession === null;

  const tabs: ZHTab<"form" | "history">[] = [
    {
      id: "form",
      label: ctx.editing ? "Editar Factura" : "Nueva Factura",
      icon: "receipt_long",
      inert: true,
    },
    { id: "history", label: "Historial", icon: "history" },
  ];

  return (
    <div className="sf-ophead" role="region" aria-label="Contexto de la venta">
      <ZHTabBar
        tabs={tabs}
        activeTab="form"
        onChange={(id) => {
          if (id === "history") {
            void ctx.resetForm();
            ctx.setTab("listado");
          }
        }}
      />

      <div className="sf-ophead__context">
        {loading ? (
          <Badge variant="neutral" label="Cargando…" size="md" />
        ) : (
          <>
            <ContextItem label="Sucursal" value={ctx.branchName} />
            <ContextItem label="Establecimiento" value={operational.establishmentCode} code />
            <ContextItem label="Punto" value={operational.emissionPointCode} code />
            {ctx.emissionType && (
              <Badge
                variant={ctx.emissionType === "Electronic" ? "success" : "info"}
                label={ctx.emissionType === "Electronic" ? "Electrónica" : "Física"}
                size="md"
              />
            )}
            {ctx.isElectronic && <ElectronicEnvironmentBadge />}
            {status.level === "incomplete" && (
              <Badge
                variant="error"
                label="Falta configuración"
                size="md"
                title={status.missing.join(", ")}
              />
            )}
            {status.level === "review" && (
              <Badge
                variant="warning"
                label="Revisar configuración"
                size="md"
                title={status.warnings.join(" ")}
              />
            )}
          </>
        )}
      </div>

      <ZHBtn type="button" variant="secondary" size="sm" onClick={() => setConfigOpen(true)}>
        <span className="material-symbols-outlined zh-icon-md">tune</span>
        Configuración
      </ZHBtn>

      <SalesEmissionConfigModal
        ctx={ctx}
        status={status}
        operational={operational}
        open={configOpen}
        onClose={() => setConfigOpen(false)}
      />
    </div>
  );
}
