import { useState } from 'react';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import type { BusinessPartnerDto } from '../types/businessPartner.types';

const MAX_NOTES = 2000;

type Props = {
  partner: BusinessPartnerDto;
  saving: boolean;
  error?: string | null;
  onClose: () => void;
  onSave: (notes: string | null) => void;
};

export function MasterDataCustomerNotesModal({ partner, saving, error, onClose, onSave }: Props) {
  const [notes, setNotes] = useState(partner.customerNotes ?? '');
  const remaining = MAX_NOTES - notes.length;

  return (
    <div className="md-modal-backdrop" role="dialog" aria-modal="true">
      <form
        className="md-modal md-modal--wide"
        onSubmit={(e) => {
          e.preventDefault();
          onSave(notes.trim() || null);
        }}
      >
        <h2>Notas del cliente</h2>
        <p className="md-modal-sub">
          {partner.legalName} · {partner.identificationNumber}
        </p>

        {error && (
          <ZHPageNotice variant="error" message={error} className="md-modal-notice" />
        )}

        <ZHField label="Notas internas">
          <textarea
            className="zh-input md-notes-textarea"
            value={notes}
            onChange={(e) => setNotes(e.target.value.slice(0, MAX_NOTES))}
            rows={6}
            disabled={saving}
            placeholder="Observaciones internas sobre este cliente…"
            maxLength={MAX_NOTES}
          />
        </ZHField>
        <p className="md-notes-counter" aria-live="polite">
          {remaining} caracteres restantes
        </p>

        <div className="md-modal-actions">
          <ZHBtn variant="ghost" type="button" onClick={onClose} disabled={saving}>
            Cancelar
          </ZHBtn>
          <ZHBtn variant="primary" type="submit" disabled={saving}>
            {saving ? 'Guardando…' : 'Guardar notas'}
          </ZHBtn>
        </div>
      </form>
    </div>
  );
}
