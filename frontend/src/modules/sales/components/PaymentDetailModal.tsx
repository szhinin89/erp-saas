import { useState } from "react";
import type {
  CardDetailInput,
  TransferDetailInput,
  ChequeDetailInput,
  PaymentMethodDetailType,
} from "../api/salesService";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { formatMoney } from "../../../lib/sanitizers";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { PAYMENT_DETAIL_TOLERANCE } from "../constants/tolerances";

type DetailRow = {
  _k: number;
  amount: number;
  card?: CardDetailInput;
  transfer?: TransferDetailInput;
  cheque?: ChequeDetailInput;
};

interface Props {
  open: boolean;
  methodName: string;
  detailType: PaymentMethodDetailType;
  initialRows: DetailRow[];
  initialKey: number;
  available: number;
  onConfirm: (rows: DetailRow[]) => void;
  onCancel: () => void;
}

export function PaymentDetailModal({
  open,
  methodName,
  detailType,
  initialRows,
  initialKey,
  available,
  onConfirm,
  onCancel,
}: Props) {
  const [rows, setRows] = useState<DetailRow[]>(initialRows);
  const [nextKey, setNextKey] = useState(initialKey);
  const totalAmountDecimals = getDecimalConfig().totalAmount;

  const isCard = detailType === "Card";
  const isTransfer = detailType === "Transfer";
  const isCheque = detailType === "Check";

  const totalDetail = rows.reduce((s, r) => s + (r.amount || 0), 0);
  const exceeds = totalDetail > available + PAYMENT_DETAIL_TOLERANCE;

  const addRow = () => {
    const newRow: DetailRow = {
      _k: nextKey,
      amount: 0,
      card: isCard ? {} : undefined,
      transfer: isTransfer ? {} : undefined,
      cheque: isCheque ? {} : undefined,
    };
    setRows((prev) => [...prev, newRow]);
    setNextKey((k) => k + 1);
  };

  const upd = (k: number, fn: (r: DetailRow) => DetailRow) =>
    setRows((prev) => prev.map((r) => (r._k === k ? fn(r) : r)));

  return (
    <ZHModal
      open={open}
      onClose={onCancel}
      size={isCard ? "lg" : "md"}
      title={methodName}
      subtitle={`Disponible: $${formatMoney(available, totalAmountDecimals)}`}
      footer={
        <>
          <div className="zh-modal-footer-summary">
            <span className="zh-modal-footer-label">
              Total:{" "}
              <strong>${formatMoney(totalDetail, totalAmountDecimals)}</strong>
            </span>
          </div>
          <ZHBtn variant="ghost" size="md" onClick={onCancel}>
            Cancelar
          </ZHBtn>
          <ZHBtn
            variant="primary"
            size="md"
            disabled={
              rows.length === 0 || rows.some((r) => r.amount <= 0) || exceeds
            }
            onClick={() => onConfirm(rows.filter((r) => r.amount > 0))}
          >
            Confirmar ({rows.filter((r) => r.amount > 0).length})
          </ZHBtn>
        </>
      }
    >
      {exceeds && (
        <ZHPageNotice
          variant="error"
          message={`Excede el saldo disponible ($${formatMoney(available, totalAmountDecimals)}) por $${formatMoney(totalDetail - available, totalAmountDecimals)}`}
        />
      )}

      <div className="pdt-add-row">
        <ZHBtn variant="secondary" size="sm" onClick={addRow}>
          + Agregar
        </ZHBtn>
      </div>

      {rows.length === 0 && (
        <div className="empty-state">
          Haga click en &quot;+ Agregar&quot; para registrar un{" "}
          {methodName.toLowerCase()}
        </div>
      )}

      {rows.map((row) => (
        <div key={row._k} className="pdt-row-card">
          <ZHIconButton
            icon="delete"
            title="Eliminar"
            variant="danger"
            className="pdt-row-remove"
            onClick={() =>
              setRows((prev) => prev.filter((r) => r._k !== row._k))
            }
          />
          <div className="pdt-row-fields">
            <div className="pdt-field pdt-field--grow">
              <ZHFieldLabel size="sm" className="pdt-label">
                Banco
              </ZHFieldLabel>
              <ZhTextInput
                placeholder="Banco"
                value={
                  (isCard
                    ? row.card?.bankName
                    : isTransfer
                      ? row.transfer?.bankName
                      : row.cheque?.bankName) ?? ""
                }
                onChange={(e) => {
                  const v = e.target.value;
                  upd(row._k, (r) =>
                    isCard
                      ? { ...r, card: { ...r.card, bankName: v } }
                      : isTransfer
                        ? { ...r, transfer: { ...r.transfer, bankName: v } }
                        : { ...r, cheque: { ...r.cheque, bankName: v } },
                  );
                }}
              />
            </div>
            {isCard && (
              <>
                <div className="pdt-field pdt-field--grow">
                  <ZHFieldLabel size="sm" className="pdt-label">
                    Marca
                  </ZHFieldLabel>
                  <ZhTextInput
                    placeholder="Visa"
                    value={row.card?.cardBrand ?? ""}
                    onChange={(e) =>
                      upd(row._k, (r) => ({
                        ...r,
                        card: { ...r.card, cardBrand: e.target.value },
                      }))
                    }
                  />
                </div>
                <div className="pdt-field pdt-field--w60">
                  <ZHFieldLabel size="sm" className="pdt-label">
                    Last4
                  </ZHFieldLabel>
                  <ZhTextInput
                    maxLength={4}
                    placeholder="1234"
                    value={row.card?.cardLastFour ?? ""}
                    onChange={(e) =>
                      upd(row._k, (r) => ({
                        ...r,
                        card: {
                          ...r.card,
                          cardLastFour: e.target.value
                            .replace(/\D/g, "")
                            .slice(0, 4),
                        },
                      }))
                    }
                  />
                </div>
                <div className="pdt-field pdt-field--grow">
                  <ZHFieldLabel size="sm" className="pdt-label">
                    Autoriz.
                  </ZHFieldLabel>
                  <ZhTextInput
                    placeholder="AUTH01"
                    value={row.card?.authorizationCode ?? ""}
                    onChange={(e) =>
                      upd(row._k, (r) => ({
                        ...r,
                        card: { ...r.card, authorizationCode: e.target.value },
                      }))
                    }
                  />
                </div>
                <div className="pdt-field pdt-field--w60">
                  <ZHFieldLabel size="sm" className="pdt-label">
                    Lote
                  </ZHFieldLabel>
                  <ZhTextInput
                    placeholder="001"
                    value={row.card?.lotNumber ?? ""}
                    onChange={(e) =>
                      upd(row._k, (r) => ({
                        ...r,
                        card: { ...r.card, lotNumber: e.target.value },
                      }))
                    }
                  />
                </div>
              </>
            )}
            {isTransfer && (
              <>
                <div className="pdt-field pdt-field--grow">
                  <ZHFieldLabel size="sm" className="pdt-label">
                    Comprobante
                  </ZHFieldLabel>
                  <ZhTextInput
                    placeholder="TRX-001"
                    value={row.transfer?.receiptNumber ?? ""}
                    onChange={(e) =>
                      upd(row._k, (r) => ({
                        ...r,
                        transfer: {
                          ...r.transfer,
                          receiptNumber: e.target.value,
                        },
                      }))
                    }
                  />
                </div>
                <div className="pdt-field pdt-field--w120">
                  <ZHFieldLabel size="sm" className="pdt-label">
                    Fecha
                  </ZHFieldLabel>
                  <ZhDateInput
                    value={row.transfer?.transferDate ?? ""}
                    onChange={(e) =>
                      upd(row._k, (r) => ({
                        ...r,
                        transfer: {
                          ...r.transfer,
                          transferDate: e.target.value,
                        },
                      }))
                    }
                  />
                </div>
              </>
            )}
            {isCheque && (
              <>
                <div className="pdt-field pdt-field--grow">
                  <ZHFieldLabel size="sm" className="pdt-label">
                    Nro. Cheque
                  </ZHFieldLabel>
                  <ZhTextInput
                    placeholder="001234"
                    value={row.cheque?.chequeNumber ?? ""}
                    onChange={(e) =>
                      upd(row._k, (r) => ({
                        ...r,
                        cheque: { ...r.cheque, chequeNumber: e.target.value },
                      }))
                    }
                  />
                </div>
                <div className="pdt-field pdt-field--grow">
                  <ZHFieldLabel size="sm" className="pdt-label">
                    Titular
                  </ZHFieldLabel>
                  <ZhTextInput
                    placeholder="Nombre"
                    value={row.cheque?.holderName ?? ""}
                    onChange={(e) =>
                      upd(row._k, (r) => ({
                        ...r,
                        cheque: { ...r.cheque, holderName: e.target.value },
                      }))
                    }
                  />
                </div>
                <div className="pdt-field pdt-field--w120">
                  <ZHFieldLabel size="sm" className="pdt-label">
                    Fecha cobro
                  </ZHFieldLabel>
                  <ZhDateInput
                    value={row.cheque?.cashDate ?? ""}
                    onChange={(e) =>
                      upd(row._k, (r) => ({
                        ...r,
                        cheque: { ...r.cheque, cashDate: e.target.value },
                      }))
                    }
                  />
                </div>
              </>
            )}
            <div className="pdt-field pdt-field--w90">
              <ZHFieldLabel size="sm" className="pdt-label">
                Monto
              </ZHFieldLabel>
              <ZhDecimalInput
                decimals={totalAmountDecimals}
                positiveOnly
                defaultValue={
                  row.amount > 0
                    ? formatMoney(row.amount, totalAmountDecimals)
                    : ""
                }
                placeholder="0.00"
                onBlur={(e) =>
                  upd(row._k, (r) => ({
                    ...r,
                    amount: Number(e.target.value) || 0,
                  }))
                }
              />
            </div>
          </div>
        </div>
      ))}
    </ZHModal>
  );
}
