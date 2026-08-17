import type { CreditRow } from "../hooks/useSalesPage";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { formatMoney } from "../../../lib/sanitizers";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { INSTALLMENT_ROUNDING_TOLERANCE } from "../constants/tolerances";

interface Props {
  open: boolean;
  amount: number;
  rows: CreditRow[];
  paymentTermName?: string;
  installments?: number;
  daysBetween?: number;
  onRowsChange: (rows: CreditRow[]) => void;
  onRecalculate: () => void;
  onConfirm: (totalAmount: number) => void;
  onCancel: () => void;
}

export function CreditSimulatorModal({
  open,
  amount,
  rows,
  paymentTermName,
  installments,
  daysBetween,
  onRowsChange,
  onRecalculate,
  onConfirm,
  onCancel,
}: Props) {
  const totalAmountDecimals = getDecimalConfig().totalAmount;
  const factor = 10 ** totalAmountDecimals;
  const totalCuotas = rows.reduce((s, r) => s + r.amount, 0);
  const diff = Math.round((amount - totalCuotas) * factor) / factor;

  return (
    <ZHModal
      open={open}
      onClose={onCancel}
      size="md"
      title="Simulación de Cuotas"
      subtitle={`Monto a crédito: $${formatMoney(amount, totalAmountDecimals)}${paymentTermName && installments && daysBetween ? ` — ${paymentTermName} (${installments} cuota${installments > 1 ? "s" : ""} × ${daysBetween} días)` : ""}`}
      footer={
        <>
          <ZHBtn variant="ghost" size="md" onClick={onCancel}>
            Cancelar
          </ZHBtn>
          <ZHBtn variant="secondary" size="md" onClick={onRecalculate}>
            Recalcular
          </ZHBtn>
          <ZHBtn
            variant="primary"
            size="md"
            disabled={Math.abs(diff) > INSTALLMENT_ROUNDING_TOLERANCE}
            onClick={() => onConfirm(totalCuotas)}
          >
            Confirmar cuotas
          </ZHBtn>
        </>
      }
    >
      {diff !== 0 && (
        <ZHPageNotice
          variant="warning"
          message={`Diferencia: $${formatMoney(Math.abs(diff), totalAmountDecimals)} — las cuotas deben sumar $${formatMoney(amount, totalAmountDecimals)}`}
        />
      )}

      <table className="pf-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Vencimiento</th>
            <th className="zh-text-align-right">Monto</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row, idx) => (
            <tr key={row.number}>
              <td>{row.number}</td>
              <td>
                <ZhDateInput
                  value={row.dueDate}
                  onChange={(e) =>
                    onRowsChange(
                      rows.map((r, i) =>
                        i === idx ? { ...r, dueDate: e.target.value } : r,
                      ),
                    )
                  }
                />
              </td>
              <td className="zh-table-cell--num">
                <ZhDecimalInput
                  decimals={totalAmountDecimals}
                  positiveOnly
                  defaultValue={formatMoney(row.amount, totalAmountDecimals)}
                  onBlur={(e) =>
                    onRowsChange(
                      rows.map((r, i) =>
                        i === idx
                          ? { ...r, amount: Number(e.target.value) || 0 }
                          : r,
                      ),
                    )
                  }
                />
              </td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td colSpan={2} className="zh-text-align-right">
              <strong>Total cuotas</strong>
            </td>
            <td className="zh-table-cell--num">
              <ZHMoneyValue
                value={totalCuotas}
                decimals={totalAmountDecimals}
                emphasis="strong"
              />
            </td>
          </tr>
        </tfoot>
      </table>
    </ZHModal>
  );
}
