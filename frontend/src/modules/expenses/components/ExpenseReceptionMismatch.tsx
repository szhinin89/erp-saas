import { useEffect, useRef } from "react";
import { ZHCompactNotice } from "../../../components/zh/notices";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { useI18n } from "../../../i18n/i18n";
import { formatDecimalDisplay } from "../../../lib/sanitizers";
import { message } from "../../../lib/messages";
import { buildNotice } from "../../../notices";

/** Diferencia entre el total calculado en pantalla y el total del XML recibido. */
export interface ReceptionMismatch {
  calculatedTotal: number;
  receivedTotal: number;
  receivedVat: number;
  diff: number;
}

/**
 * EXPENSE-UI-02 — advertencia compacta de diferencia de importes contra el XML recibido.
 * Solo presentación: la regla que bloquea el guardado sigue siendo la de la página (no cambia).
 *
 * - Indicador persistente (`ZHCompactNotice`: insignia + detalle con total XML, IVA XML y total
 *   calculado) desde que existe la diferencia; desaparece solo cuando los importes coinciden.
 * - Toast oficial (`message.warning`) únicamente tras una interacción relevante: cuando el usuario ya
 *   ingresó importes (total calculado > 0) y siguen sin coincidir. Al abrir el gasto, con los
 *   conceptos aún sin completar (total 0), solo se ve el indicador. El aviso sale UNA vez por
 *   aparición de la diferencia: no depende de los importes concretos, así que renders y pulsaciones
 *   no lo repiten; si la diferencia se resuelve y reaparece, avisa de nuevo. Intentar guardar con
 *   diferencia ya lo cubre `handleSave` de la página con su propio mensaje de error.
 */
export function ExpenseReceptionMismatchNotice({
  mismatch,
}: {
  mismatch: ReceptionMismatch | null;
}) {
  const { t } = useI18n();
  const moneyDecimals = usePrecisionDecimals("money");
  const taxDecimals = usePrecisionDecimals("tax");
  const tRef = useRef(t);
  useEffect(() => {
    tRef.current = t;
  }, [t]);

  const hasMismatch = mismatch !== null;
  const hasEnteredAmounts = mismatch !== null && mismatch.calculatedTotal > 0;
  const notified = useRef(false);
  useEffect(() => {
    if (!hasMismatch) notified.current = false;
  }, [hasMismatch]);
  useEffect(() => {
    if (hasMismatch && hasEnteredAmounts && !notified.current) {
      notified.current = true;
      message.warning(tRef.current("expenses.form.reception.mismatchTitle"));
    }
  }, [hasMismatch, hasEnteredAmounts]);

  if (!mismatch) return null;
  const notice = buildNotice({
    severity: "warning",
    intent: "actionRequired",
    source: "domain-status",
    label: t("expenses.form.reception.mismatchBadge"),
    detail: t("expenses.form.reception.mismatchDetail", {
      vat: formatDecimalDisplay(mismatch.receivedVat, taxDecimals),
      received: formatDecimalDisplay(mismatch.receivedTotal, moneyDecimals),
      calculated: formatDecimalDisplay(mismatch.calculatedTotal, moneyDecimals),
    }),
  });
  return (
    <div className="exp-doc-mismatch">
      <ZHCompactNotice notice={notice} anchored />
    </div>
  );
}
