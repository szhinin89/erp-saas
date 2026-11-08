import { useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { accountingApi, type AccountDto } from "../api/accountingApi";
import {
  ACCOUNT_NATURE_OPTIONS,
  ACCOUNT_TYPE_OPTIONS,
  createAccountSchema,
  editAccountSchema,
  emptyCreateAccountForm,
  type CreateAccountFormValues,
  type EditAccountFormValues,
} from "../schemas/accountSchema";
import { expectedParentCode, type AccountTree } from "../utils/accountTreeModel";

export type AccountFormIntent =
  | { kind: "create"; parent: AccountDto | null }
  | { kind: "edit"; account: AccountDto };

interface Props {
  intent: AccountFormIntent;
  tree: AccountTree;
  onClose: () => void;
  onSaved: (account: AccountDto) => void;
}

const FORM_ID = "acc-coa-account-form";
const ROOT_LABEL = "(cuenta raíz — sin padre)";

const typeLabel = (value: string) =>
  ACCOUNT_TYPE_OPTIONS.find((o) => o.value === value)?.label ?? value;
const natureLabel = (value: string) =>
  ACCOUNT_NATURE_OPTIONS.find((o) => o.value === value)?.label ?? value;
const accountLabel = (code: string, name: string) => `${code} · ${name}`;

/** Clase de cuenta ↔ `allowsPosting` (mismo dato del contrato; solo cambia el rótulo visible). */
const classOptions = (
  <>
    <option value="true">Movimiento — recibe asientos</option>
    <option value="false">Agrupadora — solo agrupa subcuentas</option>
  </>
);

/**
 * ACCOUNTING-CHART-UX-01 — crear, crear subcuenta y editar en `ZHModal` (modal-standard.md).
 * Mismos esquemas Zod, payloads, mensajes y `applyServerErrors` que el editor anterior. El padre
 * se deduce del código y se muestra en solo lectura: el servidor sigue siendo quien valida la
 * jerarquía (ValidateCanonicalParent); aquí no se agregan bloqueos de negocio.
 */
export function AccountFormModal({ intent, tree, onClose, onSaved }: Props) {
  const isEdit = intent.kind === "edit";
  const editing = intent.kind === "edit" ? intent.account : null;
  const presetParent = intent.kind === "create" ? intent.parent : null;
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const confirmOpenRef = useRef(false);

  const createForm = useForm<CreateAccountFormValues>({
    resolver: zodResolver(createAccountSchema),
    defaultValues: presetParent
      ? {
          ...emptyCreateAccountForm(),
          code: `${presetParent.code}.`,
          accountType: presetParent.accountType as CreateAccountFormValues["accountType"],
          nature: presetParent.nature as CreateAccountFormValues["nature"],
        }
      : emptyCreateAccountForm(),
  });
  const editForm = useForm<EditAccountFormValues>({
    resolver: zodResolver(editAccountSchema),
    defaultValues: editing
      ? { name: editing.name, parentAccountId: editing.parentAccountId ?? "", allowsPosting: editing.allowsPosting }
      : { name: "", parentAccountId: "", allowsPosting: true },
  });

  // ZHModal enfoca primero la X del encabezado; este efecto corre después y lleva el foco al
  // primer campo editable del formulario.
  useEffect(() => {
    document
      .getElementById(FORM_ID)
      ?.querySelector<HTMLElement>("input:not([disabled]),select:not([disabled])")
      ?.focus();
  }, []);

  const code = createForm.watch("code") ?? "";
  const parentCode = isEdit ? null : expectedParentCode(code);
  const derivedParent = parentCode
    ? [...tree.byId.values()].find((a) => a.code === parentCode) ?? null
    : null;

  const editAllowsPosting = editForm.watch("allowsPosting");
  const editChildCount = editing ? tree.childrenById.get(editing.id)?.length ?? 0 : 0;
  const isDirty = isEdit ? editForm.formState.isDirty : createForm.formState.isDirty;

  const requestClose = async () => {
    if (saving || confirmOpenRef.current) return;
    if (isDirty) {
      confirmOpenRef.current = true;
      const discard = await message
        .confirm({
          title: "Descartar cambios",
          message: "Hay cambios sin guardar en este formulario. Si continúa, se perderán.",
          variant: "warning",
          confirmLabel: "Descartar cambios",
          cancelLabel: "Seguir editando",
        })
        .finally(() => {
          confirmOpenRef.current = false;
        });
      if (!discard) return;
    }
    onClose();
  };

  const onCreate = createForm.handleSubmit(async (values) => {
    setSaveError("");
    setSaving(true);
    try {
      const saved = await accountingApi.createAccount({
        code: values.code,
        name: values.name,
        parentAccountId: derivedParent?.id ?? null,
        accountType: values.accountType,
        nature: values.nature,
        allowsPosting: values.allowsPosting,
      });
      message.success("Cuenta creada correctamente.");
      onSaved(saved);
    } catch (err: unknown) {
      const applied = applyServerErrors(err, createForm.setError, (msg) => setSaveError(msg));
      if (!applied) setSaveError(formatApiRequestError(err, { generic: "No se pudo crear la cuenta." }));
    } finally {
      setSaving(false);
    }
  });

  const onEdit = editForm.handleSubmit(async (values) => {
    if (!editing) return;
    setSaveError("");
    setSaving(true);
    try {
      const saved = await accountingApi.updateAccount(editing.id, {
        id: editing.id,
        name: values.name,
        parentAccountId: editing.parentAccountId,
        allowsPosting: values.allowsPosting,
      });
      message.success("Cuenta actualizada correctamente.");
      onSaved(saved);
    } catch (err: unknown) {
      const applied = applyServerErrors(err, editForm.setError, (msg) => setSaveError(msg));
      if (!applied)
        setSaveError(formatApiRequestError(err, { generic: "No se pudo actualizar la cuenta." }));
    } finally {
      setSaving(false);
    }
  });

  const title = isEdit
    ? `Editar cuenta — ${editing!.code}`
    : presetParent
      ? "Nueva subcuenta"
      : "Nueva cuenta";
  const subtitle = presetParent
    ? `Dentro de: ${accountLabel(presetParent.code, presetParent.name)}`
    : isEdit
      ? editing!.name
      : "La cuenta padre se deduce del código contable.";

  const parentField = isEdit ? (
    <ZHField label="Cuenta padre" readOnly>
      <ZhTextInput
        value={
          editing!.parentAccountCode
            ? accountLabel(editing!.parentAccountCode, editing!.parentAccountName ?? "")
            : ROOT_LABEL
        }
        disabled
      />
    </ZHField>
  ) : (
    <ZHField
      label="Cuenta padre"
      readOnly
      hint={
        parentCode && !derivedParent
          ? `No existe la cuenta agrupadora «${parentCode}». El servidor validará el código al guardar.`
          : "Se deduce del código contable."
      }
      hintType={parentCode && !derivedParent ? "warning" : "muted"}
    >
      <ZhTextInput
        value={
          derivedParent
            ? accountLabel(derivedParent.code, derivedParent.name)
            : parentCode
              ? `${parentCode} (no encontrada)`
              : ROOT_LABEL
        }
        disabled
      />
    </ZHField>
  );

  return (
    <ZHModal
      open
      size="lg"
      title={title}
      subtitle={subtitle}
      closeOnBackdrop={false}
      onClose={() => void requestClose()}
      footer={
        <>
          <ZHBtn type="button" variant="ghost" onClick={() => void requestClose()} disabled={saving}>
            Cancelar
          </ZHBtn>
          <ZHBtn type="submit" form={FORM_ID} variant="primary" disabled={saving}>
            {saving ? "Guardando..." : isEdit ? "Actualizar" : "Crear"}
          </ZHBtn>
        </>
      }
    >
      <form
        id={FORM_ID}
        noValidate
        onSubmit={(e) => {
          e.preventDefault();
          void (isEdit ? onEdit() : onCreate());
        }}
      >
        {saveError && <ZHPageNotice variant="error" message={saveError} />}
        {isEdit ? (
          <ZHGrid cols={2}>
            <ZHField label="Código" readOnly>
              <ZhTextInput className="zh-input--upper" value={editing!.code} disabled />
            </ZHField>
            <ZHField label="Nombre" required fieldError={editForm.formState.errors.name?.message}>
              <ZhTextInput maxLength={150} disabled={saving} {...editForm.register("name")} />
            </ZHField>
            <ZHField label="Tipo" readOnly>
              <ZhTextInput value={typeLabel(editing!.accountType)} disabled />
            </ZHField>
            <ZHField label="Naturaleza" readOnly>
              <ZhTextInput value={natureLabel(editing!.nature)} disabled />
            </ZHField>
            {parentField}
            <ZHField label="Clase">
              <ZhSelect
                disabled={saving}
                {...editForm.register("allowsPosting", { setValueAs: (v) => v === true || v === "true" })}
              >
                {classOptions}
              </ZhSelect>
            </ZHField>
          </ZHGrid>
        ) : (
          <ZHGrid cols={2}>
            <ZHField label="Código" required fieldError={createForm.formState.errors.code?.message}>
              <ZhTextInput
                className="zh-input--upper"
                maxLength={30}
                disabled={saving}
                {...createForm.register("code")}
              />
            </ZHField>
            <ZHField label="Nombre" required fieldError={createForm.formState.errors.name?.message}>
              <ZhTextInput maxLength={150} disabled={saving} {...createForm.register("name")} />
            </ZHField>
            <ZHField label="Tipo de cuenta" required fieldError={createForm.formState.errors.accountType?.message}>
              <ZhSelect disabled={saving} {...createForm.register("accountType")}>
                {ACCOUNT_TYPE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
            <ZHField label="Naturaleza" required fieldError={createForm.formState.errors.nature?.message}>
              <ZhSelect disabled={saving} {...createForm.register("nature")}>
                {ACCOUNT_NATURE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
            {parentField}
            <ZHField label="Clase">
              <ZhSelect
                disabled={saving}
                {...createForm.register("allowsPosting", { setValueAs: (v) => v === true || v === "true" })}
              >
                {classOptions}
              </ZhSelect>
            </ZHField>
          </ZHGrid>
        )}

        {/* ACCOUNTING-CHART-INTEGRITY-01 (pendiente): avisos informativos, sin bloqueo propio. */}
        {editing && editing.allowsPosting && editAllowsPosting === false && (
          <ZHPageNotice
            variant="warning"
            message="La cuenta dejará de recibir asientos."
            detail="Si está asignada en reglas de contabilización u otras configuraciones contables, los registros automáticos que la usen podrían fallar. Este aviso es informativo y no sustituye las validaciones del servidor."
          />
        )}
        {editing && !editing.allowsPosting && editAllowsPosting === true && editChildCount > 0 && (
          <ZHPageNotice
            variant="warning"
            message={`La cuenta tiene ${editChildCount} subcuenta(s).`}
            detail="Una cuenta con subcuentas normalmente es agrupadora. Este aviso es informativo y no sustituye las validaciones del servidor."
          />
        )}
      </form>
    </ZHModal>
  );
}
