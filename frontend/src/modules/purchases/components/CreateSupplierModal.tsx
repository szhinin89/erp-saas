import { useState } from "react";
import { ZHModal } from "../../../components/zh/ZHModal";
import { message } from "../../../lib/messages";
import { MasterDataPartnerWizard } from "../../masterData/components/MasterDataPartnerWizard";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";
import { RoleTypeEnum } from "../../masterData/types/businessPartner.types";
import type { CreateBusinessPartnerBody } from "../../masterData/types/businessPartner.types";
import { SRI_ID_TYPE_RUC } from "../../masterData/constants/sriIdentificationCodes";

const DRAFT_KEY = "pur-reception-new-supplier-draft";

interface Props {
  open: boolean;
  supplierRuc: string;
  supplierName: string;
  /** infoTributaria/nombreComercial ya conocido (solo tras "Consultar XML") — null si aún no se
   * consultó el XML o el emisor no lo declaró. */
  supplierTradeName: string | null;
  onClose: () => void;
  /** El proveedor quedó creado y con rol Supplier activo — el caller refresca la fila. */
  onCreated: () => void;
}

/**
 * Modal de creación de proveedor lanzado desde Recepción electrónica cuando el documento SRI
 * trae un RUC sin BusinessPartner (o sin rol Supplier) en el ERP. Envuelve el wizard de
 * MasterData ya existente — no reimplementa formulario ni validación, solo precarga RUC/razón
 * social y omite el paso de búsqueda porque ya sabemos que no existe.
 */
export function CreateSupplierModal({
  open,
  supplierRuc,
  supplierName,
  supplierTradeName,
  onClose,
  onCreated,
}: Props) {
  const [saving, setSaving] = useState(false);

  const handleCreate = async (
    body: CreateBusinessPartnerBody,
    supplierConfig?: { refundProviderTypeCode: string; paymentTermId: string },
  ): Promise<void> => {
    setSaving(true);
    try {
      const created = await businessPartnerFacade.createBusinessPartner(body);
      await businessPartnerFacade.assignRole(created.id, {
        roleType: RoleTypeEnum.Supplier,
        supplierConfig: supplierConfig
          ? {
              paymentTermId: supplierConfig.paymentTermId,
              refundProviderTypeCode: supplierConfig.refundProviderTypeCode,
            }
          : undefined,
      });
      message.success("Proveedor creado correctamente.");
      onCreated();
    } finally {
      setSaving(false);
    }
  };

  const handleAssignRole = async (id: string): Promise<void> => {
    setSaving(true);
    try {
      await businessPartnerFacade.assignRole(id, {
        roleType: RoleTypeEnum.Supplier,
      });
      message.success("Rol de proveedor asignado correctamente.");
      onCreated();
    } finally {
      setSaving(false);
    }
  };

  if (!open) return null;

  return (
    <ZHModal
      open={open}
      onClose={onClose}
      size="lg"
      title="Crear proveedor"
      subtitle={`RUC ${supplierRuc} · precargado desde el documento de recepción`}
    >
      <MasterDataPartnerWizard
        role="supplier"
        draftKey={DRAFT_KEY}
        submitting={saving}
        editingPartner={null}
        embedded
        initialValues={{
          identificationType: SRI_ID_TYPE_RUC,
          identificationNumber: supplierRuc,
          legalName: supplierName,
          tradeName: supplierTradeName ?? undefined,
        }}
        onSubmitCreate={handleCreate}
        onSubmitUpdate={() => Promise.resolve()}
        onAssignRole={handleAssignRole}
        onCancel={onClose}
      />
    </ZHModal>
  );
}
