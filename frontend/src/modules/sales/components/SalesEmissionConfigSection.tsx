import { useState } from "react";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { Badge } from "../../../components/PageShell";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZhSelect } from "../../../components/zh/inputs";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import type { SalesPageContext } from "../hooks/useSalesPage";
import {
  computeSalesConfigStatus,
  type SalesConfigStatus,
} from "../utils/salesEmissionConfigStatus";

function buildSummaryLine(ctx: SalesPageContext): string | null {
  const docTypeCode = ctx.readOnly
    ? ctx.editing?.docTypeCode
    : ctx.formWatch.docTypeCode;
  const docTypeName =
    ctx.sriDocTypes.find((dt) => dt.code === docTypeCode)?.name ?? null;
  const parts = [
    docTypeName,
    ctx.myCashSession?.cashRegisterNameSnapshot ?? null,
    ctx.branchName ?? null,
  ].filter((p): p is string => !!p);
  return parts.length > 0 ? parts.join(" · ") : null;
}

function buildHintLine(ctx: SalesPageContext): string | null {
  const emissionType = ctx.myCashSession?.emissionType ?? ctx.editing?.emissionType;
  const emissionLabel =
    emissionType === "Electronic"
      ? "Emisión electrónica"
      : emissionType === "Physical"
        ? "Emisión física"
        : null;
  const pointCode = ctx.myCashSession?.emissionPointCodeSnapshot ?? null;
  const parts = [emissionLabel, pointCode ? `Punto ${pointCode}` : null].filter(
    (p): p is string => !!p,
  );
  return parts.length > 0 ? parts.join(" · ") : null;
}

// ── Compact card (main panel) ───────────────────────────────────────────
// SALES-POS-SILENT-OK-STATUS-AND-ALERTS-01: patrón "silencioso cuando todo está bien" — un
// estado "ready" no muestra ningún badge/aviso (nada que llame la atención sin motivo), solo el
// resumen. Solo "review"/"incomplete" muestran un ZHPageNotice (amarillo/rojo) con la causa,
// porque ahí sí hay algo que el cajero debería revisar o resolver.
export function SalesEmissionConfigSection({ ctx }: { ctx: SalesPageContext }) {
  const [open, setOpen] = useState(false);
  const loading = ctx.hasCashSession === null;
  const status = computeSalesConfigStatus(ctx);
  const summaryLine = buildSummaryLine(ctx);
  const hintLine = buildHintLine(ctx);

  return (
    <div className="sf-sidebar__section">
      <div className="sf-sidebar__header zh-section-title">
        <span className="material-symbols-outlined sf-sidebar__header-icon">
          apartment
        </span>
        Configuración de venta
        <ZHFieldHelp helpKey={HELP_KEYS.SALES_EMISSION_SECTION} />
      </div>
      <div className="sf-config-card">
        {loading ? (
          <Badge variant="neutral" label="Cargando…" size="md" />
        ) : (
          <>
            {summaryLine && (
              <div className="sf-config-card__summary">{summaryLine}</div>
            )}
            {hintLine && <div className="sf-config-card__hint">{hintLine}</div>}
            {status.level === "incomplete" && (
              <ZHPageNotice
                variant="error"
                message="Falta configuración"
                detail={status.missing.join(", ")}
              />
            )}
            {status.level === "review" && (
              <ZHPageNotice
                variant="warning"
                message="Revisar configuración"
                detail={status.warnings.join(" ")}
              />
            )}
          </>
        )}
        <div className="sf-config-card__actions">
          {/* SALES-POS-EMISSION-CONFIG-DUPLICATED-ACTIONS-01: un solo botón — no hay un modo
              "ver" distinto de un modo "editar", el modal ya decide por su cuenta qué campos
              quedan editables (ctx.readOnly/ctx.fieldDisabled, igual que antes) según el estado
              real del formulario. Dos botones para la misma acción solo duplicaba la decisión. */}
          <ZHBtn
            type="button"
            variant="secondary"
            size="sm"
            onClick={() => setOpen(true)}
          >
            Configuración
          </ZHBtn>
        </div>
      </div>
      <SalesEmissionConfigModal
        ctx={ctx}
        status={status}
        open={open}
        onClose={() => setOpen(false)}
      />
    </div>
  );
}

// ── Detail modal — mismos campos que antes vivían inline en el panel,
// leyendo/escribiendo exactamente el mismo form (ctx.formWatch/ctx.setValue) — sin segunda
// fuente de verdad. ──────────────────────────────────────────────────────
function SalesEmissionConfigModal({
  ctx,
  status,
  open,
  onClose,
}: {
  ctx: SalesPageContext;
  status: SalesConfigStatus;
  open: boolean;
  onClose: () => void;
}) {
  const emissionType = ctx.myCashSession?.emissionType ?? ctx.editing?.emissionType;

  return (
    <ZHModal
      open={open}
      onClose={onClose}
      size="md"
      title="Configuración de venta"
      subtitle="Sucursal, caja, punto de emisión, documento y forma de pago SRI por defecto."
      footer={
        <ZHBtn type="button" variant="primary" size="md" onClick={onClose}>
          Cerrar
        </ZHBtn>
      }
    >
      {/* SALES-POS-SILENT-OK-STATUS-AND-ALERTS-01: sin badge de estado "Lista" — si todo está OK
          no hay nada que avisar, el modal solo muestra los datos. Los avisos (missing/warnings)
          siguen apareciendo tal cual cuando corresponde. */}
      {status.missing.length > 0 && (
        <ZHPageNotice
          variant="error"
          message={`Falta para poder emitir: ${status.missing.join(", ")}.`}
        />
      )}
      {status.warnings.length > 0 && (
        <ZHPageNotice variant="warning" message={status.warnings.join(" ")} />
      )}

      <div className="sf-emission">
        {ctx.branchName && (
          <div>
            <ZHFieldLabel size="sm" className="sf-emission__label">
              {"Sucursal:"}
            </ZHFieldLabel>
            <span className="sf-emission__value">{ctx.branchName}</span>
          </div>
        )}
        {ctx.myCashSession && (
          <div>
            <ZHFieldLabel size="sm" className="sf-emission__label">
              {"Caja:"}
            </ZHFieldLabel>
            <ZHFieldHelp helpKey={HELP_KEYS.SALES_CASH_SESSION} />
            <span className="sf-emission__value">
              {ctx.myCashSession.cashRegisterCodeSnapshot} —{" "}
              {ctx.myCashSession.cashRegisterNameSnapshot}
            </span>
          </div>
        )}
        {ctx.myCashSession && (
          <div>
            <ZHFieldLabel size="sm" className="sf-emission__label">
              {"Punto:"}
            </ZHFieldLabel>
            <span className="sf-emission__value">
              {ctx.myCashSession.emissionPointCodeSnapshot}
            </span>
          </div>
        )}
        {emissionType && (
          <div>
            <ZHFieldLabel size="sm" className="sf-emission__label">
              {"Tipo Emisión:"}
            </ZHFieldLabel>
            <ZHFieldHelp helpKey={HELP_KEYS.SALES_EMISSION_TYPE} />
            <Badge
              variant={emissionType === "Electronic" ? "success" : "info"}
              label={emissionType === "Electronic" ? "Electrónica" : "Física"}
              size="md"
            />
          </div>
        )}
        <div className="sf-emission__full">
          <ZHFieldLabel size="sm" className="sf-emission__label">
            Tipo Documento
          </ZHFieldLabel>
          <ZhSelect
            className="zh-select--compact zh-mb-4"
            value={
              ctx.readOnly
                ? (ctx.editing?.docTypeCode ?? "")
                : ctx.formWatch.docTypeCode
            }
            onChange={(e) => ctx.setValue("docTypeCode", e.target.value)}
            disabled={ctx.fieldDisabled}
            title={
              ctx.sriDocTypes.find(
                (dt) =>
                  dt.code ===
                  (ctx.readOnly
                    ? (ctx.editing?.docTypeCode ?? "")
                    : ctx.formWatch.docTypeCode),
              )?.name
            }
          >
            {ctx.sriDocTypes.map((dt) => (
              <option key={dt.code} value={dt.code} title={dt.name}>
                {dt.code} — {dt.name}
              </option>
            ))}
          </ZhSelect>
        </div>
        <div className="sf-emission__full">
          <ZHFieldLabel size="sm" className="sf-emission__label">
            Forma Pago SRI por Defecto
          </ZHFieldLabel>
          <ZHFieldHelp helpKey={HELP_KEYS.SALES_SRI_PAYMENT_METHOD_DEFAULT} />
          <ZhSelect
            className="zh-select--compact zh-mb-4"
            value={
              ctx.readOnly
                ? (ctx.editing?.sriPaymentMethodCode ?? "")
                : ctx.formWatch.sriPaymentMethodCode
            }
            onChange={(e) => ctx.setValue("sriPaymentMethodCode", e.target.value)}
            disabled={ctx.fieldDisabled}
            title={
              ctx.sriPaymentMethods.find(
                (pm) =>
                  pm.code ===
                  (ctx.readOnly
                    ? (ctx.editing?.sriPaymentMethodCode ?? "")
                    : ctx.formWatch.sriPaymentMethodCode),
              )?.name
            }
          >
            {ctx.sriPaymentMethods.map((pm) => (
              <option key={pm.code} value={pm.code} title={pm.name}>
                {pm.code} — {pm.name}
              </option>
            ))}
          </ZhSelect>
        </div>
        {ctx.editing && (
          <div className="zh-mt-4">
            <ZHFieldLabel size="sm" className="sf-emission__label">
              {"Nro:"}
            </ZHFieldLabel>
            <span className="sf-emission__value zh-font-mono">
              {ctx.editing.invoiceNumber}
            </span>
          </div>
        )}
      </div>
    </ZHModal>
  );
}
