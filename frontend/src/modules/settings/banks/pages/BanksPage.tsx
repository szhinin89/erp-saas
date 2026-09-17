import { useCallback, useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { PageShell, Badge } from "../../../../components/PageShell";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../../components/zh/ZHIconButton";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { ZhTextInput } from "../../../../components/zh/inputs";
import { message } from "../../../../lib/messages";
import { formatApiRequestError } from "../../../lib/apiError";
import { applyServerErrors } from "../../../lib/validationErrors";
import { bankService, type BankDto } from "../api/bankService";
import {
  createBankSchema,
  editBankSchema,
  emptyCreateBankForm,
  type CreateBankFormValues,
  type EditBankFormValues,
} from "../schemas/bankSchema";

import "../../../../styles/shared/items-catalog.css";

type Mode = "list" | "create" | "edit";

/**
 * BANK-CATALOG-01 — Configuración > Catálogos > Bancos. Auditoría de reutilización: mismo patrón
 * de ChartOfAccountsPage.tsx (PageShell+ZHCard+ZHDataTable+RHF/Zod+ZHField/ZHGrid+
 * applyServerErrors), sin componentes nuevos. Catálogo global del tenant (sin CompanyId, sin
 * cuenta contable) — solo CRUD básico + activar/desactivar, nunca borrado físico.
 */
export function BanksPage() {
  const [banks, setBanks] = useState<BankDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [mode, setMode] = useState<Mode>("list");
  const [editing, setEditing] = useState<BankDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [search, setSearch] = useState("");
  const [togglingId, setTogglingId] = useState<string | null>(null);

  const createForm = useForm<CreateBankFormValues>({
    resolver: zodResolver(createBankSchema),
    defaultValues: emptyCreateBankForm(),
  });
  const editForm = useForm<EditBankFormValues>({
    resolver: zodResolver(editBankSchema),
    defaultValues: { name: "", shortName: "" },
  });

  const fetchBanks = useCallback(async () => {
    setLoading(true);
    try {
      const list = await bankService.list(false, search || undefined);
      setBanks(list);
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: "No se pudieron cargar los bancos." }));
    } finally {
      setLoading(false);
    }
  }, [search]);

  useEffect(() => {
    void fetchBanks();
  }, [fetchBanks]);

  const openCreate = () => {
    setEditing(null);
    createForm.reset(emptyCreateBankForm());
    setSaveError("");
    setMode("create");
  };

  const openEdit = (b: BankDto) => {
    setEditing(b);
    editForm.reset({ name: b.name, shortName: b.shortName ?? "" });
    setSaveError("");
    setMode("edit");
  };

  const handleCancel = () => {
    setEditing(null);
    setSaveError("");
    setMode("list");
  };

  const onCreateValid = createForm.handleSubmit(async (values) => {
    setSaveError("");
    setSaving(true);
    try {
      await bankService.create({
        code: values.code,
        name: values.name,
        shortName: values.shortName || null,
      });
      message.success("Banco creado correctamente.");
      setMode("list");
      void fetchBanks();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, createForm.setError, (msg) => setSaveError(msg));
      if (!applied) setSaveError(formatApiRequestError(err, { generic: "No se pudo crear el banco." }));
    } finally {
      setSaving(false);
    }
  });

  const onEditValid = editForm.handleSubmit(async (values) => {
    if (!editing) return;
    setSaveError("");
    setSaving(true);
    try {
      await bankService.update(editing.id, {
        id: editing.id,
        name: values.name,
        shortName: values.shortName || null,
      });
      message.success("Banco actualizado correctamente.");
      setMode("list");
      setEditing(null);
      void fetchBanks();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, editForm.setError, (msg) => setSaveError(msg));
      if (!applied)
        setSaveError(formatApiRequestError(err, { generic: "No se pudo actualizar el banco." }));
    } finally {
      setSaving(false);
    }
  });

  const handleToggleActive = async (b: BankDto) => {
    if (togglingId) return;

    const confirmed = await message.confirm({
      title: b.isActive ? `Desactivar "${b.name}"` : `Activar "${b.name}"`,
      message: b.isActive
        ? `"${b.name}" dejará de estar disponible para seleccionarse en cuentas bancarias/destinos financieros. Los registros existentes no se eliminan.`
        : `"${b.name}" volverá a estar disponible para seleccionarse.`,
      variant: b.isActive ? "danger" : "warning",
      confirmLabel: b.isActive ? "Desactivar" : "Activar",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;

    setTogglingId(b.id);
    try {
      if (b.isActive) await bankService.disable(b.id);
      else await bankService.enable(b.id);
      message.success(b.isActive ? "Banco desactivado correctamente." : "Banco activado correctamente.");
      void fetchBanks();
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: "No se pudo cambiar el estado del banco." }));
    } finally {
      setTogglingId(null);
    }
  };

  const columns: ZHDataTableColumn<BankDto>[] = [
    {
      key: "code",
      header: "Código",
      render: (row) => <code className="prd-sku">{row.code}</code>,
    },
    { key: "name", header: "Nombre", render: (row) => row.name },
    { key: "shortName", header: "Nombre corto", render: (row) => row.shortName ?? "—" },
    { key: "countryCode", header: "País", render: (row) => row.countryCode },
    {
      key: "isActive",
      header: "Estado",
      render: (row) => (
        <Badge label={row.isActive ? "Activo" : "Inactivo"} variant={row.isActive ? "green" : "gray"} />
      ),
    },
    {
      key: "actions",
      header: "",
      align: "right",
      render: (row) => (
        <div className="prd-row-actions">
          <ZHIconButton icon="edit" title="Editar" variant="ghost" onClick={() => openEdit(row)} />
          <ZHIconButton
            icon={row.isActive ? "toggle_on" : "toggle_off"}
            title={row.isActive ? "Desactivar" : "Activar"}
            variant="ghost"
            disabled={togglingId === row.id}
            onClick={() => void handleToggleActive(row)}
          />
        </div>
      ),
    },
  ];

  return (
    <PageShell
      kicker="Configuración"
      title="Bancos"
      subtitle="Catálogo maestro de bancos, seleccionable desde cuentas bancarias/destinos financieros"
      action={
        mode === "list" ? (
          <ZHBtn type="button" variant="primary" onClick={openCreate}>
            <span className="material-symbols-outlined">add</span>
            Nuevo banco
          </ZHBtn>
        ) : undefined
      }
    >
      {mode === "list" && (
        <ZHCard
          title="Listado"
          actions={
            <div className="coa-list-filters">
              <ZhTextInput
                placeholder="Buscar por código o nombre..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
              <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void fetchBanks()} disabled={loading}>
                Actualizar
              </ZHBtn>
            </div>
          }
        >
          <ZHDataTable
            columns={columns}
            rows={banks}
            rowKey={(row) => row.id}
            loading={loading}
            showRowNumber
            emptyMessage="No hay bancos registrados."
          />
        </ZHCard>
      )}

      {mode === "create" && (
        <ZHCard title="Nuevo banco">
          {saveError && <ZHPageNotice variant="error" message={saveError} />}
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
            <ZHField
              label="Nombre corto"
              fieldError={createForm.formState.errors.shortName?.message}
            >
              <ZhTextInput maxLength={50} disabled={saving} {...createForm.register("shortName")} />
            </ZHField>
            <ZHField label="País">
              <ZhTextInput value="EC" disabled />
            </ZHField>
          </ZHGrid>
          <div className="prd-crud-actions">
            <ZHBtn variant="primary" size="md" onClick={() => void onCreateValid()} disabled={saving}>
              {saving ? "Guardando..." : "Crear"}
            </ZHBtn>
            <ZHBtn variant="ghost" size="md" onClick={handleCancel}>
              Cancelar
            </ZHBtn>
          </div>
        </ZHCard>
      )}

      {mode === "edit" && editing && (
        <ZHCard title={`Editar banco — ${editing.code}`}>
          {saveError && <ZHPageNotice variant="error" message={saveError} />}
          <ZHGrid cols={2}>
            <ZHField label="Código" readOnly>
              <ZhTextInput className="zh-input--upper" value={editing.code} disabled />
            </ZHField>
            <ZHField label="País" readOnly>
              <ZhTextInput value={editing.countryCode} disabled />
            </ZHField>
            <ZHField label="Nombre" required fieldError={editForm.formState.errors.name?.message}>
              <ZhTextInput maxLength={150} disabled={saving} {...editForm.register("name")} />
            </ZHField>
            <ZHField label="Nombre corto" fieldError={editForm.formState.errors.shortName?.message}>
              <ZhTextInput maxLength={50} disabled={saving} {...editForm.register("shortName")} />
            </ZHField>
          </ZHGrid>
          <div className="prd-crud-actions">
            <ZHBtn variant="primary" size="md" onClick={() => void onEditValid()} disabled={saving}>
              {saving ? "Guardando..." : "Actualizar"}
            </ZHBtn>
            <ZHBtn variant="ghost" size="md" onClick={handleCancel}>
              Cancelar
            </ZHBtn>
          </div>
        </ZHCard>
      )}
    </PageShell>
  );
}
