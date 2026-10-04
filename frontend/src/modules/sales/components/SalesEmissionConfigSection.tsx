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
import type { SalesConfigStatus } from "../utils/salesEmissionConfigStatus";

function buildSummaryLine(ctx: SalesPageContext): string | null {
  const docTypeCode = ctx.readOnly
    ? ctx.editing?.docTypeCode
    : ctx.formWatch.docTypeCode;
  const docTypeName =
    ctx.sriDocTypes.find((dt) => dt.code === docTypeCode)?.name ?? null;
  const parts = [
    docTypeName,
    belongsToCurrentSession(ctx) ? (ctx.myCashSession?.cashRegisterNameSnapshot ?? null) : null,
    ctx.branchName ?? null,
  ].filter((p): p is string => !!p);
  return parts.length > 0 ? parts.join(" · ") : null;
}

/** La venta en pantalla pertenece a la caja abierta actual (venta nueva o factura de esa misma
 * sesión) — solo entonces los datos de la caja actual describen a la venta. */
function belongsToCurrentSession(ctx: SalesPageContext): boolean {
  return !ctx.editing || ctx.editing.cashSessionId === ctx.myCashSession?.id;
}

const EMISSION_TYPE_LABEL: Record<string, string> = {
  Electronic: "Emisión electrónica",
  Physical: "Emisión física",
};

/** POS-EMISSION-TYPE-SNAPSHOT-01: código del punto de emisión de la venta en pantalla. Venta
 * nueva o del mismo punto que la caja → snapshot de la caja; factura de OTRO punto (histórica
 * abierta desde otra caja) → segmento "EEE-PPP-" de su número definitivo, nunca el de la caja
 * actual. Un borrador de otro punto aún no tiene número definitivo → sin código. */
function resolvePointCode(ctx: SalesPageContext): string | null {
  const session = ctx.myCashSession;
  if (!ctx.editing) return session?.emissionPointCodeSnapshot ?? null;
  if (session && ctx.editing.emissionPointId === session.emissionPointId)
    return session.emissionPointCodeSnapshot;
  return /^\d{3}-(\d{3})-\d+$/.exec(ctx.editing.invoiceNumber)?.[1] ?? null;
}

function buildHintLine(ctx: SalesPageContext): string | null {
  const emissionLabel = ctx.emissionType
    ? (EMISSION_TYPE_LABEL[ctx.emissionType] ?? null)
    : null;
  const pointCode = resolvePointCode(ctx);
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
// POS-CONFIG-STATUS-SEVERITY-01: el estado viene de ctx.configStatus (calculado UNA vez en el
// hook): rojo ⇔ bloquea Emitir (forma parte de ctx.emitBlockers); amarillo = revisar sin
// bloquear; info nunca se pinta como alerta en la tarjeta.
export function SalesEmissionConfigSection({ ctx }: { ctx: SalesPageContext }) {
  const [open, setOpen] = useState(false);
  const loading = ctx.hasCashSession === null;
  const status = ctx.configStatus;
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
  const emissionType = ctx.emissionType;
  const pointCode = resolvePointCode(ctx);
  const infos = status.issues.filter((i) => i.severity === "info").map((i) => i.message);

  return (
    <ZHModal
      open={open}
      onClose={onClose}
      size="md"
      title="Configuración de venta"
      subtitle={
        ctx.isElectronic
          ? "Sucursal, caja, punto de emisión, documento y forma de pago SRI por defecto."
          : "Sucursal, caja, punto de emisión y documento."
      }
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
      {infos.length > 0 && <ZHPageNotice variant="info" message={infos.join(" ")} />}

      <div className="sf-emission">
        {ctx.branchName && (
          <div>
            <ZHFieldLabel size="sm" className="sf-emission__label">
              {"Sucursal:"}
            </ZHFieldLabel>
            <span className="sf-emission__value">{ctx.branchName}</span>
          </div>
        )}
        {ctx.myCashSession && belongsToCurrentSession(ctx) && (
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
        {pointCode && (
          <div>
            <ZHFieldLabel size="sm" className="sf-emission__label">
              {"Punto:"}
            </ZHFieldLabel>
            <span className="sf-emission__value">{pointCode}</span>
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
        {/* POS-EMISSION-VISIBILITY-01: la Forma de pago SRI solo existe en el XML electrónico —
            una factura física nunca se vuelve electrónica (snapshot inmutable), así que en
            física no se muestra ni se exige. */}
        {ctx.isElectronic && (
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
        )}
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
