import { useCallback, useId, useState } from "react";
import { Controller, FormProvider, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useAsync } from "../../../hooks/useAsync";
import { useDiscardConfirmation, releaseDiscardApproval, useUnsavedChangesGuard } from "../../../hooks/useUnsavedChangesGuard";
import { message } from "../../../lib/messages";
import { todayIso } from "../../../lib/formatters/dateFormatters";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhSearchSelect } from "../../../components/zh/inputs/ZhSearchSelect";
import { applyServerErrors } from "../../lib/validationErrors";
import {
  MasterDataPartnerWizard,
  employeePartnerFacade,
  type BusinessPartnerSummaryDto,
  type CreateBusinessPartnerBody,
} from "../../masterData/facades/employeePartnerFacade";
import { employeeErrorMessage, employeeService } from "../api/employeeService";
import { employeeCatalogService } from "../api/employeeCatalogService";
import { employeeCreateSchema, type EmployeeCreateValues } from "../schemas/employeeSchemas";
import type { EmployeeCatalogEntry, EmployeeDto } from "../types/employee.types";

const PARTNER_DRAFT_KEY = "employees.partnerWizard.draft";

type Props = {
  canViewCatalogs: boolean;
  onClose: () => void;
  onCreated: (employee: EmployeeDto) => void;
};

/**
 * Alta de ficha sobre el listado: selecciona un socio con rol Employee (o lo registra/le asigna el
 * rol con el wizard oficial de MasterData) y captura solo datos laborales. La identidad nunca se
 * duplica aquí.
 */
export function EmployeeCreateModal({ canViewCatalogs, onClose, onCreated }: Props) {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canSearchPartners = canShow("masterdata.businesspartners.view");
  const canCreatePartner = canShow("masterdata.businesspartners.create");
  const canAssignRole = canShow("masterdata.businesspartners.update");
  const canOpenWizard = canSearchPartners && canAssignRole;
  const formId = useId();

  const [mode, setMode] = useState<"form" | "partner">("form");
  const [partner, setPartner] = useState<BusinessPartnerSummaryDto | null>(null);
  const [partnerSaving, setPartnerSaving] = useState(false);
  const [partnerDirty, setPartnerDirty] = useState(false);
  const [error, setError] = useState("");
  const catalogs = useAsync(employeeCatalogService.loadOptions, canViewCatalogs);

  const form = useForm<EmployeeCreateValues>({
    resolver: zodResolver(employeeCreateSchema(t)),
    defaultValues: {
      businessPartnerId: "",
      startDate: todayIso(),
      jobPositionId: "",
      professionId: "",
      specialtyIds: [],
    },
  });
  const { errors, isDirty, isSubmitting } = form.formState;
  useUnsavedChangesGuard(isDirty || partnerDirty);
  const confirmDiscard = useDiscardConfirmation();

  const close = async () => {
    if (!(await confirmDiscard())) return;
    onClose();
    releaseDiscardApproval();
  };

  const searchPartners = useCallback(
    (q: string, signal: AbortSignal) => employeePartnerFacade.searchEligible(q, signal),
    [],
  );

  const choosePartner = (next: BusinessPartnerSummaryDto | null) => {
    setPartner(next);
    form.setValue("businessPartnerId", next?.id ?? "", {
      shouldDirty: true,
      shouldValidate: form.formState.isSubmitted,
    });
  };

  /** El socio queda seleccionado para completar la ficha. */
  const returnWith = (next: BusinessPartnerSummaryDto) => {
    choosePartner(next);
    setPartnerDirty(false);
    setMode("form");
  };

  const leaveWizard = async () => {
    if (partnerDirty && !(await confirmDiscard())) return;
    setPartnerDirty(false);
    setMode("form");
    releaseDiscardApproval();
  };

  /** El wizard entrega solo el id: tras asignar en MasterData se relee para mostrarlo. */
  const assignAndSelect = async (id: string) => {
    setPartnerSaving(true);
    try {
      await employeePartnerFacade.assignEmployeeRole(id);
      message.success(t("employees.create.roleAssigned", "Rol de empleado asignado."));
      returnWith(await employeePartnerFacade.getById(id));
    } finally {
      setPartnerSaving(false);
    }
  };

  const createPartner = async (body: CreateBusinessPartnerBody) => {
    // El wizard no distingue permisos: sin ellos no se envía nada al servidor.
    if (!canCreatePartner || !canAssignRole) {
      throw new Error(
        t(
          "employees.create.noCreatePartnerPermission",
          "No tiene permiso para crear socios de negocio. Busque uno existente o solicítelo a quien administre Datos Maestros.",
        ),
      );
    }
    setPartnerSaving(true);
    try {
      const created = await employeePartnerFacade.create(body);
      await employeePartnerFacade.assignEmployeeRole(created.id);
      message.success(t("employees.create.partnerCreated", "Socio de negocio registrado como empleado."));
      returnWith(created);
    } finally {
      setPartnerSaving(false);
    }
  };

  const submit = form.handleSubmit(async (values) => {
    setError("");
    try {
      const employee = await employeeService.create({
        businessPartnerId: values.businessPartnerId,
        startDate: values.startDate,
        jobPositionId: values.jobPositionId || undefined,
        professionId: values.professionId || undefined,
        specialtyIds: values.specialtyIds.length > 0 ? values.specialtyIds : undefined,
      });
      message.success(t("employees.create.success", "Empleado registrado correctamente."));
      form.reset(values);
      onCreated(employee);
    } catch (err) {
      if (!applyServerErrors(err, form.setError, setError)) {
        setError(employeeErrorMessage(err, t("employees.create.error", "No se pudo registrar el empleado.")));
      }
    }
  });

  const actives = (rows: EmployeeCatalogEntry[] | undefined) => (rows ?? []).filter((r) => r.isActive);
  const catalogSelect = (
    name: "jobPositionId" | "professionId",
    label: string,
    rows: EmployeeCatalogEntry[] | undefined,
  ) => (
    <ZHField label={label} fieldError={errors[name]?.message}>
      <ZhSelect disabled={isSubmitting || catalogs.loading} {...form.register(name)}>
        <option value="">{t("employees.none", "Sin asignar")}</option>
        {actives(rows).map((r) => (
          <option key={r.id} value={r.id}>
            {r.name}
          </option>
        ))}
      </ZhSelect>
    </ZHField>
  );

  if (mode === "partner") {
    return (
      <ZHModal
        open
        size="xl"
        closeOnBackdrop={false}
        title={t("employees.create.partnerTitle", "Socio de negocio para el empleado")}
        closeLabel={t("common.close", "Cerrar")}
        onClose={() => void leaveWizard()}
      >
        <MasterDataPartnerWizard
          role="employee"
          embedded
          draftKey={PARTNER_DRAFT_KEY}
          submitting={partnerSaving}
          editingPartner={null}
          onSubmitCreate={createPartner}
          onAssignRole={assignAndSelect}
          onDirtyChange={setPartnerDirty}
          onCancel={() => void leaveWizard()}
        />
      </ZHModal>
    );
  }

  return (
    <ZHModal
      open
      size="lg"
      closeOnBackdrop={false}
      title={t("employees.create.title", "Nuevo empleado")}
      closeLabel={t("common.close", "Cerrar")}
      onClose={() => void close()}
      footer={
        <>
          <ZHBtn type="button" variant="ghost" size="md" onClick={() => void close()}>
            {t("common.cancel", "Cancelar")}
          </ZHBtn>
          <ZHBtn
            type="submit"
            form={formId}
            variant="primary"
            size="md"
            disabled={isSubmitting || !canSearchPartners}
          >
            {isSubmitting
              ? t("common.saving", "Guardando...")
              : t("employees.create.submit", "Crear ficha")}
          </ZHBtn>
        </>
      }
    >
      <FormProvider {...form}>
        <form id={formId} onSubmit={submit} noValidate>
          <ZHGrid cols={1}>
            {error && <ZHPageNotice variant="error" message={error} />}
            {catalogs.error && <ZHPageNotice variant="error" message={catalogs.error} />}
            {!canSearchPartners && (
              <ZHPageNotice
                variant="warning"
                message={t(
                  "employees.create.noPartnerViewPermission",
                  "Para registrar un empleado necesita el permiso de consulta de socios de negocio.",
                )}
              />
            )}
            {canSearchPartners && (
              <>
                <ZHField
                  label={t("employees.create.partner", "Socio de negocio con rol Empleado")}
                  required
                  fieldError={errors.businessPartnerId?.message}
                >
                  <Controller
                    control={form.control}
                    name="businessPartnerId"
                    render={() => (
                      <ZhSearchSelect<BusinessPartnerSummaryDto>
                        aria-label={t("employees.create.partner", "Socio de negocio con rol Empleado")}
                        value={partner}
                        onChange={choosePartner}
                        loadOptions={searchPartners}
                        getOptionKey={(p) => p.id}
                        getOptionLabel={(p) => p.legalName}
                        getOptionDescription={(p) => p.identificationNumber}
                        placeholder={t("employees.create.partnerPlaceholder", "Buscar por identificación o nombre")}
                        emptyText={t("employees.create.partnerEmpty", "No hay socios con rol Empleado que coincidan.")}
                        loadingText={t("common.loading", "Cargando...")}
                        disabled={isSubmitting}
                      />
                    )}
                  />
                </ZHField>
                {canOpenWizard ? (
                  <div className="zh-form-actions-row zh-form-actions-row--flush">
                    <ZHBtn
                      type="button"
                      variant="secondary"
                      size="sm"
                      icon="person_add"
                      onClick={() => setMode("partner")}
                    >
                      {t("employees.create.openPartnerWizard", "Registrar o asignar rol Empleado")}
                    </ZHBtn>
                  </div>
                ) : (
                  <ZHPageNotice
                    variant="info"
                    message={t(
                      "employees.create.noRolePermission",
                      "Si la persona no aparece, quien administre Datos Maestros debe registrarla o asignarle el rol Empleado.",
                    )}
                  />
                )}
              </>
            )}

            <ZHGrid cols={2}>
              <ZHField
                label={t("employees.fields.startDate", "Fecha de ingreso")}
                required
                fieldError={errors.startDate?.message}
              >
                <ZhDateInput disabled={isSubmitting} {...form.register("startDate")} />
              </ZHField>
              {canViewCatalogs && (
                <>
                  {catalogSelect("jobPositionId", t("employees.fields.jobPosition", "Cargo"), catalogs.data?.jobPositions)}
                  {catalogSelect("professionId", t("employees.fields.profession", "Profesión"), catalogs.data?.professions)}
                </>
              )}
            </ZHGrid>
            {canViewCatalogs ? (
              <ZHField label={t("employees.fields.specialties", "Especialidades")}>
                <Controller
                  control={form.control}
                  name="specialtyIds"
                  render={({ field }) => (
                    <ZhSearchSelect<EmployeeCatalogEntry>
                      mode="multiple"
                      aria-label={t("employees.fields.specialties", "Especialidades")}
                      options={actives(catalogs.data?.specialties)}
                      value={actives(catalogs.data?.specialties).filter((s) => field.value.includes(s.id))}
                      onChange={(next) => field.onChange(next.map((s) => s.id))}
                      getOptionKey={(s) => s.id}
                      getOptionLabel={(s) => s.name}
                      portal
                      disabled={isSubmitting}
                    />
                  )}
                />
              </ZHField>
            ) : (
              <ZHPageNotice
                variant="info"
                message={t(
                  "employees.create.noCatalogPermission",
                  "Sin permiso de consulta de catálogos laborales: cargo, profesión y especialidades pueden asignarse después.",
                )}
              />
            )}
          </ZHGrid>
        </form>
      </FormProvider>
    </ZHModal>
  );
}
