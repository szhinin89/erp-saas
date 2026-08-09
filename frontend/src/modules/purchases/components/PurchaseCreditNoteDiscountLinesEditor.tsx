import type {
  FieldArrayWithId,
  UseFieldArrayAppend,
  UseFieldArrayRemove,
  UseFormRegister,
} from "react-hook-form";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { useI18n } from "../../../i18n/i18n";
import type { PurchaseCreditNoteDraftFormValues } from "../schemas/purchaseCreditNoteSchema";
import "../../../styles/shared/erp-form-core.css";

interface Props {
  register: UseFormRegister<PurchaseCreditNoteDraftFormValues>;
  fields: FieldArrayWithId<PurchaseCreditNoteDraftFormValues, "lines", "id">[];
  append: UseFieldArrayAppend<PurchaseCreditNoteDraftFormValues, "lines">;
  remove: UseFieldArrayRemove;
  disabled?: boolean;
}

/**
 * Auditoría de reutilización (frontend/CLAUDE.md): se revisó
 * `PurchaseReturnableLinesEditor.tsx` (tabla de líneas con `useFieldArray` +
 * `ZhDecimalInput`, mismo patrón de tabla/estilos reutilizado aquí) y
 * `ReturnableLinesEditor.tsx` (SalesReturn). Ninguno aplica directamente:
 * ambos derivan sus filas de una lista fija ya calculada por el servidor
 * (líneas de factura con remanente), mientras que esta pantalla necesita
 * filas dinámicas libres (concepto + monto) sin origen en ninguna línea de
 * factura — no existe un editor de líneas libres reutilizable en el módulo
 * Purchases. Se extiende el mismo patrón visual (`pf-table`, `zh-text-align-right`,
 * `table-scroll`) en vez de crear una tabla nueva desde cero.
 *
 * Nunca incluye Item/Bodega/Cantidad — diseño FLOW-READY-02C §2.2: esta
 * entidad es solo para descuento/promoción, nunca mueve inventario.
 */
export function PurchaseCreditNoteDiscountLinesEditor({
  register,
  fields,
  append,
  remove,
  disabled,
}: Readonly<Props>) {
  const { t } = useI18n();

  return (
    <div className="table-scroll">
      <table className="pf-table">
        <thead>
          <tr>
            <th>{t("purchases.creditNote.lines.description", "Concepto")}</th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.lines.subtotal", "Subtotal")}
            </th>
            <th className="zh-text-align-right">
              {t("purchases.creditNote.lines.vatAmount", "IVA")}
            </th>
            <th aria-label={t("purchases.creditNote.lines.remove", "Eliminar")} />
          </tr>
        </thead>
        <tbody>
          {fields.map((field, index) => (
            <tr key={field.id}>
              <td>
                <ZhTextInput
                  className="zh-input"
                  maxLength={500}
                  disabled={disabled}
                  {...register(`lines.${index}.description` as const)}
                />
              </td>
              <td className="zh-text-align-right">
                <ZhDecimalInput
                  decimals={2}
                  positiveOnly
                  disabled={disabled}
                  {...register(`lines.${index}.subtotal` as const)}
                />
              </td>
              <td className="zh-text-align-right">
                <ZhDecimalInput
                  decimals={2}
                  positiveOnly
                  disabled={disabled}
                  {...register(`lines.${index}.vatAmount` as const)}
                />
              </td>
              <td>
                <ZHIconButton
                  icon="delete"
                  variant="danger"
                  title={t("purchases.creditNote.lines.remove", "Eliminar línea")}
                  onClick={() => remove(index)}
                  disabled={disabled}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <ZHBtn
        type="button"
        variant="ghost"
        onClick={() =>
          append({ description: "", subtotal: 0, vatCode: null, vatRate: null, vatAmount: 0 })
        }
        disabled={disabled}
      >
        {t("purchases.creditNote.lines.add", "+ Agregar línea")}
      </ZHBtn>
    </div>
  );
}
