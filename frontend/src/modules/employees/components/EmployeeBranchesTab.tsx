import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useI18n } from "../../../i18n/i18n";
import { useAsync } from "../../../hooks/useAsync";
import { formatDate, todayIso } from "../../../lib/formatters/dateFormatters";
import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { branchLookupFacade } from "../../branches/facades/branchLookupFacade";
import { employeeService } from "../api/employeeService";
import {
  assignmentSchema,
  closeAssignmentSchema,
  type AssignmentValues,
  type CloseAssignmentValues,
} from "../schemas/employeeSchemas";
import type { RunEmployeeWrite } from "../pages/useEmployeeDetailPage";
import type { EmployeeAssignment, EmployeeDto } from "../types/employee.types";
import { EmployeeActionModal, type ReportDirty } from "./EmployeeEmploymentSection";

type AssignmentSituation = "current" | "future" | "past";

/** Fechas inclusivas "YYYY-MM-DD" comparadas con hoy de la empresa. */
function assignmentSituation(
  assignment: Pick<EmployeeAssignment, "startDate" | "endDate">,
  today: string,
): AssignmentSituation {
  if (assignment.startDate > today) return "future";
  return assignment.endDate && assignment.endDate < today ? "past" : "current";
}

type ModalProps = {
  employee: EmployeeDto;
  saving: boolean;
  runWrite: RunEmployeeWrite;
  onDirtyChange: ReportDirty;
  onClose: () => void;
};

function CreateAssignmentModal({ employee, saving, runWrite, onDirtyChange, onClose }: ModalProps) {
  const { t } = useI18n();
  const branches = useAsync(() => branchLookupFacade.list("active"));
  const form = useForm<AssignmentValues>({
    resolver: zodResolver(assignmentSchema(t)),
    defaultValues: { branchId: "", startDate: todayIso(), endDate: "" },
  });
  const { errors, isDirty } = form.formState;

  const submit = form.handleSubmit(async (values) => {
    const done = await runWrite(
      async () => {
        await employeeService.createAssignment(employee.id, {
          branchId: values.branchId,
          startDate: values.startDate,
          endDate: values.endDate || undefined,
        });
      },
      {
        success: t("employees.branches.created", "Asignación registrada correctamente."),
        error: t("employees.branches.createError", "No se pudo registrar la asignación."),
      },
    );
    if (done) onClose();
  });

  return (
    <EmployeeActionModal
      dirtyKey="assignment"
      title={t("employees.branches.new", "Nueva asignación")}
      subtitle={employee.businessPartner.displayName ?? undefined}
      submitLabel={t("employees.branches.assign", "Asignar")}
      saving={saving}
      dirty={isDirty}
      onDirtyChange={onDirtyChange}
      onSubmit={submit}
      onClose={onClose}
    >
      {branches.error && <ZHPageNotice variant="error" message={branches.error} />}
      <ZHField
        label={t("employees.fields.branch", "Sucursal")}
        required
        fieldError={errors.branchId?.message}
      >
        <ZhSelect disabled={saving || branches.loading} {...form.register("branchId")}>
          <option value="">{t("employees.select", "Seleccione...")}</option>
          {(branches.data ?? []).map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </ZhSelect>
      </ZHField>
      <ZHField
        label={t("employees.branches.startDate", "Desde")}
        required
        fieldError={errors.startDate?.message}
      >
        <ZhDateInput disabled={saving} {...form.register("startDate")} />
      </ZHField>
      <ZHField
        label={t("employees.branches.endDate", "Hasta")}
        hint={t("employees.branches.endDateHint", "Opcional. Sin fecha queda abierta.")}
        fieldError={errors.endDate?.message}
      >
        <ZhDateInput disabled={saving} {...form.register("endDate")} />
      </ZHField>
    </EmployeeActionModal>
  );
}

function CloseAssignmentModal({
  employee,
  assignment,
  saving,
  runWrite,
  onDirtyChange,
  onClose,
}: ModalProps & { assignment: EmployeeAssignment }) {
  const { t } = useI18n();
  const today = todayIso();
  const form = useForm<CloseAssignmentValues>({
    resolver: zodResolver(closeAssignmentSchema(t, assignment.startDate)),
    defaultValues: { endDate: today >= assignment.startDate ? today : assignment.startDate },
  });
  const { errors, isDirty } = form.formState;
  const title = t("employees.branches.close", "Cerrar asignación");

  const submit = form.handleSubmit(async (values) => {
    const done = await runWrite(
      async () => {
        await employeeService.closeAssignment(employee.id, assignment.id, {
          rowVersion: assignment.rowVersion,
          endDate: values.endDate,
        });
      },
      {
        success: t("employees.branches.closed", "Asignación cerrada correctamente."),
        error: t("employees.branches.closeError", "No se pudo cerrar la asignación."),
      },
    );
    if (done) onClose();
  });

  return (
    <EmployeeActionModal
      dirtyKey="close-assignment"
      title={title}
      subtitle={assignment.branchName}
      submitLabel={title}
      saving={saving}
      dirty={isDirty}
      onDirtyChange={onDirtyChange}
      onSubmit={submit}
      onClose={onClose}
    >
      <ZHField
        label={t("employees.branches.endDate", "Hasta")}
        required
        fieldError={errors.endDate?.message}
      >
        <ZhDateInput disabled={saving} min={assignment.startDate} {...form.register("endDate")} />
      </ZHField>
    </EmployeeActionModal>
  );
}

type Props = {
  employee: EmployeeDto;
  permissions: { manageAssignments: boolean; viewBranches: boolean };
  saving: boolean;
  runWrite: RunEmployeeWrite;
  onDirtyChange: ReportDirty;
};

export function EmployeeBranchesTab({ employee, permissions, saving, runWrite, onDirtyChange }: Props) {
  const { t } = useI18n();
  const [creating, setCreating] = useState(false);
  const [closingId, setClosingId] = useState<string | null>(null);
  const today = todayIso();
  // Por id: tras recargar, el cierre usa siempre la versión vigente de la asignación.
  const closing = employee.assignments.find((a) => a.id === closingId && !a.endDate);
  const canCreate = permissions.manageAssignments && permissions.viewBranches;

  const situations: Record<AssignmentSituation, [string, BadgeVariant]> = {
    current: [t("employees.branches.current", "Vigente"), "success"],
    future: [t("employees.branches.future", "Futura"), "info"],
    past: [t("employees.branches.past", "Histórica"), "neutral"],
  };

  const columns: ZHDataTableColumn<EmployeeAssignment>[] = [
    {
      key: "branch",
      header: t("employees.fields.branch", "Sucursal"),
      render: (a) => (a.branchCode ? `${a.branchCode} · ${a.branchName}` : a.branchName),
    },
    {
      key: "startDate",
      header: t("employees.branches.startDate", "Desde"),
      render: (a) => formatDate(a.startDate),
    },
    {
      key: "endDate",
      header: t("employees.branches.endDate", "Hasta"),
      render: (a) => (a.endDate ? formatDate(a.endDate) : t("employees.branches.openEnded", "Sin fecha de fin")),
    },
    {
      key: "situation",
      header: t("employees.branches.situation", "Situación"),
      render: (a) => {
        const [label, variant] = situations[assignmentSituation(a, today)];
        return <Badge size="md" label={label} variant={variant} />;
      },
    },
    ...(permissions.manageAssignments
      ? [
          {
            key: "actions",
            header: t("employees.fields.actions", "Acciones"),
            align: "right" as const,
            render: (a: EmployeeAssignment) =>
              a.endDate ? null : (
                <ZHBtn
                  type="button"
                  variant="ghost"
                  size="sm"
                  icon="event_busy"
                  disabled={saving}
                  aria-label={`${t("employees.branches.close", "Cerrar asignación")} ${a.branchName}`}
                  onClick={() => setClosingId(a.id)}
                >
                  {t("employees.branches.closeShort", "Cerrar")}
                </ZHBtn>
              ),
          },
        ]
      : []),
  ];

  const modalProps = { employee, saving, runWrite, onDirtyChange };

  return (
    <ZHCard
      title={t("employees.branches.title", "Asignaciones a sucursales")}
      actions={
        canCreate ? (
          <ZHBtn
            type="button"
            variant="primary"
            size="sm"
            icon="add"
            disabled={!employee.isActive || saving}
            onClick={() => setCreating(true)}
          >
            {t("employees.branches.new", "Nueva asignación")}
          </ZHBtn>
        ) : undefined
      }
    >
      {canCreate && !employee.isActive && (
        <ZHPageNotice
          variant="info"
          message={t(
            "employees.branches.inactiveEmployee",
            "Un empleado de baja no admite nuevas asignaciones.",
          )}
        />
      )}
      {permissions.manageAssignments && !permissions.viewBranches && (
        <ZHPageNotice
          variant="info"
          message={t(
            "employees.branches.noBranchPermission",
            "Para crear asignaciones necesita el permiso de consulta de sucursales.",
          )}
        />
      )}
      <ZHDataTable
        columns={columns}
        rows={employee.assignments}
        rowKey={(a) => a.id}
        emptyMessage={t("employees.branches.empty", "No hay asignaciones a sucursales.")}
      />
      {creating && <CreateAssignmentModal {...modalProps} onClose={() => setCreating(false)} />}
      {closing && (
        <CloseAssignmentModal {...modalProps} assignment={closing} onClose={() => setClosingId(null)} />
      )}
    </ZHCard>
  );
}
