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
import { ZhSelect } from "../../../components/zh/inputs";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { formatMoney } from "../../../lib/sanitizers";
import { getPrecisionPolicy } from "../../../lib/config/precisionPolicy.config";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { PAYMENT_DETAIL_TOLERANCE } from "../constants/tolerances";
import { deriveDetailReference } from "../utils/paymentDetailReference";

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
  /** SALES-TRANSFER-PAYMENT-REFERENCE-PAYLOAD-01: viene de PaymentMethodDto.requiresReference —
   * cuando es true, bloquea "Confirmar" hasta que cada fila con monto tenga comprobante/
   * autorización/número de cheque capturado (el backend rechaza la emisión sin esto). */
  requiresReference: boolean;
  /** SALES-TRANSFER-BANK-ACCOUNT-01: cuentas bancarias activas de la empresa (Banco + alias +
   * tipo/número enmascarado) — solo se usa cuando detailType === "Transfer". */
  bankAccountOptions: { id: string; label: string }[];
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
  requiresReference,
  bankAccountOptions,
  initialRows,
  initialKey,
  available,
  onConfirm,
  onCancel,
}: Props) {
  const [rows, setRows] = useState<DetailRow[]>(initialRows);
  const [nextKey, setNextKey] = useState(initialKey);
  // Montaje del input editable (defaultValue): escala de money de la policy.
  const totalAmountDecimals = getPrecisionPolicy().moneyDecimals;
  // Presentación de textos compuestos (subtítulo/mensaje): semántica declarada (04E).
  const moneyDecimals = usePrecisionDecimals("money");

  const isCard = detailType === "Card";
  const isTransfer = detailType === "Transfer";
  const isCheque = detailType === "Check";

  const totalDetail = rows.reduce((s, r) => s + (r.amount || 0), 0);
  const exceeds = totalDetail > available + PAYMENT_DETAIL_TOLERANCE;
  // SALES-TRANSFER-PAYMENT-REFERENCE-PAYLOAD-01: bloquea antes de emitir — nunca deja que una fila
  // con monto capturado llegue a onConfirm sin la referencia que el backend exige para este
  // método, en vez de dejar que el rechazo ocurra recién al intentar emitir la factura.
  const missingReference =
    requiresReference &&
    rows.some((r) => r.amount > 0 && !deriveDetailReference(detailType, r));
  // SALES-TRANSFER-BANK-ACCOUNT-01: Transferencia exige seleccionar una cuenta bancaria destino —
  // ninguna fila con monto puede confirmarse sin ella (Banco texto libre ya no existe).
  const missingBankAccount =
    isTransfer && rows.some((r) => r.amount > 0 && !r.transfer?.companyBankAccountId);
  const missingTransferDate =
    isTransfer && rows.some((r) => r.amount > 0 && !r.transfer?.transferDate);

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
      subtitle={`Disponible: $${formatMoney(available, moneyDecimals)}`}
      footer={
        <>
          <div className="zh-modal-footer-summary">
            <span className="zh-modal-footer-label">
              Total:{" "}
              <ZHMoneyValue
                value={totalDetail}
                precision="money"
                emphasis="strong"
              />
            </span>
          </div>
          <ZHBtn variant="ghost" size="md" onClick={onCancel}>
            Cancelar
          </ZHBtn>
          <ZHBtn
            variant="primary"
            size="md"
            disabled={
              rows.length === 0 ||
              rows.some((r) => r.amount <= 0) ||
              exceeds ||
              missingReference ||
              missingBankAccount ||
              missingTransferDate
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
          message={`Excede el saldo disponible ($${formatMoney(available, moneyDecimals)}) por $${formatMoney(totalDetail - available, moneyDecimals)}`}
        />
      )}

      {missingBankAccount && (
        <ZHPageNotice
          variant="error"
          message={`El método "${methodName}" requiere seleccionar una cuenta bancaria destino en cada fila antes de confirmar.`}
        />
      )}

      {missingTransferDate && (
        <ZHPageNotice
          variant="error"
          message={`El método "${methodName}" requiere la fecha de operación en cada fila antes de confirmar.`}
        />
      )}

      {missingReference && (
        <ZHPageNotice
          variant="error"
          message={`El método "${methodName}" requiere un comprobante/referencia. Complete ${
            isTransfer ? "el Comprobante" : isCheque ? "el Nro. Cheque" : "la Autoriz."
          } en cada fila antes de confirmar.`}
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
            {isTransfer ? (
              <div className="pdt-field pdt-field--grow">
                <ZHFieldLabel size="sm" className="pdt-label">
                  Cuenta bancaria destino
                </ZHFieldLabel>
                <ZhSelect
                  value={row.transfer?.companyBankAccountId ?? ""}
                  onChange={(e) => {
                    const v = e.target.value;
                    upd(row._k, (r) => ({
                      ...r,
                      transfer: { ...r.transfer, companyBankAccountId: v || undefined },
                    }));
                  }}
                >
                  <option value="">Seleccione una cuenta bancaria</option>
                  {bankAccountOptions.map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.label}
                    </option>
                  ))}
                </ZhSelect>
              </div>
            ) : (
              <div className="pdt-field pdt-field--grow">
                <ZHFieldLabel size="sm" className="pdt-label">
                  Banco
                </ZHFieldLabel>
                <ZhTextInput
                  placeholder="Banco"
                  value={(isCard ? row.card?.bankName : row.cheque?.bankName) ?? ""}
                  onChange={(e) => {
                    const v = e.target.value;
                    upd(row._k, (r) =>
                      isCard
                        ? { ...r, card: { ...r.card, bankName: v } }
                        : { ...r, cheque: { ...r.cheque, bankName: v } },
                    );
                  }}
                />
              </div>
            )}
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
                precision="money"
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
