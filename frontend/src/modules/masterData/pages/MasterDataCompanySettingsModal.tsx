import { useState } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import {
  ZHBtn,
  ZHField,
  ZHGrid,
  ZHFormActions,
} from "../../../components/zh/ZHForm";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhNumberInput, ZhDecimalInput } from "../../../components/zh/inputs";
import type {
  BusinessPartnerSummaryDto,
  CompanyBpTradingSettingsDto,
} from "../types/businessPartner.types";

const tradingSettingsSchema = z.object({
  paymentDays: z.coerce
    .number({ invalid_type_error: "Los días de pago son requeridos." })
    .int()
    .min(0, "Los días de pago deben ser 0 o mayor."),
  creditLimit: z.coerce
    .number({ invalid_type_error: "El límite de crédito es requerido." })
    .min(0, "El límite de crédito debe ser 0 o mayor."),
  currencyCode: z
    .string()
    .length(3, "La moneda debe tener exactamente 3 caracteres (ej: USD)."),
});

type TradingSettingsValues = z.infer<typeof tradingSettingsSchema>;

type Props = {
  partner: BusinessPartnerSummaryDto;
  initialSettings?: CompanyBpTradingSettingsDto | null;
  saving: boolean;
  error?: string | null;
  onClose: () => void;
  onSave: (payload: {
    creditLimit: number;
    paymentDays: number;
    creditCurrencyCode: string;
  }) => Promise<void>;
  onBlock: (reason: string) => void;
  onUnblock: () => void;
};

export function MasterDataCompanySettingsModal({
  partner,
  initialSettings,
  saving,
  error,
  onClose,
  onSave,
  onBlock,
  onUnblock,
}: Props) {
  const [formError, setFormError] = useState<string | null>(null);
  const [showBlockForm, setShowBlockForm] = useState(false);
  const [blockReason, setBlockReason] = useState("");

  const isBlocked = initialSettings?.isBlocked ?? false;

  const {
    handleSubmit,
    control,
    setError,
    formState: { errors },
  } = useForm<TradingSettingsValues>({
    resolver: zodResolver(tradingSettingsSchema),
    defaultValues: {
      paymentDays: initialSettings?.paymentDays ?? 30,
      creditLimit: initialSettings?.creditLimit ?? 0,
      currencyCode: initialSettings?.creditCurrencyCode ?? "USD",
    },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await onSave({
        paymentDays: values.paymentDays,
        creditLimit: values.creditLimit,
        creditCurrencyCode: values.currencyCode,
      });
    } catch (err) {
      const applied = applyServerErrors(err, setError, (msg) =>
        setFormError(msg),
      );
      if (!applied)
        setFormError(
          formatApiRequestError(err, {
            generic: "Error al guardar las condiciones comerciales.",
          }),
        );
    }
  });

  const handleBlock = async (e: React.FormEvent) => {
    e.preventDefault();
    const reason = blockReason.trim();
    if (!reason) return;

    const confirmed = await message.confirm({
      title: "Bloquear en esta empresa",
      message: `"${partner.legalName}" quedará bloqueado en esta empresa: no podrá operar (nuevas ventas/compras, nuevos documentos) hasta que se desbloquee. Motivo: "${reason}".`,
      variant: "danger",
      confirmLabel: "Bloquear",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;

    onBlock(reason);
    setShowBlockForm(false);
    setBlockReason("");
  };

  const handleUnblock = async () => {
    const confirmed = await message.confirm({
      title: "Desbloquear",
      message: `"${partner.legalName}" volverá a poder operar normalmente en esta empresa (nuevas ventas/compras, nuevos documentos).`,
      variant: "warning",
      confirmLabel: "Desbloquear",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;
    onUnblock();
  };

  return (
    <ZHModal
      open
      onClose={onClose}
      title="Condiciones por empresa"
      subtitle={`${partner.legalName} · ${partner.identificationNumber}`}
      closeOnBackdrop={false}
    >
      <form onSubmit={onSubmit}>
        {(error || formError) && (
          <ZHPageNotice variant="error" message={error ?? formError!} />
        )}

        {isBlocked && (
          <ZHPageNotice
            variant="error"
            message={`BLOQUEADO${initialSettings?.blockedReason ? ` — ${initialSettings.blockedReason}` : ""}`}
          />
        )}

        <ZHGrid cols={2}>
          <ZHField
            label="Días de pago"
            required
            fieldError={errors.paymentDays?.message}
          >
            <Controller
              name="paymentDays"
              control={control}
              render={({ field }) => (
                <ZhNumberInput
                  className="zh-input"
                  positiveOnly
                  value={String(field.value ?? "")}
                  onChange={(e) => field.onChange(e.target.value)}
                  disabled={saving}
                />
              )}
            />
          </ZHField>
          <ZHField
            label="Límite de crédito"
            fieldError={errors.creditLimit?.message}
          >
            <Controller
              name="creditLimit"
              control={control}
              render={({ field }) => (
                <ZhDecimalInput
                  className="zh-input"
                  decimals={2}
                  positiveOnly
                  value={String(field.value ?? "")}
                  onChange={(e) => field.onChange(e.target.value)}
                  disabled={saving}
                />
              )}
            />
          </ZHField>
          <ZHField label="Moneda" fieldError={errors.currencyCode?.message}>
            <Controller
              name="currencyCode"
              control={control}
              render={({ field }) => (
                <input
                  className="zh-input mono"
                  maxLength={3}
                  {...field}
                  onChange={(e) => field.onChange(e.target.value.toUpperCase())}
                  disabled={saving}
                />
              )}
            />
          </ZHField>
        </ZHGrid>

        {/* ── Block section ─────────────────────────────────────────────── */}
        {!isBlocked && !showBlockForm && (
          <div className="md-block-row">
            <ZHBtn
              variant="destructive"
              size="sm"
              type="button"
              disabled={saving}
              onClick={() => setShowBlockForm(true)}
            >
              Bloquear en esta empresa
            </ZHBtn>
          </div>
        )}

        {!isBlocked && showBlockForm && (
          <div className="md-block-form">
            <ZHField label="Motivo del bloqueo" required>
              <input
                className="zh-input"
                value={blockReason}
                onChange={(e) => setBlockReason(e.target.value)}
                placeholder="Ej: Deuda vencida, incumplimiento de contrato..."
                maxLength={500}
                disabled={saving}
                autoFocus
              />
            </ZHField>
            <div className="zh-form-actions-row zh-form-actions-row--end">
              <ZHBtn
                variant="ghost"
                size="sm"
                type="button"
                onClick={() => setShowBlockForm(false)}
                disabled={saving}
              >
                Cancelar
              </ZHBtn>
              <ZHBtn
                variant="destructive"
                size="sm"
                type="button"
                onClick={(e) => void handleBlock(e)}
                disabled={saving || !blockReason.trim()}
              >
                Confirmar bloqueo
              </ZHBtn>
            </div>
          </div>
        )}

        {isBlocked && (
          <div className="md-block-row">
            <ZHBtn
              variant="secondary"
              size="sm"
              type="button"
              disabled={saving}
              onClick={() => void handleUnblock()}
            >
              {saving ? "Desbloqueando..." : "Desbloquear"}
            </ZHBtn>
          </div>
        )}

        <ZHFormActions
          onCancel={onClose}
          hideDraft
          saveButtonType="submit"
          disableSave={saving}
          labels={{
            cancel: "Cerrar",
            save: saving ? "Guardando..." : "Guardar condiciones",
          }}
        />
      </form>
    </ZHModal>
  );
}
