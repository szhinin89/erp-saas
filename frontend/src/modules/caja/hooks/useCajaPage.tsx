import { useState, useEffect, useCallback } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { cajaService } from "../api/cajaService";
import type {
  CashSessionDto,
  CashSessionListItemDto,
  CashRegisterDto,
  CashSessionCollectionSummaryDto,
  CashRegisterOpeningReadinessDto,
} from "../api/cajaService";
import { useManualCashMovementFlow } from "./useManualCashMovementFlow";
import { useCashBankDepositFlow } from "./useCashBankDepositFlow";
import {
  openCashSessionSchema,
  emptyOpenForm,
  closeCashSessionSchema,
  defaultClosingCounts,
  type OpenCashSessionFormValues,
  type CloseCashSessionFormValues,
} from "../schemas/cajaSchema";
import { useActiveBranchStore } from "../../../store/activeBranchStore";
import { useAuthStore } from "../../../store/authStore";
import { useI18n } from "../../../i18n/i18n";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import { formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";

type Tab = "listado" | "abrir" | "detalle" | "cerrar";

export function useCajaPage() {
  const moneyDecimals = usePrecisionDecimals("money"); // presentación (04F)
  const { t } = useI18n();

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
  const [registersLoading, setRegistersLoading] = useState(true);
  const [registersError, setRegistersError] = useState("");
  const [mySessionLoading, setMySessionLoading] = useState(true);
  const [mySessionError, setMySessionError] = useState("");
  const activeBranch = useActiveBranchStore((s) => s.branch);
  const branchName = activeBranch?.name ?? null;
  const branchId = activeBranch?.id ?? null;
  const currentUserName = useAuthStore((s) => s.user?.fullName) ?? null;

  // ── Forms ──────────────────────────────────────────────────────────
  const openForm = useForm<OpenCashSessionFormValues>({
    resolver: zodResolver(openCashSessionSchema(t)),
    defaultValues: emptyOpenForm(),
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

  // ── READINESS-2B-3 — habilitación preventiva de la caja seleccionada ──
  // El backend evalúa la caja con su propio punto de emisión (misma regla que la apertura). Solo se
  // usa la respuesta de la caja y sucursal actuales; la apertura vuelve a decidir en el backend.
  const [openingReadinessState, setOpeningReadinessState] = useState<{
    key: string;
    readiness: CashRegisterOpeningReadinessDto | null;
    error: boolean;
  } | null>(null);
  const readinessKey = selectedRegisterId && branchId ? `${branchId}:${selectedRegisterId}` : null;

  const fetchOpeningReadiness = useCallback(async (registerId: string, key: string) => {
    try {
      const readiness = await cajaService.getOpeningReadiness(registerId);
      setOpeningReadinessState({ key, readiness, error: false });
    } catch {
      setOpeningReadinessState({ key, readiness: null, error: true });
    }
  }, []);

  useEffect(() => {
    if (readinessKey) fetchOpeningReadiness(selectedRegisterId, readinessKey);
  }, [readinessKey, selectedRegisterId, fetchOpeningReadiness]);

  const currentReadiness =
    openingReadinessState?.key === readinessKey ? openingReadinessState : null;
  const openingReadiness =
    currentReadiness?.readiness &&
    currentReadiness.readiness.cashRegisterId === selectedRegisterId &&
    currentReadiness.readiness.branchId === branchId
      ? currentReadiness.readiness
      : null;
  const openingReadinessLoading = !!readinessKey && !currentReadiness;
  const openingReadinessError = !!currentReadiness?.error;

  // ── Init ───────────────────────────────────────────────────────────
  useEffect(() => {
    fetchMySession();
    fetchList();
  }, []);

  const fetchRegisters = useCallback(async () => {
    setRegistersLoading(true);
    setRegistersError("");
    try {
      const registers = await cajaService.getCashRegisters(true);
      setCashRegisters(registers);
      if (!registers.some((r) => r.id === openForm.getValues("cashRegisterId")))
        openForm.setValue("cashRegisterId", registers[0]?.id ?? "");
    } catch (err: unknown) {
      setRegistersError(formatApiRequestError(err, { generic: t("caja.openContext.registersError") }));
    } finally {
      setRegistersLoading(false);
    }
  }, [openForm, t]);

  // Las cajas son de la sucursal activa: se vuelven a cargar al cambiar de sucursal.
  useEffect(() => {
    fetchRegisters();
  }, [branchId, fetchRegisters]);

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
    setMySessionLoading(true);
    setMySessionError("");
    try {
      const s = await cajaService.getMy();
      setMySession(s);
    } catch (err: unknown) {
      setMySessionError(formatApiRequestError(err, { generic: t("caja.openContext.sessionError") }));
    } finally {
      setMySessionLoading(false);
    }
  }, [t]);

  // Un fallo al consultar el diagnóstico no bloquea: la apertura lo decide el backend.
  const canOpen = !saving && !registersLoading && !registersError &&
    !mySessionLoading && !mySessionError && !mySession &&
    !!selectedRegister && !selectedRegister.hasOpenSession &&
    !openingReadinessLoading && openingReadiness?.canOpen !== false;

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

  // ── Open session ───────────────────────────────────────────────────
  // CRITICAL-CONFIRMATIONS-CASH-02: abrir un turno es una acción con impacto de dinero
  // operativo — se confirma antes de ejecutar (resumen de caja/sucursal, usuario y monto
  // inicial), nunca actualiza estado local antes de que el backend confirme éxito, y muestra
  // éxito/error reales al terminar.
  const handleOpen = openForm.handleSubmit(async (data) => {
    if (!canOpen) return;

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
            {t("caja.session.initialAmount")}: <strong>{formatMoneyWithSymbol(data.openingAmount, moneyDecimals)}</strong>.
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
      // La configuración pudo cambiar desde la consulta previa: se vuelve a diagnosticar la caja.
      if (readinessKey) fetchOpeningReadiness(data.cashRegisterId, readinessKey);
    }
    setSaving(false);
  });

  // ── Record movement (SALES-MANUAL-CASH-MOVEMENT-INTEGRATION-08) ──────────────────────
  // Extraído a useManualCashMovementFlow para que Ventas lo reutilice sin duplicar el flujo.
  // Mismo comportamiento exacto que antes: turno = el que se está viendo en el detalle, y al
  // registrar exitosamente se refresca ese detalle + la sesión propia (mySession) — nada cambia
  // para /treasury/cash, solo cambió DÓNDE vive el código.
  const manualMovement = useManualCashMovementFlow({
    cashSessionId: viewing?.id ?? null,
    isSessionOpen: viewing?.status === "Open",
    onRecorded: async () => {
      if (viewing) await loadDetail(viewing.id);
      fetchMySession();
    },
  });

  // ── ACCOUNTING-12C — depósito bancario del efectivo de la caja (operación propia). ──
  const bankDeposit = useCashBankDepositFlow({
    cashSessionId: viewing?.id ?? null,
    isSessionOpen: viewing?.status === "Open",
    onRecorded: async () => {
      if (viewing) await loadDetail(viewing.id);
      fetchMySession();
    },
  });

  // ── Close session ──────────────────────────────────────────────────
  const startClose = useCallback(() => {
    closeForm.reset({ closingCounts: defaultClosingCounts(), closeNotes: "" });
    setTab("cerrar");
  }, []);

  // Sin useMemo: al escribir en un input registrado RHF muta el array en su lugar y watch()
  // devuelve la misma referencia, así que un memo sobre ella dejaba el total en $0.00 (R3).
  // watch() re-renderiza en cada cambio y son 11 denominaciones: se suma en cada render.
  const countedTotal = (closeForm.watch("closingCounts") ?? []).reduce(
    (sum, c) => sum + c.denominationValue * c.quantity,
    0,
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
            {t("caja.session.expected")}: <strong>{formatMoneyWithSymbol(expected, moneyDecimals)}</strong>
            <br />
            {t("caja.session.counted")}: <strong>{formatMoneyWithSymbol(counted, moneyDecimals)}</strong>
            <br />
            {t("caja.session.difference")}: <strong>{formatMoneyWithSymbol(difference, moneyDecimals)}</strong>
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
      void fetchRegisters();
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
    registersLoading,
    registersError,
    mySessionLoading,
    mySessionError,
    fetchRegisters,
    fetchMySession,
    canOpen,
    openingReadiness,
    openingReadinessLoading,
    openingReadinessError,
    currentUserName,
    branchName,
    selectedRegister,
    allowManualMovements: manualMovement.allowManualMovements,
    canRecordManualMovements: manualMovement.canRecordManualMovements,
    openForm,
    handleOpen,
    movementForm: manualMovement.movementForm,
    handleRecordMovement: manualMovement.handleRecordMovement,
    movementTypes: manualMovement.movementTypes,
    reasons: manualMovement.reasons,
    reasonsLoading: manualMovement.reasonsLoading,
    selectedMovementType: manualMovement.selectedMovementType,
    movementModalOpen: manualMovement.movementModalOpen,
    openMovementModal: manualMovement.openMovementModal,
    closeMovementModal: manualMovement.closeMovementModal,
    // Estado propio del flujo de movimiento manual (SALES-MANUAL-CASH-MOVEMENT-INTEGRATION-08) —
    // deliberadamente separado del `saving`/`saveError` de abrir/cerrar turno (arriba): antes de
    // extraer el hook compartían una sola variable de página, pero nunca se mostraban a la vez
    // (pestañas mutuamente excluyentes: abrir/cerrar vs. detalle+modal) — separarlos aquí no
    // cambia ningún comportamiento visible, solo evita que el aviso de error de un movimiento se
    // duplicara también en el banner superior de la página.
    movementSaving: manualMovement.saving,
    movementSaveError: manualMovement.saveError,
    bankDeposit,
    closeForm,
    handleClose,
    startClose,
    countedTotal,
    loadDetail,
    fetchList,
  };
}
