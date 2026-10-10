import { useEffect, useState } from "react";
import { useFieldArray, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHDrawer } from "../../../components/zh/ZHDrawer";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHNumberValue } from "../../../components/zh/ZHNumberValue";
import { ZhDecimalInput, ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import { message } from "../../../lib/messages";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import {
  accountLookupFacade,
  type AccountDto,
} from "../../accounting/facades/accountLookupFacade";
import { initialLoadService } from "../api/initialLoadService";
import {
  openingJournalSchema,
  type OpeningJournalFormOutput,
  type OpeningJournalFormValues,
} from "../schemas/openingJournalSchema";
import type { OpeningBridgeAccountDto } from "../types/importBatch.types";

interface Props {
  open: boolean;
  cutoffDate: string | null;
  bridge: OpeningBridgeAccountDto | null;
  onClose: () => void;
  /** Se llama tras cada intento (éxito o fallo): el backend persiste Failed y la conciliación cambia. */
  onSettled: () => void;
}

const EMPTY_LINE = { accountId: "", debit: 0, credit: 0, description: "" };

const toAmount = (value: unknown): number => {
  const n = Number(value);
  return Number.isFinite(n) ? n : 0;
};

/**
 * IL-8D — publicar el ASI de apertura (IL-8A): editor libre Debe/Haber en un ZHDrawer amplio.
 *
 * Auditoría de reutilización (frontend/CLAUDE.md): se revisaron `PurchaseCreditNoteDiscountLinesEditor`
 * (useFieldArray + ZhDecimalInput en tabla editable, patrón reutilizado aquí) y
 * `ExpenseCategoryAccountSelector` (ZhSelect de cuentas vía `accountLookupFacade`, mismo patrón).
 * No existe un editor de asiento manual en Contabilidad (Libro Diario es solo lectura); la tabla HTML
 * es la excepción documentada "tabla editable por celda". Los totales son solo informativos: cuadre,
 * cuenta puente en 0, cuentas de control y período los valida el backend (SSOT).
 */
export function OpeningJournalPublishDrawer({ open, cutoffDate, bridge, onClose, onSettled }: Readonly<Props>) {
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [accountsError, setAccountsError] = useState<string | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    control,
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<OpeningJournalFormValues, unknown, OpeningJournalFormOutput>({
    resolver: zodResolver(openingJournalSchema),
    defaultValues: { lines: [EMPTY_LINE, EMPTY_LINE] },
  });
  const { fields, append, remove } = useFieldArray({ control, name: "lines" });
  const lines = useWatch({ control, name: "lines" }) ?? [];

  useEffect(() => {
    if (!open) return;
    reset({ lines: [EMPTY_LINE, EMPTY_LINE] });
    setServerError(null);
    let cancelled = false;
    accountLookupFacade
      .listAccounts()
      .then((all) => {
        if (!cancelled) setAccounts(all.filter((a) => a.isActive && a.allowsPosting));
      })
      .catch((err: unknown) => {
        if (!cancelled)
          setAccountsError(
            formatApiRequestError(err, { generic: "No se pudo cargar el plan de cuentas." }),
          );
      });
    return () => {
      cancelled = true;
    };
  }, [open, reset]);

  const totalDebit = lines.reduce((sum, l) => sum + toAmount(l?.debit), 0);
  const totalCredit = lines.reduce((sum, l) => sum + toAmount(l?.credit), 0);
  const difference = totalDebit - totalCredit;

  const pending = bridge?.pendingReclassification ?? 0;
  const bridgeUsable = bridge !== null && pending !== 0 && accounts.some((a) => a.id === bridge.accountId);

  /** Ayuda UI: la cuenta puente tiene saldo acreedor → se debita por el pendiente (o al revés). */
  const addBridgeLine = () => {
    if (!bridge) return;
    append({
      accountId: bridge.accountId,
      debit: Math.max(pending, 0),
      credit: Math.max(-pending, 0),
      description: "Reclasificación de Saldos de apertura",
    });
  };

  const onSubmit = async (values: OpeningJournalFormOutput) => {
    setServerError(null);
    try {
      await initialLoadService.publishOpeningJournal(
        values.lines.map((l) => ({
          accountId: l.accountId,
          debit: l.debit,
          credit: l.credit,
          description: l.description.trim() || null,
        })),
      );
      message.success("Asiento de apertura publicado.");
      onSettled();
      onClose();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setError);
      if (!applied)
        setServerError(
          formatApiRequestError(err, { generic: "No se pudo publicar el asiento de apertura." }),
        );
      onSettled();
    }
  };

  const lineError = (index: number) => {
    const e = errors.lines?.[index];
    return e?.accountId?.message ?? e?.debit?.message ?? e?.credit?.message ?? e?.description?.message;
  };

  return (
    <ZHDrawer
      open={open}
      onClose={onClose}
      size="lg"
      title="Publicar asiento de apertura"
      subtitle={`Fecha de apertura: ${cutoffDate ? formatDate(cutoffDate) : "sin definir"}`}
      footer={
        <>
          <ZHBtn type="button" variant="ghost" onClick={onClose} disabled={isSubmitting}>
            Cancelar
          </ZHBtn>
          <ZHBtn
            type="button"
            variant="primary"
            disabled={isSubmitting}
            onClick={() => void handleSubmit(onSubmit)()}
          >
            Publicar
          </ZHBtn>
        </>
      }
    >
      <p className="zh-form-help">
        Reclasifica la cuenta puente de Saldos de apertura a sus cuentas definitivas (por ejemplo,
        patrimonio). El sistema valida el cuadre, que la cuenta puente quede en cero y que no se usen
        cuentas de control de Inventario, CxC o CxP.
      </p>
      {bridge && (
        <p>
          Cuenta puente{" "}
          <strong>
            {bridge.accountCode} {bridge.accountName}
          </strong>
          : pendiente de reclasificación{" "}
          <strong>
            <ZHNumberValue value={pending} precision="accounting" />
          </strong>
        </p>
      )}
      {accountsError && (
        <ZHPageNotice variant="error" message="No se pudo cargar el plan de cuentas." detail={accountsError} />
      )}
      {serverError && (
        <ZHPageNotice variant="error" message="El asiento de apertura no se publicó." detail={serverError} />
      )}
      {errors.lines?.root?.message && <ZHPageNotice variant="error" message={errors.lines.root.message} />}
      {errors.lines?.message && <ZHPageNotice variant="error" message={errors.lines.message} />}
      <div className="table-scroll">
        <table className="table table--compact">
          <thead>
            <tr>
              <th>Cuenta</th>
              <th>Descripción</th>
              <th className="zh-text-align-right">Debe</th>
              <th className="zh-text-align-right">Haber</th>
              <th aria-label="Eliminar" />
            </tr>
          </thead>
          <tbody>
            {fields.map((field, index) => (
              <tr key={field.id}>
                <td>
                  <ZHField density="compact" fieldError={lineError(index)}>
                    <ZhSelect
                      aria-label={`Cuenta línea ${index + 1}`}
                      disabled={isSubmitting}
                      {...register(`lines.${index}.accountId` as const)}
                    >
                      <option value="">Seleccione una cuenta</option>
                      {accounts.map((a) => (
                        <option key={a.id} value={a.id}>
                          {a.code} - {a.name}
                        </option>
                      ))}
                    </ZhSelect>
                  </ZHField>
                </td>
                <td>
                  <ZhTextInput
                    aria-label={`Descripción línea ${index + 1}`}
                    maxLength={500}
                    disabled={isSubmitting}
                    {...register(`lines.${index}.description` as const)}
                  />
                </td>
                <td className="zh-text-align-right">
                  <ZhDecimalInput
                    aria-label={`Debe línea ${index + 1}`}
                    precision="money"
                    positiveOnly
                    density="compact"
                    disabled={isSubmitting}
                    {...register(`lines.${index}.debit` as const)}
                  />
                </td>
                <td className="zh-text-align-right">
                  <ZhDecimalInput
                    aria-label={`Haber línea ${index + 1}`}
                    precision="money"
                    positiveOnly
                    density="compact"
                    disabled={isSubmitting}
                    {...register(`lines.${index}.credit` as const)}
                  />
                </td>
                <td>
                  <ZHIconButton
                    icon="delete"
                    variant="danger"
                    title="Eliminar línea"
                    onClick={() => remove(index)}
                    disabled={isSubmitting || fields.length <= 1}
                  />
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={2}>
                <strong>Totales</strong>
              </td>
              <td className="zh-table-cell--num" aria-label="Total Debe">
                <ZHNumberValue value={totalDebit} precision="accounting" />
              </td>
              <td className="zh-table-cell--num" aria-label="Total Haber">
                <ZHNumberValue value={totalCredit} precision="accounting" />
              </td>
              <td />
            </tr>
            <tr>
              <td colSpan={2}>Diferencia (Debe − Haber)</td>
              <td colSpan={2} className="zh-table-cell--num" aria-label="Diferencia">
                <ZHNumberValue value={difference} precision="accounting" />
              </td>
              <td />
            </tr>
          </tfoot>
        </table>
      </div>
      <ZHBtn type="button" variant="ghost" onClick={() => append(EMPTY_LINE)} disabled={isSubmitting}>
        + Agregar línea
      </ZHBtn>{" "}
      <ZHBtn
        type="button"
        variant="secondary"
        onClick={addBridgeLine}
        disabled={isSubmitting || !bridgeUsable}
      >
        Agregar cuenta puente
      </ZHBtn>
    </ZHDrawer>
  );
}
