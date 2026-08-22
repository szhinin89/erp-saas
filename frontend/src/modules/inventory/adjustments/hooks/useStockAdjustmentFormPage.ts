import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useI18n } from "../../../../i18n/i18n";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { message } from "../../../../lib/messages";
import { readApiErrorMessage } from "../../../lib/apiError";
import { applyServerErrors } from "../../../lib/validationErrors";
import { warehouseService } from "../../warehouses/api/warehouseService";
import type { WarehouseDto } from "../../warehouses/api/warehouseService";
import { stockService } from "../../stock/api/stockService";
import { inventoryAdjustmentReasonsService } from "../../adjustmentReasons/api/inventoryAdjustmentReasonsService";
import type { InventoryAdjustmentReasonDto } from "../../adjustmentReasons/types";
import { itemLookupFacade } from "../../../items/facades/itemLookupFacade";
import type { ItemPackagingLevelDto } from "../../../../types/items";
import {
  defaultStockAdjustmentHeaderValues,
  stockAdjustmentHeaderSchema,
  type StockAdjustmentHeaderValues,
} from "../../../../schemas/inventory/stockAdjustmentSchema";
import { stockAdjustmentsService } from "../api/stockAdjustmentsService";
import type { AdjustmentMovementType, StockAdjustmentDto } from "../types";
import {
  computeQuantityInBaseUom,
  isStockInsufficient,
  resolveConversionFactor,
  resolveLineUomCode,
} from "../utils/adjustmentLineMath";
import { useAdjustmentLifecycleActions } from "./useAdjustmentLifecycleActions";
import type { AdjustmentProductProfile } from "../components/AdjustmentProductPicker";

export type AdjustmentEditorLine = {
  _key: number;
  itemId: string;
  sku: string;
  itemName: string;
  baseUomCode: string;
  packagingLevels: ItemPackagingLevelDto[];
  packagingLevelId: string | null;
  quantity: number;
  /** Solo se edita/envía en Ingreso — en Egreso lo deriva el backend del promedio móvil. */
  unitCostBase: number | null;
  lineNotes: string;
  /** Stock actual en unidad base para la bodega elegida; null = aún desconocido (nunca 0 falso). */
  currentStock: number | null;
  /** Ya resuelto por el backend (solo en ajustes ejecutados). */
  totalCost: number | null;
};

/**
 * INVENTORY-ADJUSTMENTS-03 — Pantalla 2 (crear / editar borrador / ver). Un solo hook y una sola
 * ruta con el modo derivado de la ruta + el estado del documento, igual que hace Compras: sin `id`
 * es creación; con `id` en Draft es edición; con `id` en Executed/Cancelled es solo lectura
 * (`formLocked`), mismo criterio que `StockTransferPage`.
 *
 * El backend es la autoridad de todas las reglas (motivo activo y compatible, notas obligatorias,
 * stock suficiente). Aquí se replican solo como ayuda visual previa; en particular, el aviso de
 * stock insuficiente en Egreso NO bloquea el guardado del borrador — guardar un borrador no toca
 * stock, y el gate real ocurre al Ejecutar, en el servidor.
 */
export function useStockAdjustmentFormPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const canView = canShow("inventory.adjustments.view");
  const canCreate = canShow("inventory.adjustments.create");
  const canUpdate = canShow("inventory.adjustments.update");
  const canExecute = canShow("inventory.adjustments.confirm");
  const canCancel = canShow("inventory.adjustments.cancel");

  const [adjustment, setAdjustment] = useState<StockAdjustmentDto | null>(null);
  const [lines, setLines] = useState<AdjustmentEditorLine[]>([]);
  const [lineKey, setLineKey] = useState(1);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [reasons, setReasons] = useState<InventoryAdjustmentReasonDto[]>([]);
  const [loading, setLoading] = useState(!!id);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const form = useForm<StockAdjustmentHeaderValues>({
    resolver: zodResolver(stockAdjustmentHeaderSchema),
    defaultValues: defaultStockAdjustmentHeaderValues,
  });
  const { watch, setValue, reset } = form;

  const movementType = watch("movementType") as AdjustmentMovementType;
  const warehouseId = watch("warehouseId");
  const reasonId = watch("reasonId");

  const status = adjustment?.status ?? "Draft";
  const formLocked = adjustment !== null && adjustment.status !== "Draft";
  const isDraft = adjustment === null || adjustment.status === "Draft";

  // ── Catálogos ──────────────────────────────────────────────────────────────
  useEffect(() => {
    warehouseService
      .list("active")
      .then((list) => setWarehouses(list ?? []))
      .catch(() => setWarehouses([]));
    // includeInactive=true: un ajuste ya guardado puede apuntar a un motivo desactivado después;
    // se necesita el registro para poder mostrar su nombre en modo lectura.
    inventoryAdjustmentReasonsService
      .list(true)
      .then((list) => setReasons(list ?? []))
      .catch(() => setReasons([]));
  }, []);

  /** Solo motivos activos y compatibles con el tipo de movimiento ("Ambos" sirve para los dos). */
  const selectableReasons = useMemo(
    () =>
      reasons
        .filter(
          (r) =>
            r.isActive &&
            (r.allowedMovementType === movementType ||
              r.allowedMovementType === "Ambos"),
        )
        .sort((a, b) => a.sortOrder - b.sortOrder),
    [reasons, movementType],
  );

  const selectedReason = useMemo(
    () => reasons.find((r) => r.id === reasonId) ?? null,
    [reasons, reasonId],
  );

  const notesRequired = selectedReason?.requiresNotes === true;

  // Cambiar el tipo de movimiento invalida un motivo que ya no aplica — se limpia en vez de
  // enviar al backend una combinación que sabemos que rechazará.
  useEffect(() => {
    if (formLocked || !reasonId) return;
    const stillValid = selectableReasons.some((r) => r.id === reasonId);
    if (!stillValid) setValue("reasonId", "");
  }, [selectableReasons, reasonId, setValue, formLocked]);

  // ── Carga de un ajuste existente ───────────────────────────────────────────
  const loadAdjustment = useCallback(
    async (adjustmentId: string) => {
      setLoadError(null);
      setLoading(true);
      try {
        const dto = await stockAdjustmentsService.getById(adjustmentId);
        setAdjustment(dto);
        reset({
          movementType: dto.movementType,
          warehouseId: dto.warehouseId,
          reasonId: dto.reasonId,
          notes: dto.notes ?? "",
        });
        const editorLines = await Promise.all(
          dto.lines.map(async (l, index) => {
            let packagingLevels: ItemPackagingLevelDto[] = [];
            let sku = "";
            try {
              const detail = await itemLookupFacade.getById(l.itemId);
              packagingLevels = detail.packagingLevels.filter((p) => p.isActive);
              sku = detail.sku;
            } catch {
              // Best-effort: sin el detalle del ítem la línea se sigue mostrando con los datos
              // que ya trae el DTO (uom, factor, cantidades) — nunca se inventan presentaciones.
            }
            const line: AdjustmentEditorLine = {
              _key: index + 1,
              itemId: l.itemId,
              sku,
              itemName: l.itemName,
              baseUomCode: l.baseUomCode,
              packagingLevels,
              packagingLevelId: l.packagingLevelId,
              quantity: l.quantity,
              unitCostBase: l.unitCostBase,
              lineNotes: l.lineNotes ?? "",
              currentStock: l.currentStockBefore,
              totalCost: l.totalCost,
            };
            return line;
          }),
        );
        setLines(editorLines);
        setLineKey(editorLines.length + 1);
      } catch (err) {
        setLoadError(
          readApiErrorMessage(err) ??
            t(
              "inventory.adjustments.messages.loadError",
              "No se pudo cargar el ajuste.",
            ),
        );
      } finally {
        setLoading(false);
      }
    },
    [reset, t],
  );

  useEffect(() => {
    if (id) void loadAdjustment(id);
  }, [id, loadAdjustment]);

  // ── Stock actual por línea ────────────────────────────────────────────────
  const fetchCurrentStock = useCallback(
    async (
      itemId: string,
      forWarehouseId: string,
    ): Promise<{ quantity: number; averageCost: number } | null> => {
      if (!forWarehouseId) return null;
      try {
        const rows = await stockService.getStock(itemId, forWarehouseId);
        const row = rows?.find((r) => r.warehouseId === forWarehouseId);
        if (!row) return { quantity: 0, averageCost: 0 };
        return { quantity: row.quantity, averageCost: row.averageCost };
      } catch {
        // Best-effort: la advertencia visual de stock es una ayuda, no un requisito para operar.
        return null;
      }
    },
    [],
  );

  // Al cambiar de bodega, el stock cacheado por línea ya no corresponde: se vuelve a consultar.
  // El efecto depende solo de la bodega (no de `lines`, que se lee por ref) para no disparar una
  // consulta por cada tecla al editar una cantidad.
  const linesRef = useRef<AdjustmentEditorLine[]>([]);
  linesRef.current = lines;

  useEffect(() => {
    if (!warehouseId || formLocked) return;
    let cancelled = false;
    void (async () => {
      const snapshot = linesRef.current;
      if (snapshot.length === 0) return;
      const results = await Promise.all(
        snapshot.map((l) => fetchCurrentStock(l.itemId, warehouseId)),
      );
      if (cancelled) return;
      const byItem = new Map(
        snapshot.map((l, i) => [l.itemId, results[i]?.quantity ?? null]),
      );
      setLines((prev) =>
        prev.map((l) =>
          byItem.has(l.itemId)
            ? { ...l, currentStock: byItem.get(l.itemId) ?? null }
            : l,
        ),
      );
    })();
    return () => {
      cancelled = true;
    };
  }, [warehouseId, formLocked, fetchCurrentStock]);

  // ── Líneas ────────────────────────────────────────────────────────────────
  const addLine = useCallback(
    async (product: AdjustmentProductProfile) => {
      if (lines.some((l) => l.itemId === product.id)) {
        message.warning(
          t(
            "inventory.adjustments.messages.duplicateProduct",
            "Este producto ya está en el ajuste. Edite la cantidad en la línea existente.",
          ),
        );
        return;
      }

      let packagingLevels: ItemPackagingLevelDto[] = [];
      try {
        const detail = await itemLookupFacade.getById(product.id);
        packagingLevels = detail.packagingLevels.filter((p) => p.isActive);
      } catch {
        packagingLevels = [];
      }
      const stock = await fetchCurrentStock(product.id, warehouseId);

      setLines((prev) => [
        ...prev,
        {
          _key: lineKey,
          itemId: product.id,
          sku: product.sku,
          itemName: product.name,
          baseUomCode: product.baseUomCode,
          packagingLevels,
          packagingLevelId: null,
          quantity: 1,
          // Prellenado solo en Ingreso y solo si el stock actual expone un costo promedio real
          // (> 0); nunca se inventa un costo ni se prellena en Egreso, donde el backend manda.
          unitCostBase:
            movementType === "Ingreso" && (stock?.averageCost ?? 0) > 0
              ? (stock?.averageCost ?? null)
              : null,
          lineNotes: "",
          currentStock: stock?.quantity ?? null,
          totalCost: null,
        },
      ]);
      setLineKey((k) => k + 1);
    },
    [lines, lineKey, warehouseId, movementType, fetchCurrentStock, t],
  );

  const updateLine = useCallback(
    (key: number, patch: Partial<AdjustmentEditorLine>) => {
      setLines((prev) =>
        prev.map((l) => (l._key === key ? { ...l, ...patch } : l)),
      );
    },
    [],
  );

  const removeLine = useCallback((key: number) => {
    setLines((prev) => prev.filter((l) => l._key !== key));
  }, []);

  // ── Derivados de presentación ─────────────────────────────────────────────
  const lineViews = useMemo(
    () =>
      lines.map((line) => {
        const conversionFactor = resolveConversionFactor(
          line.packagingLevels,
          line.packagingLevelId,
        );
        const quantityInBaseUom = computeQuantityInBaseUom(
          line.quantity,
          conversionFactor,
        );
        return {
          line,
          conversionFactor,
          quantityInBaseUom,
          uomCode: resolveLineUomCode(
            line.packagingLevels,
            line.packagingLevelId,
            line.baseUomCode,
          ),
          hasPresentation: conversionFactor > 1,
          insufficientStock:
            movementType === "Egreso" &&
            isStockInsufficient(quantityInBaseUom, line.currentStock),
        };
      }),
    [lines, movementType],
  );

  const insufficientStockLines = useMemo(
    () => lineViews.filter((v) => v.insufficientStock),
    [lineViews],
  );

  /** Estimación cliente para Ingreso; el costo real lo fija el backend al Ejecutar. */
  const estimatedTotalCost = useMemo(
    () =>
      movementType === "Ingreso"
        ? lineViews.reduce(
            (sum, v) => sum + v.line.quantity * (v.line.unitCostBase ?? 0),
            0,
          )
        : 0,
    [lineViews, movementType],
  );

  const executedTotalCost = useMemo(
    () => (adjustment?.lines ?? []).reduce((s, l) => s + (l.totalCost ?? 0), 0),
    [adjustment],
  );

  // ── Guardar ───────────────────────────────────────────────────────────────
  const save = form.handleSubmit(async (values) => {
    setSaveError(null);

    if (lines.length === 0) {
      setSaveError(
        t(
          "inventory.adjustments.messages.addLine",
          "Agregue al menos una línea al ajuste.",
        ),
      );
      return;
    }
    if (lines.some((l) => !(l.quantity > 0))) {
      setSaveError(
        t(
          "inventory.adjustments.messages.quantityGreaterThanZero",
          "La cantidad de cada línea debe ser mayor a cero.",
        ),
      );
      return;
    }
    if (notesRequired && !(values.notes ?? "").trim()) {
      form.setError("notes", {
        type: "manual",
        message: t(
          "inventory.adjustments.messages.notesRequired",
          "El motivo seleccionado exige registrar una observación.",
        ),
      });
      return;
    }
    if (
      values.movementType === "Ingreso" &&
      lines.some((l) => l.unitCostBase === null || !(l.unitCostBase > 0))
    ) {
      setSaveError(
        t(
          "inventory.adjustments.messages.unitCostRequired",
          "En un Ingreso, cada línea requiere un costo unitario base mayor a cero.",
        ),
      );
      return;
    }

    const warehouseName =
      warehouses.find((w) => w.id === values.warehouseId)?.name ??
      adjustment?.warehouseName ??
      "";

    const payload = {
      warehouseId: values.warehouseId,
      warehouseName,
      movementType: values.movementType,
      reasonId: values.reasonId,
      notes: (values.notes ?? "").trim() || null,
      lines: lines.map((l) => ({
        itemId: l.itemId,
        itemName: l.itemName,
        packagingLevelId: l.packagingLevelId,
        quantity: l.quantity,
        // Egreso: el backend deriva el costo de RunningAverageCost y no acepta uno manual.
        unitCostBase:
          values.movementType === "Ingreso" ? (l.unitCostBase ?? null) : null,
        lineNotes: l.lineNotes.trim() || null,
      })),
    };

    setSaving(true);
    try {
      if (adjustment) {
        const updated = await stockAdjustmentsService.update(adjustment.id, {
          id: adjustment.id,
          ...payload,
        });
        setAdjustment(updated);
        message.success(
          t(
            "inventory.adjustments.messages.updated",
            "Borrador actualizado correctamente.",
          ),
        );
      } else {
        const created = await stockAdjustmentsService.create(payload);
        setAdjustment(created);
        message.success(
          t(
            "inventory.adjustments.messages.created",
            "Borrador de ajuste creado correctamente.",
          ),
        );
        navigate(`/inventory/adjustments/${created.id}`, { replace: true });
      }
    } catch (err) {
      const applied = applyServerErrors(err, form.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied) {
        setSaveError(
          readApiErrorMessage(err) ??
            t(
              "inventory.adjustments.messages.saveError",
              "No se pudo guardar el ajuste. Intente nuevamente.",
            ),
        );
      }
    } finally {
      setSaving(false);
    }
  });

  const lifecycle = useAdjustmentLifecycleActions(
    useCallback((updated: StockAdjustmentDto) => setAdjustment(updated), []),
  );

  return {
    t,
    id,
    navigate,
    canView,
    canCreate,
    canUpdate,
    canExecute,
    canCancel,
    form,
    errors: form.formState.errors,
    adjustment,
    status,
    isDraft,
    formLocked,
    loading,
    loadError,
    saving,
    saveError,
    warehouses,
    warehouseId,
    movementType,
    selectableReasons,
    selectedReason,
    notesRequired,
    lines,
    lineViews,
    addLine,
    updateLine,
    removeLine,
    insufficientStockLines,
    estimatedTotalCost,
    executedTotalCost,
    save,
    lifecycle,
  };
}
