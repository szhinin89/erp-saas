import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhTextInput, ZhPhoneInput } from "../../../components/zh/inputs";

interface Props {
  open: boolean;
  isEdit: boolean;
  saving: boolean;
  error: string;
  custId: string;
  custName: string;
  custIdType: string;
  custAddress: string;
  custEmail: string;
  custPhone: string;
  sriIdTypes: { code: string; name: string }[];
  onCustIdChange: (v: string) => void;
  onCustNameChange: (v: string) => void;
  onCustIdTypeChange: (v: string) => void;
  onCustAddressChange: (v: string) => void;
  onCustEmailChange: (v: string) => void;
  onCustPhoneChange: (v: string) => void;
  onSave: () => void;
  onCancel: () => void;
}

export function QuickCustomerModal({
  open,
  isEdit,
  saving,
  error,
  custId,
  custName,
  custIdType,
  custAddress,
  custEmail,
  custPhone,
  sriIdTypes,
  onCustIdChange,
  onCustNameChange,
  onCustIdTypeChange,
  onCustAddressChange,
  onCustEmailChange,
  onCustPhoneChange,
  onSave,
  onCancel,
}: Props) {
  return (
    <ZHModal
      open={open}
      onClose={onCancel}
      size="sm"
      title={isEdit ? "Editar Cliente" : "Crear Cliente"}
      subtitle="Datos básicos del cliente."
      footer={
        <>
          <ZHBtn variant="ghost" size="md" onClick={onCancel}>
            Cancelar
          </ZHBtn>
          <ZHBtn
            variant="primary"
            size="md"
            disabled={saving || !custId.trim() || !custName.trim()}
            onClick={onSave}
          >
            {saving
              ? "Guardando..."
              : isEdit
                ? "Guardar cambios"
                : "Crear y seleccionar"}
          </ZHBtn>
        </>
      }
    >
      {error && <ZHPageNotice variant="error" message={error} />}

      <ZHGrid cols={2}>
        <ZHField label="Tipo ID">
          <select
            value={custIdType}
            onChange={(e) => onCustIdTypeChange(e.target.value)}
            disabled={isEdit}
          >
            {sriIdTypes.map((t) => (
              <option key={t.code} value={t.code}>
                {t.name}
              </option>
            ))}
          </select>
        </ZHField>
        <ZHField label="Número" required>
          <input
            value={custId}
            onChange={(e) => onCustIdChange(e.target.value)}
            placeholder="0302126842"
            readOnly={isEdit}
          />
        </ZHField>
      </ZHGrid>

      <ZHField label="Nombre / Razón social" required>
        <ZhTextInput
          value={custName}
          onChange={(e) => onCustNameChange(e.target.value)}
          placeholder="Nombre del cliente"
        />
      </ZHField>

      <ZHField label="Dirección">
        <ZhTextInput
          value={custAddress}
          onChange={(e) => onCustAddressChange(e.target.value)}
          placeholder="Av. Principal 123, Cuenca"
        />
      </ZHField>

      <ZHGrid cols={2}>
        <ZHField label="Correo">
          <input
            value={custEmail}
            onChange={(e) => onCustEmailChange(e.target.value)}
            placeholder="cliente@email.com"
            type="email"
          />
        </ZHField>
        <ZHField label="Teléfono">
          <ZhPhoneInput
            value={custPhone}
            onChange={onCustPhoneChange}
            placeholder="0991234567"
          />
        </ZHField>
      </ZHGrid>
    </ZHModal>
  );
}
