import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useI18n } from "../../../i18n/i18n";
import { useAsync } from "../../../hooks/useAsync";
import { Badge } from "../../../components/PageShell";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHConfirmModal } from "../../../components/zh/ZHConfirmModal";
import { ZhSearchSelect } from "../../../components/zh/inputs/ZhSearchSelect";
import {
  membershipLookupFacade,
  type CompanyUserMembershipAdminDto,
} from "../../access/facades/membershipLookupFacade";
import { employeeService } from "../api/employeeService";
import type { RunEmployeeWrite } from "../pages/useEmployeeDetailPage";
import type { EmployeeDto } from "../types/employee.types";
import { EmployeeActionModal, type ReportDirty } from "./EmployeeEmploymentSection";

type ModalProps = {
  employee: EmployeeDto;
  saving: boolean;
  runWrite: RunEmployeeWrite;
  onDirtyChange: ReportDirty;
  onClose: () => void;
};

function LinkUserModal({ employee, saving, runWrite, onDirtyChange, onClose }: ModalProps) {
  const { t } = useI18n();
  // Solo membresías activas de la empresa; el backend ya excluye las cuentas protegidas.
  const memberships = useAsync(() => membershipLookupFacade.list(true));
  const form = useForm<{ identityUserId: string }>({
    resolver: zodResolver(
      z.object({
        identityUserId: z
          .string()
          .min(1, t("employees.validation.userRequired", "Seleccione un usuario.")),
      }),
    ),
    defaultValues: { identityUserId: "" },
  });
  const { errors, isDirty } = form.formState;
  const title = t("employees.access.link", "Vincular usuario");

  const submit = form.handleSubmit(async ({ identityUserId }) => {
    const done = await runWrite(
      () => employeeService.linkUser(employee.id, { identityUserId, rowVersion: employee.rowVersion }),
      {
        success: t("employees.access.linked", "Usuario vinculado correctamente."),
        error: t("employees.access.linkError", "No se pudo vincular el usuario."),
      },
    );
    if (done) onClose();
  });

  const userLabel = t("employees.access.user", "Usuario ERP");

  return (
    <EmployeeActionModal
      dirtyKey="link-user"
      title={title}
      subtitle={employee.businessPartner.displayName ?? undefined}
      submitLabel={title}
      saving={saving}
      dirty={isDirty}
      onDirtyChange={onDirtyChange}
      onSubmit={submit}
      onClose={onClose}
    >
      {memberships.error && <ZHPageNotice variant="error" message={memberships.error} />}
      <ZHField label={userLabel} required fieldError={errors.identityUserId?.message}>
        <Controller
          control={form.control}
          name="identityUserId"
          render={({ field }) => (
            <ZhSearchSelect<CompanyUserMembershipAdminDto>
              aria-label={userLabel}
              options={memberships.data ?? []}
              value={memberships.data?.find((m) => m.identityUserId === field.value) ?? null}
              onChange={(m) => field.onChange(m?.identityUserId ?? "")}
              getOptionKey={(m) => m.identityUserId}
              getOptionLabel={(m) => m.fullName || m.username}
              getOptionDescription={(m) => m.username}
              getOptionSearchText={(m) => `${m.fullName} ${m.username}`}
              placeholder={t("employees.access.userPlaceholder", "Buscar usuario de la empresa")}
              emptyText={t("employees.access.userEmpty", "Sin usuarios que coincidan.")}
              portal
              disabled={saving || memberships.loading}
            />
          )}
        />
      </ZHField>
    </EmployeeActionModal>
  );
}

type Props = {
  employee: EmployeeDto;
  permissions: { linkUser: boolean; viewMemberships: boolean };
  saving: boolean;
  runWrite: RunEmployeeWrite;
  onDirtyChange: ReportDirty;
};

export function EmployeeAccessTab({ employee, permissions, saving, runWrite, onDirtyChange }: Props) {
  const { t } = useI18n();
  const [linking, setLinking] = useState(false);
  const [confirmUnlink, setConfirmUnlink] = useState(false);
  const user = employee.identityUser;
  // Vincular exige ambos permisos; desvincular solo el de vinculación.
  const canLink = permissions.linkUser && permissions.viewMemberships;

  const unlink = async () => {
    setConfirmUnlink(false);
    await runWrite(() => employeeService.unlinkUser(employee.id, employee.rowVersion), {
      success: t("employees.access.unlinked", "Usuario desvinculado correctamente."),
      error: t("employees.access.unlinkError", "No se pudo desvincular el usuario."),
    });
  };

  return (
    <ZHCard title={t("employees.access.title", "Acceso ERP")}>
      {user ? (
        <>
          <ZHInfoRow label={t("employees.access.user", "Usuario ERP")} value={user.username} />
          <ZHInfoRow
            label={t("employees.access.membership", "Membresía en la empresa")}
            value={
              <Badge
                size="md"
                variant={user.membershipActive ? "success" : "warning"}
                label={
                  user.membershipActive
                    ? t("employees.access.membershipActive", "Activa")
                    : t("employees.access.membershipInactive", "Inactiva")
                }
              />
            }
          />
        </>
      ) : (
        <ZHPageNotice
          variant="neutral"
          message={t("employees.access.noVisibleUser", "No hay un usuario vinculado visible.")}
          detail={t(
            "employees.access.noVisibleUserDetail",
            "Puede existir un vínculo con una cuenta protegida que no se muestra desde la empresa.",
          )}
        />
      )}
      {permissions.linkUser && !permissions.viewMemberships && !user && (
        <ZHPageNotice
          variant="info"
          message={t(
            "employees.access.noMembershipPermission",
            "Para vincular un usuario necesita además el permiso de consulta de usuarios de la empresa.",
          )}
        />
      )}
      {permissions.linkUser && (
        <div className="zh-form-actions-row zh-form-actions-row--end">
          <ZHBtn
            type="button"
            variant="ghost"
            size="md"
            icon="link_off"
            disabled={saving}
            onClick={() => setConfirmUnlink(true)}
          >
            {t("employees.access.unlink", "Desvincular usuario")}
          </ZHBtn>
          {canLink && !user && (
            <ZHBtn
              type="button"
              variant="primary"
              size="md"
              icon="link"
              disabled={saving}
              onClick={() => setLinking(true)}
            >
              {t("employees.access.link", "Vincular usuario")}
            </ZHBtn>
          )}
        </div>
      )}
      {linking && (
        <LinkUserModal
          employee={employee}
          saving={saving}
          runWrite={runWrite}
          onDirtyChange={onDirtyChange}
          onClose={() => setLinking(false)}
        />
      )}
      <ZHConfirmModal
        open={confirmUnlink}
        variant="danger"
        title={t("employees.access.unlink", "Desvincular usuario")}
        message={t(
          "employees.access.unlinkConfirm",
          "Se quitará el vínculo de esta ficha. La cuenta, la membresía y los permisos no cambian.",
        )}
        confirmLabel={t("employees.access.unlink", "Desvincular usuario")}
        cancelLabel={t("common.cancel", "Cancelar")}
        onConfirm={() => void unlink()}
        onCancel={() => setConfirmUnlink(false)}
      />
    </ZHCard>
  );
}
