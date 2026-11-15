import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHBtn, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZhSelect } from "../../../components/zh/inputs";
import { message } from "../../../lib/messages";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { BpLocationCard } from "./BpLocationCard";
import { BpLocationFormFields } from "./BpLocationFormFields";
import { locationSchema, type LocationFormValues } from "./bpLocationForm";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import {
  geographyLookupFacade,
  type GeographyItemDto,
} from "../../branches/facades/geographyLookupFacade";
import { PHYSICAL_ADDRESS_GEO_COUNTRY_ID } from "../constants/physicalAddressGeography";
import { LocationTypeEnum } from "../types/businessPartner.types";
import type {
  BpLocationDto,
  CreateLocationBody,
  UpdateLocationBody,
} from "../types/businessPartner.types";

type LocModal = { mode: "create" | "edit"; data?: BpLocationDto } | null;

export interface BpLocationsSectionProps {
  bpId: string;
  /** Pestaña activa: la sección permanece montada para conservar filtro y formulario. */
  visible: boolean;
  locations: BpLocationDto[];
  canUpdate: boolean;
  canDisable: boolean;
  /** Estado de guardado compartido con la ficha (una sola operación a la vez). */
  saving: boolean;
  onSavingChange: (saving: boolean) => void;
  actionError: string | null;
  onActionError: (error: string | null) => void;
  /** Recarga los datos de la ficha tras una operación exitosa. */
  onReload: () => Promise<void>;
}

/**
 * Sección "Ubicaciones" de la ficha compartida de Business Partner (CRUD, principal,
 * activar/desactivar con confirmación). Extraída de MasterDataBusinessPartnerDetailPage
 * sin cambio de comportamiento, permisos ni contratos.
 */
export function BpLocationsSection({
  bpId,
  visible,
  locations,
  canUpdate,
  canDisable,
  saving,
  onSavingChange,
  actionError,
  onActionError,
  onReload,
}: BpLocationsSectionProps) {
  const [locModal, setLocModal] = useState<LocModal>(null);
  const [locFilter, setLocFilter] = useState<"all" | "active" | "inactive">(
    "active",
  );
  const [geoProvinces, setGeoProvinces] = useState<GeographyItemDto[]>([]);
  const [geoCantons, setGeoCantons] = useState<GeographyItemDto[]>([]);
  const [geoParishes, setGeoParishes] = useState<GeographyItemDto[]>([]);

  const locForm = useForm<LocationFormValues>({
    resolver: zodResolver(locationSchema),
    defaultValues: {
      name: "",
      type: LocationTypeEnum.Matrix,
      purpose: 0,
      addressLine: "",
      isPrimary: false,
    },
  });

  const loadProvinces = async () => {
    try {
      setGeoProvinces(
        await geographyLookupFacade.provinces(PHYSICAL_ADDRESS_GEO_COUNTRY_ID),
      );
    } catch {
      /* empty */
    }
  };
  const loadCantons = async (provinceId: string) => {
    if (!provinceId) {
      setGeoCantons([]);
      setGeoParishes([]);
      return;
    }
    try {
      setGeoCantons(await geographyLookupFacade.cantons(provinceId));
    } catch {
      /* empty */
    }
    setGeoParishes([]);
  };
  const loadParishes = async (cantonId: string) => {
    if (!cantonId) {
      setGeoParishes([]);
      return;
    }
    try {
      setGeoParishes(await geographyLookupFacade.parishes(cantonId));
    } catch {
      /* empty */
    }
  };

  const openLocCreate = async () => {
    locForm.reset({
      name: "",
      type: LocationTypeEnum.Matrix,
      purpose: 0,
      addressLine: "",
      isPrimary: false,
      provinceCode: "",
      cantonCode: "",
      parishCode: "",
      phone: "",
      email: "",
      otherDescription: "",
    });
    onActionError(null);
    setGeoCantons([]);
    setGeoParishes([]);
    await loadProvinces();
    setLocModal({ mode: "create" });
  };

  const openLocEdit = async (loc: BpLocationDto) => {
    const purposeMap: Record<string, number> = {
      "Facturación": 1,
      Entrega: 2,
      Fiscal: 4,
      Correspondencia: 8,
    };
    const purposeVal = loc.purposes.reduce(
      (acc, p) => acc | (purposeMap[p] ?? 0),
      0,
    );
    const typeVal =
      LocationTypeEnum[loc.locationType as keyof typeof LocationTypeEnum] ??
      LocationTypeEnum.Other;
    locForm.reset({
      name: loc.name,
      type: typeVal,
      purpose: purposeVal,
      addressLine: loc.addressLine,
      provinceCode: loc.provinceCode ?? "",
      cantonCode: loc.cantonCode ?? "",
      parishCode: loc.parishCode ?? "",
      phone: loc.phone ?? "",
      email: loc.email ?? "",
      otherDescription: loc.otherDescription ?? "",
      isPrimary: false,
    });
    onActionError(null);
    await loadProvinces();
    if (loc.provinceCode) await loadCantons(loc.provinceCode);
    if (loc.cantonCode) await loadParishes(loc.cantonCode);
    setLocModal({ mode: "edit", data: loc });
  };

  const saveLoc = locForm.handleSubmit(async (values) => {
    onActionError(null);
    onSavingChange(true);
    try {
      const body = {
        name: values.name,
        type: values.type,
        purpose: values.purpose,
        addressLine: values.addressLine,
        provinceCode: values.provinceCode || null,
        cantonCode: values.cantonCode || null,
        parishCode: values.parishCode || null,
        phone: values.phone || null,
        email: values.email || null,
        otherDescription: values.otherDescription || null,
        isPrimary: values.isPrimary,
      };
      if (locModal?.mode === "edit" && locModal.data) {
        const { isPrimary: _, ...updateBody } = body;
        await businessPartnerFacade.updateLocation(
          bpId,
          locModal.data.id,
          updateBody as UpdateLocationBody,
        );
        message.success("Ubicación actualizada correctamente.");
      } else {
        await businessPartnerFacade.createLocation(
          bpId,
          body as CreateLocationBody,
        );
        message.success("Ubicación creada correctamente.");
      }
      setLocModal(null);
      await onReload();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, locForm.setError, (msg) =>
        onActionError(msg),
      );
      if (!applied) onActionError("Error al guardar la ubicación.");
    } finally {
      onSavingChange(false);
    }
  });

  const toggleLoc = async (loc: BpLocationDto) => {
    if (saving) return;

    const confirmed = await message.confirm({
      title: loc.isActive ? `Desactivar "${loc.name}"` : `Activar "${loc.name}"`,
      message: loc.isActive
        ? `"${loc.name}" dejará de estar disponible para uso operativo (nuevos documentos, envíos). El histórico no se elimina.`
        : `"${loc.name}" volverá a estar disponible para uso operativo.`,
      variant: loc.isActive ? "danger" : "warning",
      confirmLabel: loc.isActive ? "Desactivar" : "Activar",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;

    onSavingChange(true);
    onActionError(null);
    try {
      if (loc.isActive)
        await businessPartnerFacade.deactivateLocation(bpId, loc.id);
      else await businessPartnerFacade.activateLocation(bpId, loc.id);
      await onReload();
      message.success(
        loc.isActive
          ? "Ubicación desactivada correctamente."
          : "Ubicación activada correctamente.",
      );
    } catch (err) {
      const errorMessage = formatApiRequestError(err, {
        generic: "Error al cambiar estado de la ubicación.",
      });
      onActionError(errorMessage);
      message.error(errorMessage);
    } finally {
      onSavingChange(false);
    }
  };

  const setPrimaryLoc = async (loc: BpLocationDto) => {
    onSavingChange(true);
    onActionError(null);
    try {
      await businessPartnerFacade.setLocationPrimary(bpId, loc.id);
      await onReload();
      message.success(`"${loc.name}" marcada como ubicación principal.`);
    } catch (err) {
      const errorMessage = formatApiRequestError(err, {
        generic: "Error al marcar como principal.",
      });
      onActionError(errorMessage);
      message.error(errorMessage);
    } finally {
      onSavingChange(false);
    }
  };

  const filteredLocs =
    locFilter === "all"
      ? locations
      : locFilter === "active"
        ? locations.filter((l) => l.isActive)
        : locations.filter((l) => !l.isActive);

  return (
    <>
      {visible && (
        <section className="md-detail-section">
          <div className="md-detail-section__header">
            <h3>Ubicaciones</h3>
            <div className="md-detail-filter-actions">
              <ZhSelect
                className="md-detail-filter-select zh-select--compact"
                value={locFilter}
                onChange={(e) =>
                  setLocFilter(e.target.value as "all" | "active" | "inactive")
                }
              >
                <option value="active">Activas</option>
                <option value="inactive">Inactivas</option>
                <option value="all">Todas</option>
              </ZhSelect>
              <ZHBtn
                variant="primary"
                size="sm"
                onClick={openLocCreate}
                disabled={!canUpdate}
              >
                <span className="material-symbols-outlined">add</span> Nueva
                ubicación
              </ZHBtn>
            </div>
          </div>
          {filteredLocs.length === 0 ? (
            <p className="md-detail-empty-hint">
              {locations.length === 0
                ? "No hay ubicaciones registradas."
                : `No hay ubicaciones ${locFilter === "active" ? "activas" : "inactivas"}.`}
            </p>
          ) : (
            <div className="md-locations-grid">
              {filteredLocs.map((l) => (
                <BpLocationCard
                  key={l.id}
                  location={l}
                  canUpdate={canUpdate}
                  canDisable={canDisable}
                  saving={saving}
                  onEdit={(loc) => void openLocEdit(loc)}
                  onSetPrimary={(loc) => void setPrimaryLoc(loc)}
                  onToggle={(loc) => void toggleLoc(loc)}
                />
              ))}
            </div>
          )}
        </section>
      )}

      <ZHModal
        open={locModal !== null}
        onClose={() => setLocModal(null)}
        title={
          locModal?.mode === "edit" ? "Editar ubicación" : "Nueva ubicación"
        }
        subtitle="Configure la dirección y propósito de la ubicación."
        footer={
          <ZHFormActions
            onCancel={() => setLocModal(null)}
            onSave={() => void saveLoc()}
            hideDraft
            disableSave={saving}
            labels={{
              cancel: "Cancelar",
              save: saving
                ? "Guardando..."
                : locModal?.mode === "edit"
                  ? "Guardar cambios"
                  : "Crear ubicación",
            }}
          />
        }
      >
        {actionError && <ZHPageNotice variant="error" message={actionError} />}
        <BpLocationFormFields
          form={locForm}
          saving={saving}
          isCreate={locModal?.mode === "create"}
          provinces={geoProvinces}
          cantons={geoCantons}
          parishes={geoParishes}
          onProvinceChange={(id) => void loadCantons(id)}
          onCantonChange={(id) => void loadParishes(id)}
        />
      </ZHModal>
    </>
  );
}
