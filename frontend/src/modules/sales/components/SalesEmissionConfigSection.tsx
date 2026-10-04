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
import type { SalesOperationalContext } from "../utils/salesOperationalContext";

// POS-OPERATIONAL-HEADER-01: la tarjeta grande "Configuración de venta" del panel izquierdo se
// retiró — su resumen (sucursal / establecimiento / punto / tipo de emisión) y su estado viven
// ahora en el header operativo compacto (SalesOperationalHeader), que abre ESTE mismo modal con
// el botón "Configuración". El modal no cambia: mismos campos, mismo form (ctx.formWatch /
// ctx.setValue), sin segunda fuente de verdad.

// ── Detail modal ─────────────────────────────────────────────────────────
export function SalesEmissionConfigModal({
  ctx,
  status,
  operational,
  open,
  onClose,
}: {
  ctx: SalesPageContext;
  status: SalesConfigStatus;
  operational: SalesOperationalContext;
  open: boolean;
  onClose: () => void;
}) {
  const emissionType = ctx.emissionType;
  const pointCode = operational.emissionPointCode;
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
        {operational.establishmentCode && (
          <div>
            <ZHFieldLabel size="sm" className="sf-emission__label">
              {"Establecimiento:"}
            </ZHFieldLabel>
            <span className="sf-emission__value">{operational.establishmentCode}</span>
          </div>
        )}
        {ctx.myCashSession && operational.fromCurrentSession && (
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
