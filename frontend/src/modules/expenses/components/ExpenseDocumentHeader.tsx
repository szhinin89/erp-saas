import { ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhDateTimeInput } from "../../../components/zh/inputs/ZhDateTimeInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhTextarea } from "../../../components/zh/inputs/ZhTextarea";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";
import type { PaymentTermDto } from "../../masterData/api/paymentTermService";
import type { SupplierPickerRow } from "../../masterData/types/businessPartner.types";
import { SupplierSearchSelect } from "../../masterData/components/SupplierSearchSelect";
import type { SriDocTypeLookup, SriTaxSupportLookup } from "../../items/facades/sriLookupFacade";

export interface ExpenseDocumentHeaderState {
  supplierId: string;
  issueDate: string;
  accountingDate: string;
  documentType: string;
  documentNumber: string;
  paymentTermId: string;
  dueDate: string;
  authorizationNumber: string;
  authorizationDate: string;
  notes: string;
  /**
   * RETENTIONS-EXPENSE-TAX-SUPPORT-UI-02H — código de sustento tributario SRI (codSustento).
   * Vacío es válido: el backend usa el default del proveedor cuando este campo no se especifica
   * (ver `SupplierRoleConfig.DefaultTaxSupportCode`).
   */
  taxSupportCode: string;
}

export type ExpenseDocumentHeaderErrors = Partial<
  Record<keyof ExpenseDocumentHeaderState, string>
>;

interface Props {
  value: ExpenseDocumentHeaderState;
  supplier: SupplierPickerRow | null;
  paymentTerms: PaymentTermDto[];
  sriTaxSupports: SriTaxSupportLookup[];
  sriDocTypes: SriDocTypeLookup[];
  disabled?: boolean;
  errors?: ExpenseDocumentHeaderErrors;
  onChange: (patch: Partial<ExpenseDocumentHeaderState>) => void;
  onSupplierChange: (supplier: SupplierPickerRow | null) => void;
}

export function ExpenseDocumentHeader({
  value,
  supplier,
  paymentTerms,
  sriTaxSupports,
  sriDocTypes,
  disabled,
  errors,
  onChange,
  onSupplierChange,
}: Props) {
  return (
    <section className="exp-doc-section" aria-label="Cabecera del gasto">
      <div className="exp-doc-section__heading">
        <h2>Cabecera</h2>
        <p>Documento por proveedor, sin bodega ni datos de inventario.</p>
      </div>

      <ZHGrid cols={3}>
        <ZHField label="Proveedor" required fieldError={errors?.supplierId}>
          {/* ZH-SUPPLIER-SEARCH-SINGLE-SOURCE-02 — buscador único de proveedores; en Gastos solo
              se ofrecen proveedores activos (mismo criterio que el selector propio anterior). */}
          <SupplierSearchSelect
            activeOnly
            value={supplier}
            disabled={disabled}
            onChange={(next) => {
              onSupplierChange(next);
              const prefillPaymentTerm = !value.paymentTermId;
              onChange({
                supplierId: next?.id ?? "",
                // RETENTIONS-EXPENSE-TAX-SUPPORT-UI-02H — mismo criterio: si el usuario ya
                // escribió algo, se preserva; si no, se sugiere el default del proveedor (solo
                // pre-llenado en el cliente — el backend vuelve a aplicar el mismo fallback si
                // este campo llega vacío).
                taxSupportCode:
                  value.taxSupportCode || next?.supplierConfig?.defaultTaxSupportCode || "",
              });
              // ADR-033: la condición de pago default de compras/gastos vive en
              // CompanyBpPurchaseSettings (empresa activa) — SupplierRoleConfig ya no la tiene.
              if (next && prefillPaymentTerm) {
                businessPartnerFacade
                  .getPurchaseSettings(next.id)
                  .then((settings) => {
                    if (settings.paymentTermId) {
                      onChange({ paymentTermId: settings.paymentTermId });
                    }
                  })
                  .catch(() => {});
              }
            }}
          />
        </ZHField>

        <ZHField label="Tipo de documento" required fieldError={errors?.documentType}>
          <ZhSelect
            value={value.documentType}
            disabled={disabled}
            onChange={(event) => onChange({ documentType: event.target.value })}
          >
            <option value="">Seleccione un tipo de documento</option>
            {value.documentType && !sriDocTypes.some((type) => type.code === value.documentType) && (
              <option value={value.documentType}>
                {value.documentType} - Tipo de documento no encontrado en catálogo
              </option>
            )}
            {sriDocTypes.map((type) => (
              <option key={type.code} value={type.code}>
                {type.code} - {type.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>

        <ZHField label="Numero" required fieldError={errors?.documentNumber}>
          <ZhTextInput
            value={value.documentNumber}
            maxLength={30}
            disabled={disabled}
            onChange={(event) => onChange({ documentNumber: event.target.value })}
          />
        </ZHField>

        <ZHField label="Emision" required fieldError={errors?.issueDate}>
          <ZhDateInput
            value={value.issueDate}
            disabled={disabled}
            onChange={(event) => onChange({ issueDate: event.target.value })}
          />
        </ZHField>

        <ZHField label="Fecha contable" required fieldError={errors?.accountingDate}>
          <ZhDateInput
            value={value.accountingDate}
            disabled={disabled}
            onChange={(event) => onChange({ accountingDate: event.target.value })}
          />
        </ZHField>

        <ZHField label="Vencimiento" fieldError={errors?.dueDate}>
          <ZhDateInput
            value={value.dueDate}
            disabled={disabled}
            onChange={(event) => onChange({ dueDate: event.target.value })}
          />
        </ZHField>

        <ZHField label="Condicion de pago" fieldError={errors?.paymentTermId}>
          <ZhSelect
            value={value.paymentTermId}
            disabled={disabled}
            onChange={(event) => onChange({ paymentTermId: event.target.value })}
          >
            <option value="">Usar condicion del proveedor</option>
            {paymentTerms
              .filter((term) => term.isActive || term.id === value.paymentTermId)
              .map((term) => (
                <option key={term.id} value={term.id}>
                  {term.code} - {term.summary}
                </option>
              ))}
          </ZhSelect>
        </ZHField>

        <ZHField label="Autorizacion" fieldError={errors?.authorizationNumber}>
          <ZhTextInput
            value={value.authorizationNumber}
            maxLength={49}
            disabled={disabled}
            onChange={(event) =>
              onChange({ authorizationNumber: event.target.value })
            }
          />
        </ZHField>

        <ZHField label="Fecha autorizacion" fieldError={errors?.authorizationDate}>
          <ZhDateTimeInput
            value={value.authorizationDate}
            disabled={disabled}
            onChange={(event) => onChange({ authorizationDate: event.target.value })}
          />
        </ZHField>

        {/* RETENTIONS-EXPENSE-TAX-SUPPORT-UI-02H — mismo catálogo/servicio real ya usado por
            Compras (sriLookupFacade.taxSupportCodes → global.sri_tax_support), nunca una lista
            hardcodeada aquí. */}
        <ZHField
          label="Código sustento tributario"
          fieldError={errors?.taxSupportCode}
          hint="Si se deja vacío, se usará el valor configurado para el proveedor cuando exista."
        >
          <ZhSelect
            value={value.taxSupportCode}
            disabled={disabled}
            onChange={(event) => onChange({ taxSupportCode: event.target.value })}
          >
            <option value="">— Sin especificar —</option>
            {sriTaxSupports.map((s) => (
              <option key={s.code} value={s.code}>
                {s.code} — {s.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </ZHGrid>

      <ZHField label="Notas" fieldError={errors?.notes}>
        <ZhTextarea
          value={value.notes}
          rows={3}
          maxLength={500}
          disabled={disabled}
          onChange={(event) => onChange({ notes: event.target.value })}
        />
      </ZHField>
    </section>
  );
}
