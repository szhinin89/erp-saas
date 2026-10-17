import { useCallback, useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useI18n } from "../../../../i18n/i18n";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { message } from "../../../../lib/messages";
import { readApiErrorMessage } from "../../../lib/apiError";
import { applyServerErrors } from "../../../lib/validationErrors";
import {
  adjustmentReasonSchema,
  defaultAdjustmentReasonValues,
  type AdjustmentReasonFormValues,
} from "../../../../schemas/inventory/adjustmentReasonSchema";
import { inventoryAdjustmentReasonsService } from "../api/inventoryAdjustmentReasonsService";
import {
  accountLookupFacade,
  type AccountDto,
} from "../../../accounting/facades/accountLookupFacade";
import type { InventoryAdjustmentReasonDto } from "../types";

/**
 * INVENTORY-ADJUSTMENTS-03 — catálogo administrable de motivos de ajuste (SSOT dinámico:
 * los motivos viven en BD, nunca como enum en el frontend).
 *
 * Réplica del patrón de `useWarehousesPage`: Lista → Editor con `ConfigTabsLayout`, RHF +
 * zodResolver, `applyServerErrors` para el 422 del backend (p. ej. código duplicado) y
 * activar/desactivar por endpoint dedicado en vez de un campo del formulario — nunca hay borrado
 * físico.
 */
export function useInventoryAdjustmentReasonsPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();

  const canView = canShow("inventory.adjustment-reasons.view");
  const canManage = canShow("inventory.adjustment-reasons.manage");

  const [items, setItems] = useState<InventoryAdjustmentReasonDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [panelOpen, setPanelOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [toggling, setToggling] = useState(false);
  const [accounts, setAccounts] = useState<AccountDto[]>([]);

  // ACCOUNTING-12A — cuentas imputables para la contrapartida contable del motivo.
  useEffect(() => {
    accountLookupFacade
      .listAccounts()
      .then((rows) => setAccounts(rows.filter((a) => a.isActive && a.allowsPosting)))
      .catch(() => setAccounts([]));
  }, []);

  const form = useForm<AdjustmentReasonFormValues>({
    resolver: zodResolver(adjustmentReasonSchema),
    defaultValues: defaultAdjustmentReasonValues,
  });
  const {
    reset,
    formState: { errors },
  } = form;

  const fetchList = useCallback(async () => {
    setError("");
    setLoading(true);
    try {
      // includeInactive=true: la pantalla de configuración administra activos e inactivos.
      setItems((await inventoryAdjustmentReasonsService.list(true)) ?? []);
    } catch (err) {
      setError(
        readApiErrorMessage(err) ??
          t(
            "inventory.adjustmentReasons.messages.listError",
            "No se pudieron cargar los motivos de ajuste.",
          ),
      );
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const totals = useMemo(
    () => ({
      total: items.length,
      active: items.filter((x) => x.isActive).length,
      inactive: items.filter((x) => !x.isActive).length,
      requiringNotes: items.filter((x) => x.requiresNotes).length,
    }),
    [items],
  );

  const openCreate = useCallback(() => {
    setEditingId(null);
    setSaveError("");
    reset(defaultAdjustmentReasonValues);
    setPanelOpen(true);
  }, [reset]);

  const openEdit = useCallback(
    (row: InventoryAdjustmentReasonDto) => {
      setSaveError("");
      setEditingId(row.id);
      reset({
        code: row.code,
        name: row.name,
        allowedMovementType: row.allowedMovementType,
        requiresNotes: row.requiresNotes,
        sortOrder: row.sortOrder,
        accountingAccountId: row.accountingAccountId ?? "",
      });
      setPanelOpen(true);
    },
    [reset],
  );

  const closePanel = useCallback(() => {
    setPanelOpen(false);
    setEditingId(null);
    setSaveError("");
  }, []);

  // ACCOUNTING-12A — la cuenta es configuración de la empresa activa (endpoint propio): solo se envía
  // si cambió; no existe "quitar" una cuenta configurada (sin borrado físico), solo reemplazarla.
  const saveAccount = useCallback(
    async (reasonId: string, accountingAccountId: string) => {
      const current = items.find((x) => x.id === reasonId)?.accountingAccountId ?? "";
      if (!accountingAccountId || accountingAccountId === current) return;
      await inventoryAdjustmentReasonsService.setAccount(reasonId, { accountingAccountId });
    },
    [items],
  );

  const save = form.handleSubmit(async (values) => {
    setSaveError("");
    setSaving(true);
    try {
      if (editingId) {
        // `code` es inmutable tras la creación — el comando de update no lo incluye.
        await inventoryAdjustmentReasonsService.update(editingId, {
          id: editingId,
          name: values.name.trim(),
          allowedMovementType: values.allowedMovementType,
          requiresNotes: values.requiresNotes,
          sortOrder: values.sortOrder,
        });
        await saveAccount(editingId, values.accountingAccountId);
        await fetchList();
        message.success(
          t(
            "inventory.adjustmentReasons.messages.updated",
            "Motivo de ajuste actualizado correctamente.",
          ),
        );
      } else {
        // companyId no se envía: el backend lo resuelve del contexto autenticado.
        const created = await inventoryAdjustmentReasonsService.create({
          code: values.code.trim(),
          name: values.name.trim(),
          allowedMovementType: values.allowedMovementType,
          requiresNotes: values.requiresNotes,
          sortOrder: values.sortOrder,
        });
        await saveAccount(created.id, values.accountingAccountId);
        await fetchList();
        setEditingId(created.id);
        message.success(
          t(
            "inventory.adjustmentReasons.messages.created",
            "Motivo de ajuste creado correctamente.",
          ),
        );
      }
    } catch (err) {
      const applied = applyServerErrors(err, form.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied) {
        setSaveError(
          readApiErrorMessage(err) ??
            t(
              "inventory.adjustmentReasons.messages.saveError",
              "No se pudo guardar el motivo de ajuste.",
            ),
        );
      }
    } finally {
      setSaving(false);
    }
  });

  const toggleStatus = useCallback(
    async (row: InventoryAdjustmentReasonDto) => {
      if (!canManage) return;
      setError("");
      setToggling(true);
      try {
        await inventoryAdjustmentReasonsService.toggle(row.id, !row.isActive);
        await fetchList();
        message.success(
          row.isActive
            ? t(
                "inventory.adjustmentReasons.messages.disabled",
                "Motivo desactivado.",
              )
            : t(
                "inventory.adjustmentReasons.messages.enabled",
                "Motivo activado.",
              ),
        );
      } catch (err) {
        const msg =
          readApiErrorMessage(err) ??
          t(
            "inventory.adjustmentReasons.messages.toggleError",
            "No se pudo cambiar el estado del motivo.",
          );
        setError(msg);
        message.error(msg);
      } finally {
        setToggling(false);
      }
    },
    [canManage, fetchList, t],
  );

  return {
    t,
    canView,
    canManage,
    items,
    loading,
    error,
    totals,
    panelOpen,
    editingId,
    saving,
    saveError,
    toggling,
    accounts,
    hasConfiguredAccount: !!items.find((x) => x.id === editingId)?.accountingAccountId,
    form,
    errors,
    fetchList,
    openCreate,
    openEdit,
    closePanel,
    save,
    toggleStatus,
  };
}
