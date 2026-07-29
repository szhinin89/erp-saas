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
import { applyServerErrors } from "../../lib/validationErrors";

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

  // ── Open session ───────────────────────────────────────────────────
  const handleOpen = openForm.handleSubmit(async (data) => {
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
    } catch (err: unknown) {
      const applied = applyServerErrors(err, openForm.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied) setSaveError("Error al abrir la caja.");
    }
    setSaving(false);
  });

  // ── Record movement ────────────────────────────────────────────────
  const handleRecordMovement = movementForm.handleSubmit(async (data) => {
    if (!viewing) return;
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
    } catch (err: unknown) {
      const applied = applyServerErrors(err, movementForm.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied) setSaveError("Error al registrar el movimiento.");
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

  const handleClose = closeForm.handleSubmit(async (data) => {
    if (!viewing) return;
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
    } catch (err: unknown) {
      const applied = applyServerErrors(err, closeForm.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied) setSaveError("Error al cerrar la caja.");
    }
    setSaving(false);
  });

  // ── Movement type labels ───────────────────────────────────────────
  const movementTypes = [
    { value: "ManualIncome", label: "Ingreso manual" },
    { value: "ManualExpense", label: "Egreso manual" },
    { value: "Withdrawal", label: "Retiro" },
  ];

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
