import { useState, useEffect, useCallback, useMemo } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { cajaService } from "../api/cajaService";
import type {
  CashSessionDto,
  CashSessionListItemDto,
  CashRegisterDto,
  CashSessionCollectionSummaryDto,
} from "../api/cajaService";
import { useManualCashMovementFlow } from "./useManualCashMovementFlow";
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
  const branchName = useActiveBranchStore((s) => s.branch)?.name ?? null;
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
    closeForm,
    handleClose,
    startClose,
    countedTotal,
    loadDetail,
    fetchList,
  };
}
