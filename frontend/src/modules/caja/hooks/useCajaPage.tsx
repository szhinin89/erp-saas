import { useState, useEffect, useCallback, useMemo } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { cajaService } from "../api/cajaService";
import type {
  CashSessionDto,
  CashSessionListItemDto,
  CashRegisterDto,
  CashSessionCollectionSummaryDto,
  CashMovementReasonDto,
} from "../api/cajaService";
import { manualCashMovementTypeOptions } from "../constants/cashMovementTypes";
import { operationalPreferencesService } from "../../configuracion/operaciones/api/operationalPreferencesService";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import {
  openCashSessionSchema,
  emptyOpenForm,
  recordMovementSchema,
  emptyMovementForm,
  closeCashSessionSchema,
  defaultClosingCounts,
  type OpenCashSessionFormValues,
  type RecordMovementFormValues,
  type CloseCashSessionFormValues,
} from "../schemas/cajaSchema";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { useAuthStore } from "../../../store/authStore";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import { formatMoneyWithSymbol } from "../../../lib/sanitizers";

type Tab = "listado" | "abrir" | "detalle" | "cerrar";

export function useCajaPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  // TREASURY-CASH-MANUAL-MOVEMENTS-PERMISSION-06 — `caja.record` ya existía y ya está enforced
  // fail-closed en el backend (CashSessionController.RecordMovement, [Authorize(Policy=
  // "perm:caja.record")]) desde antes de este ticket; solo faltaba que el frontend lo conociera
  // para no mostrar una acción que el backend igual rechazaría con 403.
  const canRecordManualMovements = canShow("caja.record");

  // ── Page state ─────────────────────────────────────────────────────
  const [tab, setTab] = useState<Tab>("listado");
  const [listItems, setListItems] = useState<CashSessionListItemDto[]>([]);
  const [listLoading, setListLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState("");
  const [saveError, setSaveError] = useState("");
  const [saving, setSaving] = useState(false);

  // ── Current session ────────────────────────────────────────────────
  const [mySession, setMySession] = useState<CashSessionDto | null>(null);
  const [viewing, setViewing] = useState<CashSessionDto | null>(null);

  // ── Collection summary (CASH-SESSION-COLLECTION-SUMMARY-01) — informativo,
  // separado del efectivo físico de `viewing` (totalIncome/currentBalance no cambian). ──
  const [collectionSummary, setCollectionSummary] =
    useState<CashSessionCollectionSummaryDto | null>(null);
  const [collectionSummaryLoading, setCollectionSummaryLoading] = useState(false);

  // ── Reference data ─────────────────────────────────────────────────
  const [cashRegisters, setCashRegisters] = useState<CashRegisterDto[]>([]);
  const branchName = useActiveBranchStore((s) => s.branch)?.name ?? null;
  const currentUserName = useAuthStore((s) => s.user?.fullName) ?? null;
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);

  // ── TREASURY-CASH-COMPANY-SETTING-05A — reutiliza el SSOT existente de preferencias
  // operativas (settings.operations, cash.allow_manual_in_out_movements) en vez de una
  // configuración paralela. Fail-closed en el frontend: el botón/modal solo se muestran cuando la
  // preferencia terminó de cargar Y su valor es explícitamente true — mientras carga, si falla, o
  // si el valor es false, se ocultan (nunca se asume true). El backend (RecordCashMovementHandler)
  // sigue siendo la autoridad real y ya rechaza fail-closed independientemente de esto — este
  // cambio es puramente de UI, para no mostrar un botón que el backend igual rechazaría ni, peor,
  // sugerir que la función está disponible cuando no se pudo confirmar. Se recarga al cambiar de
  // empresa activa (companySessionVersion), igual que el resto de pantallas que leen esta
  // configuración. ──
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

  // ── Forms ──────────────────────────────────────────────────────────
  const openForm = useForm<OpenCashSessionFormValues>({
    resolver: zodResolver(openCashSessionSchema(t)),
    defaultValues: emptyOpenForm(),
    mode: "onBlur",
  });

  const movementForm = useForm<RecordMovementFormValues>({
    resolver: zodResolver(recordMovementSchema(t)),
    defaultValues: emptyMovementForm(),
    mode: "onBlur",
  });

  const closeForm = useForm<CloseCashSessionFormValues>({
    resolver: zodResolver(closeCashSessionSchema(t)),
    defaultValues: { closingCounts: defaultClosingCounts(), closeNotes: "" },
    mode: "onBlur",
  });

  // ── Caja seleccionada en el formulario de apertura — misma fuente de datos
  // que llena el <select>, sin requests adicionales (Sucursal/Establecimiento/Punto de emisión).
  const selectedRegisterId = openForm.watch("cashRegisterId");
  const selectedRegister =
    cashRegisters.find((r) => r.id === selectedRegisterId) ?? null;

  // ── Init ───────────────────────────────────────────────────────────
  useEffect(() => {
    cajaService
      .getCashRegisters(true)
      .then((registers) => {
        setCashRegisters(registers);
        if (registers.length > 0)
          openForm.setValue("cashRegisterId", registers[0].id);
      })
      .catch(() => {});
    fetchMySession();
    fetchList();
  }, []);

  // ── Fetch list ─────────────────────────────────────────────────────
  const fetchList = useCallback(async () => {
    setListLoading(true);
    try {
      const r = await cajaService.list(statusFilter || undefined);
      setListItems(r.items);
    } catch {
      /* silent */
    }
    setListLoading(false);
  }, [statusFilter]);

  useEffect(() => {
    fetchList();
  }, [statusFilter]);

  // ── Fetch my session ───────────────────────────────────────────────
  const fetchMySession = useCallback(async () => {
    try {
      const s = await cajaService.getMy();
      setMySession(s);
    } catch {
      setMySession(null);
    }
  }, []);

  // ── Load detail ────────────────────────────────────────────────────
  const loadDetail = useCallback(async (id: string) => {
    try {
      const s = await cajaService.getById(id);
      setViewing(s);
      setTab("detalle");
    } catch {
      setSaveError(t("caja.session.loadError"));
    }
    fetchCollectionSummary(id);
  }, [t]);

  // ── Collection summary — informativo, nunca bloquea el detalle de caja si falla. ────
  const fetchCollectionSummary = useCallback(async (cashSessionId: string) => {
    setCollectionSummaryLoading(true);
    try {
      const summary = await cajaService.getCollectionSummary(cashSessionId);
      setCollectionSummary(summary);
    } catch {
      setCollectionSummary(null);
    }
    setCollectionSummaryLoading(false);
  }, []);

  // ── Movement types — única fuente compartida (frontend/backend enum en paridad), etiquetas
  // resueltas vía i18n (TREASURY-CASH-ARCHITECTURE-I18N-AUDIT-04) ─────
  const movementTypes = useMemo(() => manualCashMovementTypeOptions(t), [t]);

  // ── Motivos (TREASURY-CASH-MANUAL-MOVEMENTS-01) — catálogo dinámico del backend, filtrado
  // por Tenant+Company (server-side) y por el Tipo ya elegido — nunca hardcodeado en frontend.
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

  // ── Modal "Registrar movimiento manual de efectivo" (TREASURY-CASH-MANUAL-MOVEMENT-MODAL-02) —
  // reemplaza el formulario inline permanente; misma movementForm/handleRecordMovement de siempre,
  // solo cambia dónde se muestra. Solo disponible con turno abierto (ver botón en CajaPage).
  const [movementModalOpen, setMovementModalOpen] = useState(false);

  const openMovementModal = useCallback(() => {
    // Defensa en profundidad (TREASURY-CASH-MANUAL-MOVEMENTS-PERMISSION-06): aunque el botón que
    // dispara esto ya está oculto si falta cualquiera de las tres condiciones (empresa lo
    // permite, usuario tiene `caja.record`, turno abierto), nunca abrir el modal si por algún
    // motivo se invoca igual — el backend rechazaría el submit de todas formas (fail-closed real,
    // 403 sin permiso o 422 por configuración de empresa).
    if (!allowManualMovements || !canRecordManualMovements || viewing?.status !== "Open") return;
    movementForm.reset(emptyMovementForm());
    setSaveError("");
    setMovementModalOpen(true);
  }, [allowManualMovements, canRecordManualMovements, viewing]);

  const closeMovementModal = useCallback(() => {
    if (saving) return;
    movementForm.reset(emptyMovementForm());
    setSaveError("");
    setMovementModalOpen(false);
  }, [saving]);

  // ── Open session ───────────────────────────────────────────────────
  // CRITICAL-CONFIRMATIONS-CASH-02: abrir un turno es una acción con impacto de dinero
  // operativo — se confirma antes de ejecutar (resumen de caja/sucursal, usuario y monto
  // inicial), nunca actualiza estado local antes de que el backend confirme éxito, y muestra
  // éxito/error reales al terminar.
  const handleOpen = openForm.handleSubmit(async (data) => {
    if (saving) return;

    const register = cashRegisters.find((r) => r.id === data.cashRegisterId);
    const confirmed = await message.confirm({
      title: t("caja.session.openTitle"),
      message: (
        <>
          <p className="zh-confirm-message">
            {t("caja.session.openExplanation")}
          </p>
          <p className="zh-confirm-message">
            {register ? (
              <>
                {t("caja.session.register")}: <strong>{register.code} — {register.name}</strong> ({register.branchName}
                ).
                <br />
              </>
            ) : null}
            {currentUserName ? (
              <>
                {t("caja.movements.table.user")}: <strong>{currentUserName}</strong>.
                <br />
              </>
            ) : null}
            {t("caja.session.initialAmount")}: <strong>{formatMoneyWithSymbol(data.openingAmount)}</strong>.
          </p>
        </>
      ),
      variant: "warning",
      confirmLabel: t("caja.session.confirmOpen"),
      cancelLabel: t("common.cancel"),
    });
    if (!confirmed) return;

    setSaveError("");
    setSaving(true);
    try {
      const session = await cajaService.open({
        cashRegisterId: data.cashRegisterId,
        openingAmount: data.openingAmount,
        notes: data.notes || undefined,
      });
      setMySession(session);
      setViewing(session);
      setCollectionSummary(null);
      openForm.reset(emptyOpenForm());
      setTab("detalle");
      fetchList();
      message.success(t("caja.session.openSuccess"));
    } catch (err: unknown) {
      const applied = applyServerErrors(err, openForm.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied)
        setSaveError(
          formatApiRequestError(err, { unauthorized: t("caja.session.unauthorized"), generic: t("caja.session.openError") }),
        );
    }
    setSaving(false);
  });

  // ── Record movement ────────────────────────────────────────────────
  // CRITICAL-CONFIRMATIONS-CASH-02: un ingreso/egreso manual afecta el saldo de caja de
  // inmediato — se confirma antes de ejecutar (tipo, concepto y monto), con variant warning
  // para ingreso y danger para egreso/retiro (mayor riesgo de descuadre).
  const handleRecordMovement = movementForm.handleSubmit(async (data) => {
    if (!viewing || saving) return;

    const typeLabel =
      movementTypes.find((mt) => mt.value === data.movementType)?.label ??
      data.movementType;
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
          {t("caja.movements.table.amount")}: <strong>{formatMoneyWithSymbol(data.amount)}</strong>
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
      await cajaService.recordMovement(viewing.id, {
        movementType: data.movementType,
        reasonId: data.reasonId,
        amount: data.amount,
        description: data.description,
      });
      movementForm.reset(emptyMovementForm());
      setMovementModalOpen(false);
      await loadDetail(viewing.id);
      fetchMySession();
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

  // ── Close session ──────────────────────────────────────────────────
  const startClose = useCallback(() => {
    closeForm.reset({ closingCounts: defaultClosingCounts(), closeNotes: "" });
    setTab("cerrar");
  }, []);

  const closingCountsWatch = closeForm.watch("closingCounts");
  const countedTotal = useMemo(
    () =>
      (closingCountsWatch ?? []).reduce(
        (sum, c) => sum + c.denominationValue * c.quantity,
        0,
      ),
    [closingCountsWatch],
  );

  // CRITICAL-CONFIRMATIONS-CASH-02: cerrar caja finaliza el turno y bloquea nuevos
  // movimientos — confirmación fuerte con el mismo resumen (esperado/contado/diferencia) ya
  // visible en pantalla, advertencia reforzada (variant danger) si hay descuadre.
  const handleClose = closeForm.handleSubmit(async (data) => {
    if (!viewing || saving) return;

    const expected = viewing.currentBalance;
    const counted = countedTotal;
    const difference = counted - expected;
    const hasMismatch = difference !== 0;

    const confirmed = await message.confirm({
      title: t("caja.session.closeTitle"),
      message: (
        <>
          <p className="zh-confirm-message">
            {t("caja.session.closeExplanation")}
          </p>
          <p className="zh-confirm-message">
            {t("caja.session.expected")}: <strong>{formatMoneyWithSymbol(expected)}</strong>
            <br />
            {t("caja.session.counted")}: <strong>{formatMoneyWithSymbol(counted)}</strong>
            <br />
            {t("caja.session.difference")}: <strong>{formatMoneyWithSymbol(difference)}</strong>
          </p>
          {hasMismatch ? (
            <p className="zh-confirm-message">
              <strong>{t("caja.session.mismatch")}</strong> {t("caja.session.reviewCount")}
            </p>
          ) : null}
        </>
      ),
      variant: hasMismatch ? "danger" : "warning",
      confirmLabel: t("caja.session.confirmClosing"),
      cancelLabel: t("common.cancel"),
    });
    if (!confirmed) return;

    setSaveError("");
    setSaving(true);
    try {
      const session = await cajaService.close(viewing.id, {
        closingCounts: data.closingCounts.map((c) => ({
          denominationValue: c.denominationValue,
          denominationLabel: c.denominationLabel,
          quantity: c.quantity,
        })),
        closeNotes: data.closeNotes || undefined,
      });
      setViewing(session);
      setMySession(null);
      setTab("detalle");
      fetchList();
      message.success(t("caja.session.closeSuccess"));
    } catch (err: unknown) {
      const applied = applyServerErrors(err, closeForm.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied)
        setSaveError(
          formatApiRequestError(err, { unauthorized: t("caja.session.unauthorized"), generic: t("caja.session.closeError") }),
        );
    }
    setSaving(false);
  });

  return {
    tab,
    setTab,
    listItems,
    listLoading,
    statusFilter,
    setStatusFilter,
    saveError,
    setSaveError,
    saving,
    mySession,
    viewing,
    collectionSummary,
    collectionSummaryLoading,
    cashRegisters,
    branchName,
    selectedRegister,
    allowManualMovements,
    canRecordManualMovements,
    openForm,
    handleOpen,
    movementForm,
    handleRecordMovement,
    movementTypes,
    reasons,
    reasonsLoading,
    selectedMovementType,
    movementModalOpen,
    openMovementModal,
    closeMovementModal,
    closeForm,
    handleClose,
    startClose,
    countedTotal,
    loadDetail,
    fetchList,
  };
}
