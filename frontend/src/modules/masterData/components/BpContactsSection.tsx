import { useMemo, useState } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Badge } from "../../../components/PageShell";
import {
  ZHBtn,
  ZHField,
  ZHGrid,
  ZHFormActions,
} from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHModal } from "../../../components/zh/ZHModal";
import {
  ZhTextInput,
  ZhPhoneInput,
  ZhTextarea,
  ZhSelect,
  ZhCheckbox,
} from "../../../components/zh/inputs";
import { useI18n } from "../../../i18n/i18n";
import { message } from "../../../lib/messages";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { ContactRoleEnum } from "../types/businessPartner.types";
import type {
  BpContactDto,
  BpLocationDto,
  CreateContactBody,
  UpdateContactBody,
} from "../types/businessPartner.types";

type Translate = (key: string, fallback?: string) => string;

/** Esquema del formulario de contacto; los mensajes se resuelven con `t` (idioma activo). */
const contactSchema = (t: Translate) =>
  z.object({
    firstName: z.string().min(1, t("masterdata.bp.detail.con.validation.firstNameRequired", "Nombre obligatorio.")).max(100),
    lastName: z.string().max(100).optional().or(z.literal("")),
    position: z.string().max(150).optional().or(z.literal("")),
    role: z.number({ required_error: t("masterdata.bp.detail.con.validation.roleRequired", "Seleccione un rol.") }),
    locationId: z.string().optional().or(z.literal("")),
    phone: z.string().max(20).optional().or(z.literal("")),
    mobile: z.string().max(20).optional().or(z.literal("")),
    email: z.string().max(254).optional().or(z.literal("")),
    notes: z.string().max(2000).optional().or(z.literal("")),
    isPrimary: z.boolean().default(false),
    otherDescription: z.string().max(100).optional().or(z.literal("")),
  });
type ContactFormValues = z.infer<ReturnType<typeof contactSchema>>;

const R = "masterdata.bp.detail.con.role.";
const CONTACT_ROLES = [
  { value: ContactRoleEnum.Commercial, labelKey: R + "commercial", fallback: "Comercial" },
  { value: ContactRoleEnum.Accounting, labelKey: R + "accounting", fallback: "Contabilidad" },
  { value: ContactRoleEnum.Management, labelKey: R + "management", fallback: "Gerencia" },
  { value: ContactRoleEnum.Reception, labelKey: R + "reception", fallback: "Recepción" },
  { value: ContactRoleEnum.Dispatch, labelKey: R + "dispatch", fallback: "Despacho" },
  { value: ContactRoleEnum.Billing, labelKey: R + "billing", fallback: "Facturación" },
  { value: ContactRoleEnum.Technical, labelKey: R + "technical", fallback: "Técnico" },
  { value: ContactRoleEnum.Purchasing, labelKey: R + "purchasing", fallback: "Compras" },
  { value: ContactRoleEnum.Legal, labelKey: R + "legal", fallback: "Legal" },
  { value: ContactRoleEnum.Other, labelKey: R + "other", fallback: "Otro" },
];

type ConModal = { mode: "create" | "edit"; data?: BpContactDto } | null;

export interface BpContactsSectionProps {
  bpId: string;
  /** Pestaña activa: la sección permanece montada para conservar filtro y formulario. */
  visible: boolean;
  contacts: BpContactDto[];
  /** Ubicaciones activas, para el selector "Ubicación asociada". */
  activeLocations: BpLocationDto[];
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
 * Sección "Contactos" de la ficha compartida de Business Partner (CRUD, principal,
 * activar/desactivar con confirmación). Extraída de MasterDataBusinessPartnerDetailPage
 * sin cambio de comportamiento, permisos ni contratos.
 */
export function BpContactsSection({
  bpId,
  visible,
  contacts,
  activeLocations,
  canUpdate,
  canDisable,
  saving,
  onSavingChange,
  actionError,
  onActionError,
  onReload,
}: BpContactsSectionProps) {
  const { t } = useI18n();
  const [conModal, setConModal] = useState<ConModal>(null);
  const resolver = useMemo(() => zodResolver(contactSchema(t)), [t]);

  const conForm = useForm<ContactFormValues>({
    resolver,
    defaultValues: {
      firstName: "",
      role: ContactRoleEnum.Commercial,
      isPrimary: false,
    },
  });

  const openConCreate = () => {
    conForm.reset({
      firstName: "",
      lastName: "",
      position: "",
      role: ContactRoleEnum.Commercial,
      locationId: "",
      phone: "",
      mobile: "",
      email: "",
      notes: "",
      isPrimary: false,
      otherDescription: "",
    });
    onActionError(null);
    setConModal({ mode: "create" });
  };

  const openConEdit = (con: BpContactDto) => {
    const roleVal =
      ContactRoleEnum[con.contactRole as keyof typeof ContactRoleEnum] ??
      ContactRoleEnum.Other;
    conForm.reset({
      firstName: con.firstName,
      lastName: con.lastName ?? "",
      position: con.position ?? "",
      role: roleVal,
      locationId: con.locationId ?? "",
      phone: con.phone ?? "",
      mobile: con.mobile ?? "",
      email: con.email ?? "",
      notes: con.notes ?? "",
      isPrimary: false,
      otherDescription: con.otherDescription ?? "",
    });
    onActionError(null);
    setConModal({ mode: "edit", data: con });
  };

  const saveCon = conForm.handleSubmit(async (values) => {
    onActionError(null);
    onSavingChange(true);
    try {
      const body = {
        firstName: values.firstName,
        role: values.role,
        lastName: values.lastName || null,
        position: values.position || null,
        locationId: values.locationId || null,
        phone: values.phone || null,
        mobile: values.mobile || null,
        email: values.email || null,
        notes: values.notes || null,
        isPrimary: values.isPrimary,
        otherDescription: values.otherDescription || null,
      };
      if (conModal?.mode === "edit" && conModal.data) {
        const { isPrimary: _, ...updateBody } = body;
        await businessPartnerFacade.updateContact(
          bpId,
          conModal.data.id,
          updateBody as UpdateContactBody,
        );
        message.success(t("masterdata.bp.detail.con.updated", "Contacto actualizado correctamente."));
      } else {
        await businessPartnerFacade.createContact(
          bpId,
          body as CreateContactBody,
        );
        message.success(t("masterdata.bp.detail.con.created", "Contacto creado correctamente."));
      }
      setConModal(null);
      await onReload();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, conForm.setError, (msg) =>
        onActionError(msg),
      );
      if (!applied) onActionError(t("masterdata.bp.detail.con.saveError", "Error al guardar el contacto."));
    } finally {
      onSavingChange(false);
    }
  });

  const toggleCon = async (con: BpContactDto) => {
    if (saving) return;

    const confirmed = await message.confirm({
      title: con.isActive
        ? t("masterdata.bp.detail.con.confirmDeactivateTitle", { name: con.fullName })
        : t("masterdata.bp.detail.con.confirmActivateTitle", { name: con.fullName }),
      message: con.isActive
        ? t("masterdata.bp.detail.con.confirmDeactivateMessage", { name: con.fullName })
        : t("masterdata.bp.detail.con.confirmActivateMessage", { name: con.fullName }),
      variant: con.isActive ? "danger" : "warning",
      confirmLabel: con.isActive
        ? t("masterdata.bp.detail.action.deactivate", "Desactivar")
        : t("masterdata.bp.detail.action.activate", "Activar"),
      cancelLabel: t("common.cancel", "Cancelar"),
    });
    if (!confirmed) return;

    onSavingChange(true);
    onActionError(null);
    try {
      if (con.isActive)
        await businessPartnerFacade.deactivateContact(bpId, con.id);
      else await businessPartnerFacade.activateContact(bpId, con.id);
      await onReload();
      message.success(
        con.isActive
          ? t("masterdata.bp.detail.con.deactivated", "Contacto desactivado correctamente.")
          : t("masterdata.bp.detail.con.activated", "Contacto activado correctamente."),
      );
    } catch (err) {
      const errorMessage = formatApiRequestError(err, {
        generic: t("masterdata.bp.detail.con.toggleError", "Error al cambiar estado del contacto."),
      });
      onActionError(errorMessage);
      message.error(errorMessage);
    } finally {
      onSavingChange(false);
    }
  };

  const setPrimaryCon = async (con: BpContactDto) => {
    onSavingChange(true);
    onActionError(null);
    try {
      await businessPartnerFacade.setContactPrimary(bpId, con.id);
      await onReload();
      message.success(t("masterdata.bp.detail.con.primaryDone", { name: con.fullName }));
    } catch (err) {
      const errorMessage = formatApiRequestError(err, {
        generic: t("masterdata.bp.detail.primaryError", "Error al marcar como principal."),
      });
      onActionError(errorMessage);
      message.error(errorMessage);
    } finally {
      onSavingChange(false);
    }
  };

  return (
    <>
      {visible && (
        <section className="md-detail-section">
          <div className="md-detail-section__header">
            <h3>{t("masterdata.bp.detail.tabs.contacts", "Contactos")}</h3>
            <ZHBtn
              variant="primary"
              size="sm"
              onClick={openConCreate}
              disabled={!canUpdate}
            >
              <span className="material-symbols-outlined" aria-hidden="true">add</span>{" "}
              {t("masterdata.bp.detail.con.new", "Nuevo contacto")}
            </ZHBtn>
          </div>
          {contacts.length === 0 ? (
            <p className="md-detail-empty-hint">
              {t("masterdata.bp.detail.con.empty", "No hay contactos registrados.")}
            </p>
          ) : (
            <div className="md-contacts-grid">
              {contacts.map((c) => (
                <div
                  key={c.id}
                  className={`md-contact-card ${!c.isActive ? "md-contact-card--inactive" : ""}`}
                >
                  <div className="md-contact-card__header">
                    <span className="md-contact-card__name zh-row-title">
                      {c.fullName}
                    </span>
                    {c.isPrimary && <Badge variant="success" label={t("masterdata.bp.detail.primary", "Principal")} />}
                    <Badge variant="neutral" label={c.roleLabel} />
                  </div>
                  {c.position && (
                    <p className="md-contact-card__position">{c.position}</p>
                  )}
                  <div className="md-contact-card__info">
                    {c.phone && <span>📞 {c.phone}</span>}
                    {c.mobile && <span>📱 {c.mobile}</span>}
                    {c.email && <span>✉ {c.email}</span>}
                  </div>
                  {(canUpdate || canDisable) && (
                    <div className="md-contact-card__actions">
                      {canUpdate && c.isActive && (
                        <ZHBtn
                          variant="ghost"
                          size="sm"
                          onClick={() => openConEdit(c)}
                        >
                          {t("masterdata.bp.detail.action.edit", "Editar")}
                        </ZHBtn>
                      )}
                      {canUpdate && c.isActive && !c.isPrimary && (
                        <ZHBtn
                          variant="ghost"
                          size="sm"
                          disabled={saving}
                          onClick={() => void setPrimaryCon(c)}
                        >
                          {t("masterdata.bp.detail.action.primary", "Principal")}
                        </ZHBtn>
                      )}
                      {(c.isActive ? canDisable : canUpdate) && (
                        <ZHBtn
                          variant="ghost"
                          size="sm"
                          disabled={saving}
                          onClick={() => void toggleCon(c)}
                        >
                          {c.isActive
                            ? t("masterdata.bp.detail.action.deactivate", "Desactivar")
                            : t("masterdata.bp.detail.action.activate", "Activar")}
                        </ZHBtn>
                      )}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </section>
      )}

      <ZHModal
        open={conModal !== null}
        onClose={() => setConModal(null)}
        title={
          conModal?.mode === "edit"
            ? t("masterdata.bp.detail.con.edit", "Editar contacto")
            : t("masterdata.bp.detail.con.new", "Nuevo contacto")
        }
        subtitle={t("masterdata.bp.detail.con.subtitle", "Configure los datos del contacto.")}
        footer={
          <ZHFormActions
            onCancel={() => setConModal(null)}
            onSave={() => void saveCon()}
            hideDraft
            disableSave={saving}
            labels={{
              cancel: t("common.cancel", "Cancelar"),
              save: saving
                ? t("masterdata.wizard.btn.saving", "Guardando…")
                : conModal?.mode === "edit"
                  ? t("common.saveChanges", "Guardar cambios")
                  : t("masterdata.bp.detail.con.create", "Crear contacto"),
            }}
          />
        }
      >
        {actionError && <ZHPageNotice variant="error" message={actionError} />}
        <div className="pg-section-body">
          <ZHGrid cols={2}>
            <ZHField
              label={t("masterdata.bp.detail.con.field.firstName", "Nombre *")}
              required
              error={conForm.formState.errors.firstName?.message}
            >
              <ZhTextInput
                className="zh-input"
                disabled={saving}
                {...conForm.register("firstName")}
              />
            </ZHField>
            <ZHField label={t("masterdata.bp.detail.con.field.lastName", "Apellido")}>
              <ZhTextInput
                className="zh-input"
                disabled={saving}
                {...conForm.register("lastName")}
              />
            </ZHField>
          </ZHGrid>
          <ZHGrid cols={2}>
            <ZHField
              label={t("masterdata.bp.detail.con.field.role", "Rol *")}
              required
              error={conForm.formState.errors.role?.message}
            >
              <ZhSelect
                disabled={saving}
                {...conForm.register("role", { valueAsNumber: true })}
              >
                {CONTACT_ROLES.map((role) => (
                  <option key={role.value} value={role.value}>
                    {t(role.labelKey, role.fallback)}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
            <ZHField label={t("masterdata.bp.detail.con.field.position", "Cargo")}>
              <ZhTextInput
                className="zh-input"
                disabled={saving}
                {...conForm.register("position")}
              />
            </ZHField>
          </ZHGrid>
          <ZHGrid cols={3}>
            <ZHField label={t("masterdata.bp.detail.con.field.phone", "Teléfono")}>
              <Controller
                name="phone"
                control={conForm.control}
                render={({ field }) => (
                  <ZhPhoneInput {...field} disabled={saving} />
                )}
              />
            </ZHField>
            <ZHField label={t("masterdata.bp.detail.con.field.mobile", "Celular")}>
              <Controller
                name="mobile"
                control={conForm.control}
                render={({ field }) => (
                  <ZhPhoneInput {...field} disabled={saving} />
                )}
              />
            </ZHField>
            <ZHField label={t("masterdata.bp.detail.con.field.email", "Email")}>
              <ZhTextInput
                className="zh-input"
                disabled={saving}
                {...conForm.register("email")}
              />
            </ZHField>
          </ZHGrid>
          <ZHField label={t("masterdata.bp.detail.con.field.location", "Ubicación asociada")}>
            <ZhSelect disabled={saving} {...conForm.register("locationId")}>
              <option value="">{t("masterdata.bp.detail.con.none", "— Ninguna —")}</option>
              {activeLocations.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.name} ({l.typeLabel})
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZHField label={t("masterdata.bp.detail.con.field.notes", "Notas")}>
            <ZhTextarea
              className="zh-input"
              rows={2}
              disabled={saving}
              {...conForm.register("notes")}
            />
          </ZHField>
          {conModal?.mode === "create" && (
            <label className="zh-checkbox-label md-detail-primary-option">
              <ZhCheckbox
                {...conForm.register("isPrimary")}
                disabled={saving}
              />
              <span>{t("masterdata.bp.detail.con.markPrimary", "Marcar como contacto principal")}</span>
            </label>
          )}
        </div>
      </ZHModal>
    </>
  );
}
