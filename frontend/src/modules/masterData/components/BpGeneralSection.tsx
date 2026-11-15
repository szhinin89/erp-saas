import { useState } from "react";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { useI18n } from "../../../i18n/i18n";
import { message } from "../../../lib/messages";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { useSriIdTypes, getSriIdTypeName } from "../api/useSriIdTypes";
import { useLegalEntityTypes } from "../api/useLegalEntityTypes";
import type {
  BusinessPartnerDetailDto,
  UpdateBusinessPartnerBody,
} from "../types/businessPartner.types";
import { BpDetailRow } from "./BpDetailRow";
import { MasterDataPartnerWizard } from "./MasterDataPartnerWizard";

const NOOP = () => {};
const NOOP_ASYNC = () => Promise.resolve();
const DRAFT_KEY = "erp.masterdata.bp.general.draft";

/**
 * Pestaña «Datos generales»: con `businesspartners.update` reutiliza el wizard en modo embebido
 * para editar la identidad (mismo formulario, esquema y PUT que la edición del listado); sin
 * permiso muestra los mismos datos en solo lectura.
 */
export function BpGeneralSection({
  bp,
  canUpdate,
  onReload,
  onDirtyChange,
}: {
  bp: BusinessPartnerDetailDto;
  canUpdate: boolean;
  onReload: () => Promise<void>;
  onDirtyChange: (dirty: boolean) => void;
}) {
  const { t } = useI18n();
  const { options: idTypes } = useSriIdTypes();
  const { options: legalEntityTypes } = useLegalEntityTypes();
  const [saving, setSaving] = useState(false);
  // Cada guardado exitoso remonta el formulario con lo persistido (base limpia aunque la relectura
  // devuelva exactamente los mismos valores, p. ej. si el servidor normaliza).
  const [saveVersion, setSaveVersion] = useState(0);
  const formId = `bp-general-form-${bp.id}`;
  const role = bp.roles.some((r) => r.isActive && r.roleType === "Supplier")
    ? "supplier"
    : "customer";

  if (!canUpdate) {
    return (
      <section className="md-detail-section">
        <BpDetailRow
          label={t("masterdata.bp.detail.general.idType", "Tipo de identificación")}
          value={`${bp.identificationType} — ${getSriIdTypeName(bp.identificationType, idTypes)}`}
        />
        <BpDetailRow
          label={t("masterdata.bp.detail.general.idNumber", "Número")}
          value={bp.identificationNumber}
        />
        <BpDetailRow
          label={t("masterdata.bp.detail.general.legalEntityType", "Tipo de entidad legal")}
          value={
            legalEntityTypes.find((x) => x.code === bp.legalEntityTypeCode)?.name ??
            String(bp.legalEntityTypeCode)
          }
        />
        <BpDetailRow
          label={t("masterdata.bp.detail.general.legalName", "Razón social")}
          value={bp.legalName}
        />
        <BpDetailRow
          label={t("masterdata.bp.detail.general.tradeName", "Nombre comercial")}
          value={bp.tradeName}
        />
        <BpDetailRow
          label={t("masterdata.bp.detail.general.country", "País")}
          value={bp.countryCode ?? "EC"}
        />
        <BpDetailRow
          label={t("masterdata.bp.detail.summary.createdAt", "Creado")}
          value={formatDateTime(bp.createdAt)}
        />
      </section>
    );
  }

  // El error se relanza: el wizard lo traduce a errores de campo o al aviso del formulario.
  const handleUpdate = async (body: UpdateBusinessPartnerBody) => {
    setSaving(true);
    try {
      await businessPartnerFacade.updateBusinessPartner(bp.id, body);
      message.success(
        t("masterdata.bp.detail.general.saved", "Datos actualizados correctamente."),
      );
      await onReload();
      setSaveVersion((v) => v + 1);
    } finally {
      setSaving(false);
    }
  };

  // Tras guardar, la ficha recarga y el formulario se remonta con los datos persistidos (base limpia).
  const persistedKey = JSON.stringify([
    bp.legalName,
    bp.tradeName,
    bp.legalEntityTypeCode,
    bp.countryCode,
  ]);

  return (
    <section className="md-detail-section">
      <MasterDataPartnerWizard
        key={`${persistedKey}-${saveVersion}`}
        role={role}
        draftKey={DRAFT_KEY}
        submitting={saving}
        editingPartner={bp}
        embedded
        modalFormId={formId}
        onDirtyChange={onDirtyChange}
        onSubmitCreate={NOOP_ASYNC}
        onSubmitUpdate={handleUpdate}
        onAssignRole={NOOP_ASYNC}
        onCancel={NOOP}
      />
      <div className="zh-actions">
        <ZHBtn type="submit" form={formId} variant="success" disabled={saving}>
          {saving
            ? t("masterdata.wizard.btn.saving", "Guardando…")
            : t("common.saveChanges")}
        </ZHBtn>
      </div>
    </section>
  );
}
