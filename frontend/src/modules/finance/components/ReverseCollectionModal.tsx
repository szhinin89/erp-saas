import { useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZhTextarea } from "../../../components/zh/inputs";
import { ZHDataTable } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { formatDate, formatDateTime } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { paymentService, type PaymentDto } from "../api/paymentService";
import type { SalesReceivableDto } from "../api/receivableService";

const schema = z.object({ reason: z.string().trim().min(1, "El motivo es obligatorio.").max(500) });
type Values = z.infer<typeof schema>;

export function ReverseCollectionModal({ receivable, onClose, onReversed }: {
  receivable: SalesReceivableDto;
  onClose: () => void;
  onReversed: () => void;
}) {
  const [rows, setRows] = useState<PaymentDto[]>([]);
  const [selected, setSelected] = useState<PaymentDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const submitting = useRef(false);
  const { register, handleSubmit, setError: setFieldError, formState: { errors } } = useForm<Values>({
    resolver: zodResolver(schema), defaultValues: { reason: "" },
  });

  useEffect(() => {
    let active = true;
    paymentService.collectionsByReceivable(receivable.id).then(data => {
      if (active) setRows(data);
    }).catch(err => {
      if (active) setError(formatApiRequestError(err, { generic: "No se pudieron consultar los cobros." }));
    }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [receivable.id]);

  const submit = handleSubmit(async values => {
    if (!selected || submitting.current) return;
    submitting.current = true;
    setSaving(true);
    setError("");
    try {
      await paymentService.reverseCollection(selected.id, values.reason);
      message.success("Cobro anulado. Puede registrar el cobro correcto.");
      onReversed();
      onClose();
    } catch (err) {
      if (!applyServerErrors(err, setFieldError, setError))
        setError(formatApiRequestError(err, { generic: "No se pudo anular el cobro." }));
    } finally {
      submitting.current = false;
      setSaving(false);
    }
  });

  return <ZHModal open title="Anular cobro" size="lg" onClose={() => { if (!saving) onClose(); }}
    subtitle={`Factura ${receivable.invoiceNumber} — ${receivable.customerName}`}>
    <p>Corrección de un cobro registrado por error. No devuelve dinero ni genera salidas de caja o transferencias.</p>
    {error && <p role="alert">{error}</p>}
    {!selected ? <ZHDataTable rows={rows} rowKey={r => r.id} loading={loading} emptyMessage="Sin cobros registrados."
      columns={[
        { key: "date", header: "Fecha", render: r => formatDate(r.paymentDate) },
        { key: "reference", header: "Referencia", render: r => r.reference || "Sin referencia" },
        { key: "amount", header: "Importe total", render: r => <ZHMoneyValue precision="money" value={r.amount} /> },
        { key: "status", header: "Estado", render: r => r.status === "Reversed" ? "Anulado" : r.status },
        { key: "reason", header: "Motivo de anulación", render: r => r.reverseReason || "—" },
        { key: "reversedAt", header: "Anulado el", render: r => r.reversedAtUtc ? formatDateTime(r.reversedAtUtc) : "—" },
        { key: "actions", header: "Acciones", render: r => r.status === "Applied"
          ? <ZHBtn onClick={() => setSelected(r)}>Anular cobro</ZHBtn> : null },
      ]} /> : <>
      <p>Confirme la anulación del cobro de <ZHMoneyValue precision="money" value={selected.amount} /> del {formatDate(selected.paymentDate)}.
        Se restaurarán todas sus aplicaciones a CxC, incluso si incluye otras facturas.</p>
      <ZHField label="Motivo" required error={errors.reason?.message}>
        <ZhTextarea {...register("reason")} disabled={saving} maxLength={500} aria-label="Motivo" />
      </ZHField>
      <ZHBtn onClick={() => { if (!saving) setSelected(null); }} disabled={saving}>Volver</ZHBtn>
      <ZHBtn onClick={submit} disabled={saving}>Confirmar anulación</ZHBtn>
    </>}
  </ZHModal>;
}
