import { useCallback, useEffect, useState } from "react";
import { warehouseService } from "../../warehouses/api/warehouseService";
import type { WarehouseDto } from "../../warehouses/api/warehouseService";
import { stockService } from "../../stock/api/stockService";
import { branchLookupFacade } from "../../../branches/facades/branchLookupFacade";
import type { BranchListItemDto } from "../../../branches/facades/branchLookupFacade";
import {
  stockTransferService,
  type StockTransferDto,
} from "../api/stockTransferService";
import type { TransferProductProfile } from "../components/TransferProductPicker";
import { message } from "../../../../lib/messages";
import { readApiErrorMessage } from "../../../lib/apiError";
import { useI18n } from "../../../../i18n/i18n";

/**
 * El backend valida stock insuficiente en el dominio (`CurrentStock.ApplyMovement`, compartido
 * por ajustes y transferencias) con un mensaje técnico en inglés ("Insufficient stock") — se
 * traduce aquí al texto claro que pide la pantalla, sin tocar ese dominio compartido (fuera de
 * alcance de esta tarea). Cualquier otro error del backend se muestra tal cual llega.
 */
function friendlyConfirmError(
  err: unknown,
  t: (key: string, fallback?: string) => string,
): string {
  const raw = readApiErrorMessage(err) ?? "";
  if (raw.toLowerCase().includes("insufficient stock"))
    return t(
      "inventory.transfers.messages.insufficientStock",
      "No hay stock suficiente en la bodega origen.",
    );
  return (
    raw ||
    t(
      "inventory.transfers.messages.confirmError",
      "No se pudo confirmar la transferencia. Intente nuevamente.",
    )
  );
}

export type TransferLine = {
  _key: number;
  productId: string;
  sku: string;
  name: string;
  quantity: number;
  /** Disponible en la bodega origen al momento de agregar la línea — null si no se pudo
   * consultar (no bloquea: el backend sigue siendo la autoridad real al confirmar). */
  availableAtSource: number | null;
};

/**
 * P1-INVENTORY-WAREHOUSE-TRANSFER-UI-01 — el backend (StockController) solo expone crear
 * (queda en "Draft") y confirmar ("Draft" → "Confirmed"); no hay listado/detalle/cancelación.
 * Este hook refleja exactamente ese contrato: no inventa un tercer estado ni una acción de
 * cancelar contra el servidor (StockTransfer.Cancel() existe en el dominio pero no está
 * expuesto por ningún endpoint) — "Cancelar/limpiar" es puramente un reset del formulario local.
 */
export function useStockTransferPage() {
  const { t } = useI18n();
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [branches, setBranches] = useState<BranchListItemDto[]>([]);
  const [sourceWarehouseId, setSourceWarehouseId] = useState("");
  const [targetWarehouseId, setTargetWarehouseId] = useState("");
  const [reason, setReason] = useState("");
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<TransferLine[]>([]);
  const [lineKey, setLineKey] = useState(1);

  const [transfer, setTransfer] = useState<StockTransferDto | null>(null);
  const [creating, setCreating] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  useEffect(() => {
    warehouseService
      .list("active")
      .then(setWarehouses)
      .catch(() => setWarehouses([]));
    branchLookupFacade
      .list("active")
      .then(setBranches)
      .catch(() => setBranches([]));
  }, []);

  const branchNameForWarehouse = useCallback(
    (warehouseId: string | undefined): string | null => {
      const wh = warehouses.find((w) => w.id === warehouseId);
      if (!wh) return null;
      return branches.find((b) => b.id === wh.branchId)?.name ?? null;
    },
    [warehouses, branches],
  );

  const sourceWarehouse = warehouses.find((w) => w.id === sourceWarehouseId);
  const targetWarehouse = warehouses.find((w) => w.id === targetWarehouseId);
  const sameWarehouse =
    !!sourceWarehouseId &&
    !!targetWarehouseId &&
    sourceWarehouseId === targetWarehouseId;

  const totalUnits = lines.reduce((sum, l) => sum + (l.quantity || 0), 0);
  const isDraft = transfer !== null && transfer.status === "Draft";
  const isConfirmed = transfer !== null && transfer.status === "Confirmed";
  const formLocked = transfer !== null; // ya se creó — backend no soporta editar un Draft

  const addLine = useCallback(
    async (product: TransferProductProfile) => {
      if (lines.some((l) => l.productId === product.id)) {
        message.warning(
          t(
            "inventory.transfers.messages.duplicateProduct",
            "Este producto ya fue agregado. Edite la cantidad en la línea existente.",
          ),
        );
        return;
      }

      let availableAtSource: number | null = null;
      if (sourceWarehouseId) {
        try {
          const options = await stockService.getWarehouseAvailability(product.id);
          availableAtSource =
            options.find((o) => o.warehouseId === sourceWarehouseId)?.available ?? 0;
        } catch {
          availableAtSource = null;
        }
      }

      setLines((prev) => [
        ...prev,
        {
          _key: lineKey,
          productId: product.id,
          sku: product.sku,
          name: product.name,
          quantity: availableAtSource !== null ? Math.min(1, availableAtSource) || 1 : 1,
          availableAtSource,
        },
      ]);
      setLineKey((k) => k + 1);
    },
    [lines, lineKey, sourceWarehouseId, t],
  );

  const updateLineQuantity = useCallback((key: number, quantity: number) => {
    setLines((prev) => prev.map((l) => (l._key === key ? { ...l, quantity } : l)));
  }, []);

  const removeLine = useCallback((key: number) => {
    setLines((prev) => prev.filter((l) => l._key !== key));
  }, []);

  const validate = useCallback((): string | null => {
    if (!sourceWarehouseId)
      return t("inventory.transfers.messages.selectSourceWarehouse", "Seleccione una bodega origen.");
    if (!targetWarehouseId)
      return t("inventory.transfers.messages.selectTargetWarehouse", "Seleccione una bodega destino.");
    if (sourceWarehouseId === targetWarehouseId)
      return t(
        "inventory.transfers.messages.sameWarehouse",
        "La bodega origen y destino no pueden ser la misma.",
      );
    if (lines.length === 0)
      return t("inventory.transfers.messages.addProduct", "Agregue al menos un producto.");
    for (const line of lines) {
      if (line.quantity <= 0)
        return t(
          "inventory.transfers.messages.quantityGreaterThanZero",
          "La cantidad debe ser mayor a cero.",
        );
      if (line.availableAtSource !== null && line.quantity > line.availableAtSource)
        return t(
          "inventory.transfers.messages.insufficientStock",
          "No hay stock suficiente en la bodega origen.",
        );
    }
    return null;
  }, [sourceWarehouseId, targetWarehouseId, lines, t]);

  const createTransfer = useCallback(async () => {
    setError(null);
    setSuccessMessage(null);
    const validationError = validate();
    if (validationError) {
      setError(validationError);
      return;
    }
    setCreating(true);
    try {
      const created = await stockTransferService.create({
        sourceWarehouseId,
        targetWarehouseId,
        reason: reason.trim() || null,
        notes: notes.trim() || null,
        lines: lines.map((l) => ({
          productId: l.productId,
          quantity: l.quantity,
          description: `${l.sku} — ${l.name}`,
        })),
      });
      setTransfer(created);
    } catch (err) {
      setError(
        readApiErrorMessage(err) ??
          t(
            "inventory.transfers.messages.createError",
            "No se pudo crear la transferencia. Intente nuevamente.",
          ),
      );
    } finally {
      setCreating(false);
    }
  }, [validate, sourceWarehouseId, targetWarehouseId, reason, notes, lines, t]);

  const confirmTransfer = useCallback(async () => {
    if (!transfer) return;
    setError(null);
    setConfirming(true);
    try {
      const confirmed = await stockTransferService.confirm(transfer.id);
      setTransfer(confirmed);
      setSuccessMessage(
        t("inventory.transfers.messages.confirmed", "Transferencia confirmada correctamente."),
      );
    } catch (err) {
      setError(friendlyConfirmError(err, t));
    } finally {
      setConfirming(false);
    }
  }, [transfer, t]);

  const resetForm = useCallback(() => {
    setSourceWarehouseId("");
    setTargetWarehouseId("");
    setReason("");
    setNotes("");
    setLines([]);
    setLineKey(1);
    setTransfer(null);
    setError(null);
    setSuccessMessage(null);
  }, []);

  return {
    warehouses,
    sourceWarehouseId,
    setSourceWarehouseId,
    targetWarehouseId,
    setTargetWarehouseId,
    sourceWarehouse,
    targetWarehouse,
    branchNameForWarehouse,
    sameWarehouse,
    reason,
    setReason,
    notes,
    setNotes,
    lines,
    addLine,
    updateLineQuantity,
    removeLine,
    totalUnits,
    transfer,
    isDraft,
    isConfirmed,
    formLocked,
    creating,
    confirming,
    error,
    successMessage,
    createTransfer,
    confirmTransfer,
    resetForm,
  };
}
