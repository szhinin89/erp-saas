import { useState } from 'react';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZhNumberInput, ZhDecimalInput } from '../../../components/zh/inputs';
import type { BusinessPartnerSummaryDto, CompanyBpTradingSettingsDto } from '../types/businessPartner.types';

type Props = {
  partner:         BusinessPartnerSummaryDto;
  initialSettings?: CompanyBpTradingSettingsDto | null;
  saving:          boolean;
  error?:          string | null;
  onClose:         () => void;
  onSave:  (payload: { creditLimit: number; paymentDays: number; creditCurrencyCode: string }) => void;
  onBlock:   (reason: string) => void;
  onUnblock: () => void;
};

export function MasterDataCompanySettingsModal({
  partner, initialSettings, saving, error, onClose, onSave, onBlock, onUnblock,
}: Props) {
  const [paymentDays,  setPaymentDays]  = useState(String(initialSettings?.paymentDays  ?? 30));
  const [creditLimit,  setCreditLimit]  = useState(String(initialSettings?.creditLimit  ?? 0));
  const [currencyCode, setCurrencyCode] = useState(initialSettings?.creditCurrencyCode  ?? 'USD');

  // Block form state
  const [showBlockForm,  setShowBlockForm]  = useState(false);
  const [blockReason,    setBlockReason]    = useState('');

  const isBlocked = initialSettings?.isBlocked ?? false;

  const handleSave = (e: React.FormEvent) => {
    e.preventDefault();
    onSave({
      creditLimit:        Number(creditLimit)  || 0,
      paymentDays:        Number(paymentDays)  || 0,
      creditCurrencyCode: currencyCode.trim()  || 'USD',
    });
  };

  const handleBlock = (e: React.FormEvent) => {
    e.preventDefault();
    if (!blockReason.trim()) return;
    onBlock(blockReason.trim());
    setShowBlockReason(false);
    setBlockReason('');
  };

  const setShowBlockReason = setShowBlockForm;

  return (
    <div className="md-modal-backdrop" role="dialog" aria-modal="true">
      <form className="md-modal" onSubmit={handleSave}>
        <h2>Condiciones por empresa</h2>
        <p className="md-modal-sub">
          {partner.legalName} · {partner.identificationNumber}
        </p>

        {error && <ZHPageNotice variant="error" message={error} className="md-modal-notice" />}

        {isBlocked && (
          <ZHPageNotice
            variant="error"
            className="md-modal-notice"
            message={`BLOQUEADO${initialSettings?.blockedReason ? ` — ${initialSettings.blockedReason}` : ''}`}
          />
        )}

        <div className="pg-form-grid pg-form-grid--2">
          <ZHField label="Días de pago" required>
            <ZhNumberInput
              className="zh-input" positiveOnly
              value={paymentDays} onChange={(e) => setPaymentDays(e.target.value)}
              disabled={saving}
            />
          </ZHField>
          <ZHField label="Límite de crédito">
            <ZhDecimalInput
              className="zh-input" decimals={2} positiveOnly
              value={creditLimit} onChange={(e) => setCreditLimit(e.target.value)}
              disabled={saving}
            />
          </ZHField>
          <ZHField label="Moneda">
            <input className="zh-input mono" maxLength={3}
              value={currencyCode}
              onChange={(e) => setCurrencyCode(e.target.value.toUpperCase().slice(0, 3))}
              disabled={saving}
            />
          </ZHField>
        </div>

        {/* ── Block section ─────────────────────────────────────────────── */}
        {!isBlocked && !showBlockForm && (
          <div className="md-block-row">
            <ZHBtn variant="danger" size="sm" type="button" disabled={saving}
              onClick={() => setShowBlockReason(true)}>
              Bloquear en esta empresa
            </ZHBtn>
          </div>
        )}

        {!isBlocked && showBlockForm && (
          <div className="md-block-form">
            <ZHField label="Motivo del bloqueo" required>
              <input className="zh-input" value={blockReason}
                onChange={(e) => setBlockReason(e.target.value)}
                placeholder="Ej: Deuda vencida, incumplimiento de contrato…"
                maxLength={500} disabled={saving} autoFocus />
            </ZHField>
            <div className="md-modal-actions md-modal-actions--inline">
              <ZHBtn variant="ghost" size="sm" type="button" onClick={() => setShowBlockReason(false)} disabled={saving}>
                Cancelar
              </ZHBtn>
              <ZHBtn variant="danger" size="sm" type="button" onClick={handleBlock}
                disabled={saving || !blockReason.trim()}>
                Confirmar bloqueo
              </ZHBtn>
            </div>
          </div>
        )}

        {isBlocked && (
          <div className="md-block-row">
            <ZHBtn variant="secondary" size="sm" type="button" disabled={saving}
              onClick={onUnblock}>
              {saving ? 'Desbloqueando…' : 'Desbloquear'}
            </ZHBtn>
          </div>
        )}

        <div className="md-modal-actions">
          <ZHBtn variant="ghost" type="button" onClick={onClose} disabled={saving}>Cerrar</ZHBtn>
          <ZHBtn variant="primary" type="submit" disabled={saving}>
            {saving ? 'Guardando…' : 'Guardar condiciones'}
          </ZHBtn>
        </div>
      </form>
    </div>
  );
}
