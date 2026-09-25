import { useCallback, useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { cajaService } from "../api/cajaService";
import type { CashMovementReasonDto } from "../api/cajaService";
import { manualCashMovementTypeOptions } from "../constants/cashMovementTypes";
import {
  recordMovementSchema,
  emptyMovementForm,
  type RecordMovementFormValues,
} from "../schemas/cajaSchema";
import { operationalPreferencesService } from "../../configuracion/operaciones/api/operationalPreferencesService";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useAuthStore } from "../../../store/authStore";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import { formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";

export type UseManualCashMovementFlowArgs = {
  /** Id de la sesión de caja contra la que se registra el movimiento — null si no hay ninguna
   * (turno cerrado/no cargado/no existente). Nunca se resuelve dentro de este hook: cada
   * consumidor (Caja, Ventas) lo obtiene de su propio contexto operativo ya autenticado. */
  cashSessionId: string | null | undefined;
  /** true solo si esa sesión está realmente abierta — lo decide el llamador, nunca este hook. */
  isSessionOpen: boolean;
  /** Se invoca tras un registro exitoso, ANTES de mostrar el toast de éxito, para que cada
   * pantalla refresque exactamente lo que le corresponde (detalle+resumen+mySession en Caja;
   * nada, o solo un indicador propio, en Ventas — nunca el documento de venta en curso). */
  onRecorded?: () => void | Promise<void>;
};

/**
 * SALES-MANUAL-CASH-MOVEMENT-INTEGRATION-08 — extraído tal cual de useCajaPage.tsx (mismo
 * comportamiento, mismas keys i18n, mismo endpoint, mismas reglas fail-closed) para que Ventas
 * pueda reutilizarlo sin duplicar el flujo. useCajaPage.tsx ahora es un consumidor más de este
 * hook (ver ahí), no una copia — un solo lugar de verdad para "registrar movimiento manual".
 *
 * Este hook NO decide Tenant/CompanyId (los resuelve el backend del contexto autenticado, igual
 * que antes) ni si hay turno abierto (responsabilidad del llamador, vía `isSessionOpen`) — ver
 * TREASURY-CASH-MANUAL-MOVEMENTS-COMPANY-SETTING-05/-PERMISSION-06, sin cambios de esas reglas.
 */
export function useManualCashMovementFlow({
  cashSessionId,
  isSessionOpen,
  onRecorded,
}: UseManualCashMovementFlowArgs) {
  const moneyDecimals = usePrecisionDecimals("money"); // presentación (04F)
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canRecordManualMovements = canShow("caja.record");
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");

  // ── TREASURY-CASH-COMPANY-SETTING-05A — fail-closed: oculto mientras carga, si falla, o si el
  // valor es false; solo true tras confirmar explícitamente que la empresa lo permite. Se recarga
  // al cambiar de empresa activa. ──
  const [allowManualMovements, setAllowManualMovements] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setAllowManualMovements(false);
    operationalPreferencesService
      .getPreferences()
      .then((dto) => {
        if (!cancelled) setAllowManualMovements(dto.cash.allowManualInOutMovements);
      })
      .catch(() => {
        if (!cancelled) setAllowManualMovements(false);
      });
    return () => {
      cancelled = true;
    };
  }, [companySessionVersion]);

  const movementForm = useForm<RecordMovementFormValues>({
    resolver: zodResolver(recordMovementSchema(t)),
    defaultValues: emptyMovementForm(),
    mode: "onBlur",
  });

  const movementTypes = useMemo(() => manualCashMovementTypeOptions(t), [t]);

  const [reasons, setReasons] = useState<CashMovementReasonDto[]>([]);
  const [reasonsLoading, setReasonsLoading] = useState(false);
  const selectedMovementType = movementForm.watch("movementType");

  useEffect(() => {
    if (!selectedMovementType) {
      setReasons([]);
      return;
    }
    let cancelled = false;
    setReasonsLoading(true);
    cajaService
      .getCashMovementReasons(selectedMovementType)
      .then((items) => {
        if (!cancelled) setReasons(items);
      })
      .catch(() => {
        if (!cancelled) setReasons([]);
      })
      .finally(() => {
        if (!cancelled) setReasonsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [selectedMovementType]);

  // El motivo elegido para un Tipo ya no es válido si el usuario cambia el Tipo — se limpia para
  // no enviar un ReasonId incompatible (el backend lo rechazaría de todas formas, fail-closed).
  useEffect(() => {
    movementForm.setValue("reasonId", "");
  }, [selectedMovementType]);

  const [movementModalOpen, setMovementModalOpen] = useState(false);

  const openMovementModal = useCallback(() => {
    // Defensa en profundidad (TREASURY-CASH-MANUAL-MOVEMENTS-PERMISSION-06): aunque el botón que
    // dispara esto ya está oculto si falta cualquiera de las tres condiciones (empresa lo
    // permite, usuario tiene `caja.record`, turno abierto), nunca abrir el modal si por algún
    // motivo se invoca igual — el backend rechazaría el submit de todas formas (fail-closed real,
    // 403 sin permiso o 422 por configuración de empresa).
    if (!allowManualMovements || !canRecordManualMovements || !isSessionOpen || !cashSessionId)
      return;
    movementForm.reset(emptyMovementForm());
    setSaveError("");
    setMovementModalOpen(true);
  }, [allowManualMovements, canRecordManualMovements, isSessionOpen, cashSessionId]);

  const closeMovementModal = useCallback(() => {
    if (saving) return;
    movementForm.reset(emptyMovementForm());
    setSaveError("");
    setMovementModalOpen(false);
  }, [saving]);

  // CRITICAL-CONFIRMATIONS-CASH-02: un ingreso/egreso manual afecta el saldo de caja de
  // inmediato — se confirma antes de ejecutar (tipo, concepto y monto), con variant warning
  // para ingreso y danger para egreso/retiro (mayor riesgo de descuadre).
  const handleRecordMovement = movementForm.handleSubmit(async (data) => {
    if (!cashSessionId || saving) return;

    const typeLabel =
      movementTypes.find((mt) => mt.value === data.movementType)?.label ?? data.movementType;
    const reasonLabel = reasons.find((r) => r.id === data.reasonId)?.name ?? "";
    const isIncome = data.movementType === "ManualIncome";

    const confirmed = await message.confirm({
      title: isIncome ? t("caja.session.incomeTitle") : t("caja.session.expenseTitle"),
      message: (
        <p className="zh-confirm-message">
          {t("caja.movements.table.type")}: <strong>{typeLabel}</strong>
          <br />
          {t("caja.movements.table.reason")}: <strong>{reasonLabel}</strong>
          <br />
          {t("caja.session.concept")}: <strong>{data.description}</strong>
          <br />
          {t("caja.movements.table.amount")}: <strong>{formatMoneyWithSymbol(data.amount, moneyDecimals)}</strong>
        </p>
      ),
      variant: isIncome ? "warning" : "danger",
      confirmLabel: isIncome ? t("caja.session.recordIncome") : t("caja.session.recordExpense"),
      cancelLabel: t("common.cancel"),
    });
    if (!confirmed) return;

    setSaveError("");
    setSaving(true);
    try {
      await cajaService.recordMovement(cashSessionId, {
        movementType: data.movementType,
        reasonId: data.reasonId,
        amount: data.amount,
        description: data.description,
      });
      movementForm.reset(emptyMovementForm());
      setMovementModalOpen(false);
      await onRecorded?.();
      message.success(t("caja.session.movementSuccess"));
    } catch (err: unknown) {
      // A diferencia de abrir/cerrar caja, aquí el ticket exige explícitamente un toast
      // message.error con el mensaje real del backend — se muestra siempre (además de resaltar
      // el campo específico vía applyServerErrors cuando el 422 viene mapeado por campo).
      applyServerErrors(err, movementForm.setError, () => {});
      const errorMessage = formatApiRequestError(err, {
        unauthorized: t("caja.session.unauthorized"),
        generic: t("caja.session.movementError"),
      });
      setSaveError(errorMessage);
      message.error(errorMessage);
    }
    setSaving(false);
  });

  return {
    allowManualMovements,
    canRecordManualMovements,
    saving,
    saveError,
    setSaveError,
    movementForm,
    movementTypes,
    reasons,
    reasonsLoading,
    selectedMovementType,
    movementModalOpen,
    openMovementModal,
    closeMovementModal,
    handleRecordMovement,
  };
}
