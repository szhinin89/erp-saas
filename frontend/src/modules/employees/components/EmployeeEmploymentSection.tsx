import { useEffect, useId, useState, type FormEventHandler, type ReactNode } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useI18n } from "../../../i18n/i18n";
import {
  releaseDiscardApproval,
  useDiscardConfirmation,
} from "../../../hooks/useUnsavedChangesGuard";
import { formatDate, todayIso } from "../../../lib/formatters/dateFormatters";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhTextarea } from "../../../components/zh/inputs/ZhTextarea";
import { employeeService } from "../api/employeeService";
import {
  REASON_MAX_LENGTH,
  rehireSchema,
  terminateSchema,
  type RehireValues,
  type TerminateValues,
} from "../schemas/employeeSchemas";
import type { RunEmployeeWrite } from "../pages/useEmployeeDetailPage";
import type { EmployeeDto, EmployeePeriod } from "../types/employee.types";

/** Cada formulario de la ficha informa sus cambios; la página mantiene el guard único. */
export type ReportDirty = (key: string, dirty: boolean) => void;

type ActionModalProps = {
  dirtyKey: string;
  title: string;
  subtitle?: string;
  submitLabel: string;
  destructive?: boolean;
  saving: boolean;
  dirty: boolean;
  onDirtyChange: ReportDirty;
  onSubmit: FormEventHandler<HTMLFormElement>;
  onClose: () => void;
  children: ReactNode;
};

/** Diálogo de las acciones de la ficha: `ZHModal` + formulario + descarte confirmado. */
export function EmployeeActionModal({
  dirtyKey,
  title,
  subtitle,
  submitLabel,
  destructive,
  saving,
  dirty,
  onDirtyChange,
  onSubmit,
  onClose,
  children,
}: ActionModalProps) {
  const { t } = useI18n();
  const formId = useId();
  const confirmDiscard = useDiscardConfirmation();

  useEffect(() => {
    onDirtyChange(dirtyKey, dirty);
    return () => onDirtyChange(dirtyKey, false);
  }, [dirtyKey, dirty, onDirtyChange]);

  const close = async () => {
    if (saving) return;
    if (dirty && !(await confirmDiscard())) return;
    onClose();
    releaseDiscardApproval();
  };

  return (
    <ZHModal
      open
      size="md"
      closeOnBackdrop={false}
      title={title}
      subtitle={subtitle}
      closeLabel={t("common.close", "Cerrar")}
      onClose={() => void close()}
      footer={
        <>
          <ZHBtn type="button" variant="ghost" size="md" disabled={saving} onClick={() => void close()}>
            {t("common.cancel", "Cancelar")}
          </ZHBtn>
          <ZHBtn
            type="submit"
            form={formId}
            size="md"
            variant={destructive ? "destructive" : "primary"}
            disabled={saving}
          >
            {saving ? t("common.saving", "Guardando...") : submitLabel}
          </ZHBtn>
        </>
      }
    >
      <form id={formId} onSubmit={onSubmit} noValidate>
        <ZHGrid cols={1}>{children}</ZHGrid>
      </form>
    </ZHModal>
  );
}

type FormModalProps = {
  employee: EmployeeDto;
  saving: boolean;
  runWrite: RunEmployeeWrite;
  onDirtyChange: ReportDirty;
  onClose: () => void;
};

function TerminateModal({ employee, saving, runWrite, onDirtyChange, onClose }: FormModalProps) {
  const { t } = useI18n();
  const periodStart = employee.periods.find((p) => !p.endDate)?.startDate ?? "";
  const today = todayIso();
  const form = useForm<TerminateValues>({
    resolver: zodResolver(terminateSchema(t, periodStart)),
    defaultValues: { endDate: today >= periodStart ? today : periodStart, reason: "" },
  });
  const { errors, isDirty } = form.formState;
  const title = t("employees.employment.terminate", "Dar de baja");

  const submit = form.handleSubmit(async (values) => {
    const done = await runWrite(
      () =>
        employeeService.terminate(employee.id, {
          rowVersion: employee.rowVersion,
          endDate: values.endDate,
          reason: values.reason.trim() || undefined,
        }),
      {
        success: t("employees.employment.terminated", "Baja registrada correctamente."),
        error: t("employees.employment.terminateError", "No se pudo registrar la baja."),
      },
    );
    if (done) onClose();
  });

  return (
    <EmployeeActionModal
      dirtyKey="terminate"
      title={title}
      subtitle={employee.businessPartner.displayName ?? undefined}
      submitLabel={title}
      destructive
      saving={saving}
      dirty={isDirty}
      onDirtyChange={onDirtyChange}
      onSubmit={submit}
      onClose={onClose}
    >
      <ZHPageNotice
        variant="warning"
        message={t(
          "employees.employment.terminateNotice",
          "Se cerrará el período laboral y las asignaciones vigentes a sucursales. El acceso al ERP no se revoca.",
        )}
      />
      <ZHField
        label={t("employees.fields.endDate", "Fecha de fin")}
        required
        fieldError={errors.endDate?.message}
      >
        <ZhDateInput disabled={saving} min={periodStart} {...form.register("endDate")} />
      </ZHField>
      <ZHField label={t("employees.fields.reason", "Motivo")} fieldError={errors.reason?.message}>
        <ZhTextarea disabled={saving} rows={3} maxLength={REASON_MAX_LENGTH} {...form.register("reason")} />
      </ZHField>
    </EmployeeActionModal>
  );
}

function RehireModal({ employee, saving, runWrite, onDirtyChange, onClose }: FormModalProps) {
  const { t } = useI18n();
  const lastEnd = employee.periods.reduce((max, p) => (p.endDate && p.endDate > max ? p.endDate : max), "");
  const today = todayIso();
  const form = useForm<RehireValues>({
    resolver: zodResolver(rehireSchema(t, lastEnd)),
    defaultValues: { startDate: today > lastEnd ? today : "" },
  });
  const { errors, isDirty } = form.formState;
  const title = t("employees.employment.rehire", "Reincorporar");

  const submit = form.handleSubmit(async (values) => {
    const done = await runWrite(
      () =>
        employeeService.rehire(employee.id, {
          rowVersion: employee.rowVersion,
          startDate: values.startDate,
        }),
      {
        success: t("employees.employment.rehired", "Empleado reincorporado correctamente."),
        error: t("employees.employment.rehireError", "No se pudo reincorporar al empleado."),
      },
    );
    if (done) onClose();
  });

  return (
    <EmployeeActionModal
      dirtyKey="rehire"
      title={title}
      subtitle={employee.businessPartner.displayName ?? undefined}
      submitLabel={title}
      saving={saving}
      dirty={isDirty}
      onDirtyChange={onDirtyChange}
      onSubmit={submit}
      onClose={onClose}
    >
      <ZHPageNotice
        variant="info"
        message={t(
          "employees.employment.rehireNotice",
          "Se abre un período nuevo. Las asignaciones anteriores a sucursales no se reabren.",
        )}
      />
      <ZHField
        label={t("employees.fields.startDate", "Fecha de ingreso")}
        required
        fieldError={errors.startDate?.message}
      >
        <ZhDateInput disabled={saving} {...form.register("startDate")} />
      </ZHField>
    </EmployeeActionModal>
  );
}

type Props = {
  employee: EmployeeDto;
  canDisable: boolean;
  saving: boolean;
  runWrite: RunEmployeeWrite;
  onDirtyChange: ReportDirty;
};

/** Estado laboral, historial de períodos, baja y reincorporación. */
export function EmployeeEmploymentSection({ employee, canDisable, saving, runWrite, onDirtyChange }: Props) {
  const { t } = useI18n();
  const [dialog, setDialog] = useState<"terminate" | "rehire" | null>(null);

  const columns: ZHDataTableColumn<EmployeePeriod>[] = [
    {
      key: "startDate",
      header: t("employees.fields.startDate", "Fecha de ingreso"),
      render: (p) => formatDate(p.startDate),
    },
    {
      key: "endDate",
      header: t("employees.fields.endDate", "Fecha de fin"),
      render: (p) => (p.endDate ? formatDate(p.endDate) : t("employees.employment.open", "Vigente")),
    },
    {
      key: "reason",
      header: t("employees.fields.reason", "Motivo"),
      render: (p) => p.terminationReason ?? "—",
    },
  ];

  const modalProps = { employee, saving, runWrite, onDirtyChange, onClose: () => setDialog(null) };

  return (
    <ZHCard
      title={t("employees.employment.title", "Historial laboral")}
      actions={
        canDisable ? (
          employee.isActive ? (
            <ZHBtn type="button" variant="destructive" size="sm" icon="person_off" onClick={() => setDialog("terminate")}>
              {t("employees.employment.terminate", "Dar de baja")}
            </ZHBtn>
          ) : (
            <ZHBtn type="button" variant="primary" size="sm" icon="person_add" onClick={() => setDialog("rehire")}>
              {t("employees.employment.rehire", "Reincorporar")}
            </ZHBtn>
          )
        ) : undefined
      }
    >
      <ZHDataTable
        columns={columns}
        rows={employee.periods}
        rowKey={(p) => p.id}
        emptyMessage={t("employees.employment.empty", "No hay períodos registrados.")}
      />
      {dialog === "terminate" && <TerminateModal {...modalProps} />}
      {dialog === "rehire" && <RehireModal {...modalProps} />}
    </ZHCard>
  );
}
