import { ZHModal } from "../../../components/zh/ZHModal";
import type { CustomerConfigBody, CustomerRoleConfigDto } from "../types/businessPartner.types";
import { CustomerClassificationForm } from "./CustomerClassificationForm";

// ── MasterDataCustomerConfigModal — CRM-ready classification modal ───────────
export function MasterDataCustomerConfigModal({
  bpName,
  initialConfig,
  saving,
  error,
  onClose,
  onSave,
}: {
  bpName: string;
  initialConfig: CustomerRoleConfigDto | null;
  saving: boolean;
  error?: string | null;
  onClose: () => void;
  onSave: (body: CustomerConfigBody) => void;
}) {
  return (
    <ZHModal
      open
      onClose={onClose}
      title="Perfil Cliente — CRM"
      subtitle={bpName}
    >
      <CustomerClassificationForm
        initialConfig={initialConfig}
        saving={saving}
        error={error}
        onClose={onClose}
        onSave={onSave}
      />
    </ZHModal>
  );
}
