import { useEffect } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useAsync } from "../../../hooks/useAsync";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhSearchSelect } from "../../../components/zh/inputs/ZhSearchSelect";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import { employeeService } from "../api/employeeService";
import { employeeProfileSchema, type EmployeeProfileValues } from "../schemas/employeeSchemas";
import type { RunEmployeeWrite } from "../pages/useEmployeeDetailPage";
import type {
  EmployeeCatalogEntry,
  EmployeeCatalogOptions,
  EmployeeCatalogRef,
  EmployeeDto,
} from "../types/employee.types";
import { EmployeeEmploymentSection, type ReportDirty } from "./EmployeeEmploymentSection";

const toValues = (employee: EmployeeDto): EmployeeProfileValues => ({
  jobPositionId: employee.jobPosition?.id ?? "",
  professionId: employee.profession?.id ?? "",
  specialtyIds: employee.specialties.map((s) => s.id).sort(),
});

/** Activos más los ya asignados (aunque estén inactivos): estos se conservan, no se ofrecen de nuevo. */
const selectable = (
  rows: EmployeeCatalogEntry[] | undefined,
  assigned: EmployeeCatalogRef[],
): EmployeeCatalogRef[] => {
  const active = (rows ?? []).filter((r) => r.isActive);
  const kept = assigned.filter((a) => !active.some((r) => r.id === a.id));
  return [...active, ...kept];
};

type Props = {
  employee: EmployeeDto;
  permissions: { update: boolean; disable: boolean; viewCatalogs: boolean };
  catalogs: { data: EmployeeCatalogOptions | null; loading: boolean; error: string | null };
  saving: boolean;
  runWrite: RunEmployeeWrite;
  reload: () => Promise<void>;
  onDirtyChange: ReportDirty;
};

export function EmployeeGeneralTab({
  employee,
  permissions,
  catalogs,
  saving,
  runWrite,
  reload,
  onDirtyChange,
}: Props) {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  // El nombre del tipo de identificación es un catálogo con permiso propio: sin él no se consulta.
  const idTypes = useAsync(() => sriLookupFacade.idTypes(), canShow("catalog.manage"));
  const editable = permissions.update && permissions.viewCatalogs;

  const form = useForm<EmployeeProfileValues>({
    resolver: zodResolver(employeeProfileSchema),
    defaultValues: toValues(employee),
  });
  const { isDirty } = form.formState;

  // Tras otra escritura se actualiza lo no editado y se conserva lo que el usuario cambió.
  useEffect(() => {
    form.reset(toValues(employee), { keepDirtyValues: true });
  }, [employee, form]);

  useEffect(() => {
    onDirtyChange("general", isDirty);
    return () => onDirtyChange("general", false);
  }, [isDirty, onDirtyChange]);

  const submit = form.handleSubmit(async (values) => {
    const changed = (name: keyof EmployeeProfileValues) => form.getFieldState(name).isDirty;
    const jobChanged = changed("jobPositionId");
    const professionChanged = changed("professionId");
    const specialtiesChanged = changed("specialtyIds");
    if (!jobChanged && !professionChanged && !specialtiesChanged) return;

    const done = await runWrite(
      async () => {
        let rowVersion = employee.rowVersion;
        if (jobChanged || professionChanged) {
          // PATCH: solo viaja lo cambiado; vacío = null (quitar).
          const updated = await employeeService.update(employee.id, {
            rowVersion,
            ...(jobChanged ? { jobPositionId: values.jobPositionId || null } : {}),
            ...(professionChanged ? { professionId: values.professionId || null } : {}),
          });
          rowVersion = updated.rowVersion;
        }
        if (specialtiesChanged) {
          try {
            await employeeService.setSpecialties(employee.id, rowVersion, values.specialtyIds);
          } catch (err) {
            // Cargo/profesión ya quedaron aplicados: se relee para no reutilizar la versión anterior.
            if (jobChanged || professionChanged) await reload();
            throw err;
          }
        }
      },
      {
        success: t("employees.general.saved", "Datos laborales guardados."),
        error: t("employees.general.saveError", "No se pudieron guardar los datos laborales."),
      },
    );
    if (done) form.reset(values);
  });

  const inactiveSuffix = t("employees.inactiveSuffix", "(inactivo)");
  const label = (ref: EmployeeCatalogRef) => (ref.isActive ? ref.name : `${ref.name} ${inactiveSuffix}`);
  const none = t("employees.none", "Sin asignar");
  const { businessPartner: bp } = employee;
  const idTypeName = idTypes.data?.find((x) => x.code === bp.identificationType)?.name;

  const catalogSelect = (
    name: "jobPositionId" | "professionId",
    title: string,
    rows: EmployeeCatalogEntry[] | undefined,
    current: EmployeeCatalogRef | null,
  ) => (
    <ZHField label={title}>
      <ZhSelect disabled={saving || catalogs.loading} {...form.register(name)}>
        <option value="">{none}</option>
        {selectable(rows, current ? [current] : []).map((r) => (
          <option key={r.id} value={r.id}>
            {label(r)}
          </option>
        ))}
      </ZhSelect>
    </ZHField>
  );

  const jobTitle = t("employees.fields.jobPosition", "Cargo");
  const professionTitle = t("employees.fields.profession", "Profesión");
  const specialtiesTitle = t("employees.fields.specialties", "Especialidades");
  const specialtyOptions = selectable(catalogs.data?.specialties, employee.specialties);

  return (
    <div className="pg-page">
      <ZHCard title={t("employees.general.identity", "Identidad (Datos Maestros)")}>
        <ZHInfoRow label={t("employees.fields.name", "Nombre")} value={bp.displayName ?? "—"} />
        {idTypeName && (
          <ZHInfoRow label={t("employees.fields.identificationType", "Tipo de identificación")} value={idTypeName} />
        )}
        <ZHInfoRow
          label={t("employees.fields.identification", "Identificación")}
          value={bp.identificationNumber ?? "—"}
        />
      </ZHCard>

      <ZHCard title={t("employees.general.profile", "Datos laborales")}>
        {catalogs.error && <ZHPageNotice variant="error" message={catalogs.error} />}
        {permissions.update && !permissions.viewCatalogs && (
          <ZHPageNotice
            variant="info"
            message={t(
              "employees.general.noCatalogPermission",
              "Para editar cargo, profesión y especialidades necesita el permiso de consulta de catálogos laborales.",
            )}
          />
        )}
        {editable ? (
          <form onSubmit={submit} noValidate>
            <ZHGrid cols={1}>
              <ZHGrid cols={2}>
                {catalogSelect("jobPositionId", jobTitle, catalogs.data?.jobPositions, employee.jobPosition)}
                {catalogSelect("professionId", professionTitle, catalogs.data?.professions, employee.profession)}
              </ZHGrid>
              <ZHField label={specialtiesTitle}>
                <Controller
                  control={form.control}
                  name="specialtyIds"
                  render={({ field }) => (
                    <ZhSearchSelect<EmployeeCatalogRef>
                      mode="multiple"
                      aria-label={specialtiesTitle}
                      options={specialtyOptions}
                      value={specialtyOptions.filter((s) => field.value.includes(s.id))}
                      onChange={(next) => field.onChange(next.map((s) => s.id).sort())}
                      getOptionKey={(s) => s.id}
                      getOptionLabel={label}
                      portal
                      disabled={saving || catalogs.loading}
                    />
                  )}
                />
              </ZHField>
            </ZHGrid>
            <div className="zh-form-actions-row zh-form-actions-row--end">
              <ZHBtn type="submit" variant="primary" size="md" icon="save" disabled={saving || !isDirty}>
                {saving ? t("common.saving", "Guardando...") : t("common.save", "Guardar")}
              </ZHBtn>
            </div>
          </form>
        ) : (
          <>
            <ZHInfoRow label={jobTitle} value={employee.jobPosition ? label(employee.jobPosition) : none} />
            <ZHInfoRow label={professionTitle} value={employee.profession ? label(employee.profession) : none} />
            <ZHInfoRow
              wide
              label={specialtiesTitle}
              value={employee.specialties.length > 0 ? employee.specialties.map(label).join(", ") : none}
            />
          </>
        )}
      </ZHCard>

      <EmployeeEmploymentSection
        employee={employee}
        canDisable={permissions.disable}
        saving={saving}
        runWrite={runWrite}
        onDirtyChange={onDirtyChange}
      />
    </div>
  );
}
