import { useCallback, useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { readApiErrorMessage } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import {
  cashMovementReasonAdminSchema,
  defaultCashMovementReasonAdminValues,
  type CashMovementReasonAdminFormValues,
} from "../schemas/cashMovementReasonAdminSchema";
import { cajaService, type CashMovementReasonDto } from "../api/cajaService";

/**
 * TREASURY-CASH-MOVEMENT-REASONS-ADMIN-03 — catálogo administrable de motivos de movimiento
 * manual de caja (SSOT dinámico: los motivos viven en BD, nunca como enum en el frontend).
 *
 * Réplica exacta del patrón `useInventoryAdjustmentReasonsPage`: Lista → Editor con
 * `ConfigTabsLayout`, RHF + zodResolver, `applyServerErrors` para el 422 del backend (código
 * duplicado por Tenant+Company) y activar/desactivar por endpoint dedicado — nunca hay borrado
 * físico. Usa exclusivamente `cajaService` (API existente de TREASURY-CASH-MANUAL-MOVEMENTS-01),
 * sin duplicar ninguna validación ni endpoint nuevo.
 */
export function useCashMovementReasonsAdminPage() {
  const { canShow } = usePermissionsUi();

  const canView = canShow("caja.view");
  const canManage = canShow("caja.manage");

  const [items, setItems] = useState<CashMovementReasonDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [panelOpen, setPanelOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [toggling, setToggling] = useState(false);

  const form = useForm<CashMovementReasonAdminFormValues>({
    resolver: zodResolver(cashMovementReasonAdminSchema),
    defaultValues: defaultCashMovementReasonAdminValues,
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
      setItems((await cajaService.listCashMovementReasons(true)) ?? []);
    } catch (err) {
      setError(
        readApiErrorMessage(err) ?? "No se pudieron cargar los motivos de movimientos.",
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const totals = useMemo(
    () => ({
      total: items.length,
      active: items.filter((x) => x.isActive).length,
      inactive: items.filter((x) => !x.isActive).length,
    }),
    [items],
  );

  const openCreate = useCallback(() => {
    setEditingId(null);
    setSaveError("");
    reset(defaultCashMovementReasonAdminValues);
    setPanelOpen(true);
  }, [reset]);

  const openEdit = useCallback(
    (row: CashMovementReasonDto) => {
      setSaveError("");
      setEditingId(row.id);
      reset({
        code: row.code,
        name: row.name,
        movementType: row.movementType,
        sortOrder: row.sortOrder,
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

  const save = form.handleSubmit(async (values) => {
    setSaveError("");
    setSaving(true);
    try {
      if (editingId) {
        // TREASURY-CASH-MOVEMENT-REASONS-ADMIN-03A — `code` y `movementType` son inmutables tras
        // la creación: el comando de update no los incluye (aunque el formulario siga mostrando
        // el Tipo, solo lectura, para contexto).
        await cajaService.updateCashMovementReason(editingId, {
          id: editingId,
          name: values.name.trim(),
          sortOrder: values.sortOrder,
        });
        await fetchList();
        message.success("Motivo actualizado correctamente.");
      } else {
        // companyId no se envía: el backend lo resuelve del contexto autenticado.
        const created = await cajaService.createCashMovementReason({
          code: values.code.trim(),
          name: values.name.trim(),
          movementType: values.movementType,
          sortOrder: values.sortOrder,
        });
        await fetchList();
        setEditingId(created.id);
        message.success("Motivo creado correctamente.");
      }
    } catch (err) {
      const applied = applyServerErrors(err, form.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied) {
        setSaveError(readApiErrorMessage(err) ?? "No se pudo guardar el motivo.");
      }
    } finally {
      setSaving(false);
    }
  });

  const toggleStatus = useCallback(
    async (row: CashMovementReasonDto) => {
      if (!canManage) return;
      setError("");
      setToggling(true);
      try {
        await cajaService.toggleCashMovementReason(row.id);
        await fetchList();
        message.success(row.isActive ? "Motivo desactivado." : "Motivo activado.");
      } catch (err) {
        const msg = readApiErrorMessage(err) ?? "No se pudo cambiar el estado del motivo.";
        setError(msg);
        message.error(msg);
      } finally {
        setToggling(false);
      }
    },
    [canManage, fetchList],
  );

  return {
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
