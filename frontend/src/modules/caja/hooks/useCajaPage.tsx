import { useState, useEffect, useCallback, useMemo } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { cajaService } from "../api/cajaService";
import type {
  CashSessionDto,
  CashSessionListItemDto,
  CashRegisterDto,
} from "../api/cajaService";
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

  // ── Reference data ─────────────────────────────────────────────────
  const [cashRegisters, setCashRegisters] = useState<CashRegisterDto[]>([]);
  const branchName = useActiveBranchStore((s) => s.branch)?.name ?? null;
  const currentUserName = useAuthStore((s) => s.user?.fullName) ?? null;

  // ── Forms ──────────────────────────────────────────────────────────
  const openForm = useForm<OpenCashSessionFormValues>({
    resolver: zodResolver(openCashSessionSchema),
    defaultValues: emptyOpenForm(),
    mode: "onBlur",
  });

  const movementForm = useForm<RecordMovementFormValues>({
    resolver: zodResolver(recordMovementSchema),
    defaultValues: emptyMovementForm(),
    mode: "onBlur",
  });

  const closeForm = useForm<CloseCashSessionFormValues>({
    resolver: zodResolver(closeCashSessionSchema),
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
      setSaveError("No se pudo cargar la sesión.");
    }
  }, []);

  // ── Movement type labels ───────────────────────────────────────────
  const movementTypes = [
    { value: "ManualIncome", label: "Ingreso manual" },
    { value: "ManualExpense", label: "Egreso manual" },
    { value: "Withdrawal", label: "Retiro" },
  ];

  // ── Open session ───────────────────────────────────────────────────
  // CRITICAL-CONFIRMATIONS-CASH-02: abrir un turno es una acción con impacto de dinero
  // operativo — se confirma antes de ejecutar (resumen de caja/sucursal, usuario y monto
  // inicial), nunca actualiza estado local antes de que el backend confirme éxito, y muestra
  // éxito/error reales al terminar.
  const handleOpen = openForm.handleSubmit(async (data) => {
    if (saving) return;

    const register = cashRegisters.find((r) => r.id === data.cashRegisterId);
    const confirmed = await message.confirm({
      title: "Abrir turno de caja",
      message: (
        <>
          <p className="zh-confirm-message">
            Se iniciará un turno operativo de caja. Mientras esté abierto, todas las ventas y
            movimientos registrados en esta caja quedarán asociados a este turno hasta que se
            cierre.
          </p>
          <p className="zh-confirm-message">
            {register ? (
              <>
                Caja: <strong>{register.code} — {register.name}</strong> ({register.branchName}
                ).
                <br />
              </>
            ) : null}
            {currentUserName ? (
              <>
                Usuario: <strong>{currentUserName}</strong>.
                <br />
              </>
            ) : null}
            Monto inicial: <strong>{formatMoneyWithSymbol(data.openingAmount)}</strong>.
          </p>
        </>
      ),
      variant: "warning",
      confirmLabel: "Abrir caja",
      cancelLabel: "Cancelar",
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
      openForm.reset(emptyOpenForm());
      setTab("detalle");
      fetchList();
      message.success("Caja abierta correctamente.");
    } catch (err: unknown) {
      const applied = applyServerErrors(err, openForm.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied)
        setSaveError(
          formatApiRequestError(err, { generic: "No se pudo abrir la caja." }),
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
    const isIncome = data.movementType === "ManualIncome";

    const confirmed = await message.confirm({
      title: isIncome ? "Registrar ingreso de caja" : "Registrar egreso de caja",
      message: (
        <p className="zh-confirm-message">
          Tipo: <strong>{typeLabel}</strong>
          <br />
          Concepto: <strong>{data.description}</strong>
          <br />
          Monto: <strong>{formatMoneyWithSymbol(data.amount)}</strong>
        </p>
      ),
      variant: isIncome ? "warning" : "danger",
      confirmLabel: isIncome ? "Registrar ingreso" : "Registrar egreso",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;

    setSaveError("");
    setSaving(true);
    try {
      await cajaService.recordMovement(viewing.id, {
        movementType: data.movementType,
        amount: data.amount,
        description: data.description,
      });
      movementForm.reset(emptyMovementForm());
      await loadDetail(viewing.id);
      fetchMySession();
      message.success("Movimiento registrado correctamente.");
    } catch (err: unknown) {
      // A diferencia de abrir/cerrar caja, aquí el ticket exige explícitamente un toast
      // message.error con el mensaje real del backend — se muestra siempre (además de resaltar
      // el campo específico vía applyServerErrors cuando el 422 viene mapeado por campo).
      applyServerErrors(err, movementForm.setError, () => {});
      const errorMessage = formatApiRequestError(err, {
        generic: "No se pudo registrar el movimiento.",
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
      title: "Cerrar turno de caja",
      message: (
        <>
          <p className="zh-confirm-message">
            Vas a cerrar este turno de caja. Al confirmar, el turno finaliza y no se podrán
            registrar más movimientos en esta sesión.
          </p>
          <p className="zh-confirm-message">
            Esperado: <strong>{formatMoneyWithSymbol(expected)}</strong>
            <br />
            Contado: <strong>{formatMoneyWithSymbol(counted)}</strong>
            <br />
            Diferencia: <strong>{formatMoneyWithSymbol(difference)}</strong>
          </p>
          {hasMismatch ? (
            <p className="zh-confirm-message">
              <strong>Hay una diferencia entre el saldo esperado y lo contado.</strong> Revisa
              el arqueo antes de continuar si no es intencional.
            </p>
          ) : null}
        </>
      ),
      variant: hasMismatch ? "danger" : "warning",
      confirmLabel: "Confirmar cierre",
      cancelLabel: "Cancelar",
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
      message.success("Caja cerrada correctamente.");
    } catch (err: unknown) {
      const applied = applyServerErrors(err, closeForm.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied)
        setSaveError(
          formatApiRequestError(err, { generic: "No se pudo cerrar la caja." }),
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
    cashRegisters,
    branchName,
    selectedRegister,
    openForm,
    handleOpen,
    movementForm,
    handleRecordMovement,
    movementTypes,
    closeForm,
    handleClose,
    startClose,
    countedTotal,
    loadDetail,
    fetchList,
  };
}
