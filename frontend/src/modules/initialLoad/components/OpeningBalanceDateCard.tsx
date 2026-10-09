import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { initialLoadService } from "../api/initialLoadService";
import {
  openingBalanceDateSchema,
  type OpeningBalanceDateFormValues,
} from "../schemas/openingBalanceDateSchema";
import type { OpeningBalanceDateDto } from "../types/importBatch.types";

/**
 * IL-5A — Configuración → Implementación: fecha de apertura de saldos de la empresa
 * (`Company.OpeningBalanceDate`), el único corte que usan todas las cargas iniciales de saldos.
 * El backend decide si se puede definir/corregir (sin operaciones reales y coherente con las
 * aperturas ya confirmadas); esta tarjeta solo muestra ese estado y envía la fecha.
 */
export function OpeningBalanceDateCard() {
  const { canShow } = usePermissionsUi();
  const canEdit = canShow("initialload.batches.confirm");
  const [status, setStatus] = useState<OpeningBalanceDateDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<OpeningBalanceDateFormValues>({
    resolver: zodResolver(openingBalanceDateSchema),
    defaultValues: { openingBalanceDate: "" },
  });

  useEffect(() => {
    let cancelled = false;
    initialLoadService
      .getOpeningBalanceDate()
      .then((dto) => {
        if (cancelled) return;
        setStatus(dto);
        reset({ openingBalanceDate: dto.openingBalanceDate ?? "" });
      })
      .catch((err: unknown) => {
        if (!cancelled)
          setLoadError(
            formatApiRequestError(err, {
              generic: "No se pudo cargar la fecha de apertura de saldos.",
            }),
          );
      });
    return () => {
      cancelled = true;
    };
  }, [reset]);

  const onSubmit = async (values: OpeningBalanceDateFormValues) => {
    try {
      const dto = await initialLoadService.setOpeningBalanceDate(values.openingBalanceDate);
      setStatus(dto);
      reset({ openingBalanceDate: dto.openingBalanceDate ?? "" });
      message.success("Fecha de apertura de saldos guardada.");
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setError, (msg) => message.error(msg));
      if (!applied) {
        message.error(
          formatApiRequestError(err, {
            generic: "No se pudo guardar la fecha de apertura de saldos.",
          }),
        );
      }
    }
  };

  const confirmedDates = status?.confirmedOpeningDates ?? [];
  const isLocked = status?.isLocked ?? false;

  return (
    <ZHCard title="Fecha de apertura de saldos">
      <p className="zh-form-help">
        Fecha de corte única de la empresa para todas las cargas iniciales de saldos (inventario,
        cuentas por cobrar, cuentas por pagar y apertura contable). Se puede corregir mientras la
        empresa no registre operaciones reales.
      </p>
      {loadError && (
        <ZHPageNotice variant="error" message="No se pudo cargar la fecha." detail={loadError} />
      )}
      {status && (
        <>
          <p>
            Fecha vigente:{" "}
            <strong>
              {status.openingBalanceDate ? formatDate(status.openingBalanceDate) : "Sin definir"}
            </strong>
          </p>
          {isLocked && status.lockReason && (
            <ZHPageNotice variant="warning" message={status.lockReason} />
          )}
          {!isLocked && confirmedDates.length > 0 && (
            <ZHPageNotice
              variant="info"
              message={`Ya existen cargas iniciales confirmadas al ${confirmedDates
                .map(formatDate)
                .join(", ")}: solo se admite esa fecha.`}
            />
          )}
          {!canEdit && (
            <ZHPageNotice
              variant="info"
              message="No tiene permiso para modificar la fecha de apertura de saldos."
            />
          )}
          <form onSubmit={(e) => void handleSubmit(onSubmit)(e)}>
            <ZHField
              label="Fecha de apertura de saldos"
              required
              fieldError={errors.openingBalanceDate?.message}
            >
              <ZhDateInput
                className="zh-input"
                disabled={!canEdit || isLocked}
                {...register("openingBalanceDate")}
              />
            </ZHField>
            <div className="zh-form-actions-row">
              <ZHBtn type="submit" disabled={!canEdit || isLocked || !isDirty || isSubmitting}>
                Guardar fecha
              </ZHBtn>
            </div>
          </form>
        </>
      )}
    </ZHCard>
  );
}
