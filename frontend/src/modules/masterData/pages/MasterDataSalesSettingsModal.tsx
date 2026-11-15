import { useRef } from "react";
import { ZHModal } from "../../../components/zh/ZHModal";
import { CustomerSalesSettingsForm } from "../components/CustomerSalesSettingsForm";
import type { BusinessPartnerSummaryDto, CompanyBpSalesSettingsDto } from "../types/businessPartner.types";

type Props = {
  partner: BusinessPartnerSummaryDto;
  initialSettings: CompanyBpSalesSettingsDto | null;
  saving: boolean;
  error: string | null;
  onClose: () => void;
  onSave: (body: { paymentTermId: string | null }) => Promise<void>;
};

export function MasterDataSalesSettingsModal({ partner, initialSettings, saving, error, onClose, onSave }: Props) {
  const submitting = useRef(false);
  const handleClose = () => { if (!submitting.current) onClose(); };
  return (
    <ZHModal open onClose={handleClose} title="Condición predeterminada para ventas" subtitle={partner.legalName} closeOnBackdrop={false}>
      <CustomerSalesSettingsForm
        initialSettings={initialSettings}
        saving={saving}
        error={error}
        submittingRef={submitting}
        onClose={handleClose}
        onSaved={onClose}
        onSave={onSave}
      />
    </ZHModal>
  );
}
