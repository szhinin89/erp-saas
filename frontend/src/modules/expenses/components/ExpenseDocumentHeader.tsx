import { useRef, useState } from "react";
import { ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhDateTimeInput } from "../../../components/zh/inputs/ZhDateTimeInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhTextarea } from "../../../components/zh/inputs/ZhTextarea";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { Badge } from "../../../components/PageShell";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { useI18n } from "../../../i18n/i18n";
import { businessPartnerPurchaseSettingsFacade } from "../../masterData/facades/businessPartnerPurchaseSettingsFacade";
import type { PaymentTermDto } from "../../masterData/facades/paymentTermLookupFacade";
import type { SupplierPickerRow } from "../../masterData/facades/supplierPickerFacade";
import { SupplierSearchSelect } from "../../masterData/facades/supplierPickerFacade";
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
  /**
   * EXPENSE-UI-02 — campos cuyo valor proviene del comprobante XML recibido (solo presentación: no
   * cambia la importación ni el valor, que sigue siendo el real del XML y sigue siendo editable).
   */
  xmlFields?: ReadonlySet<keyof ExpenseDocumentHeaderState>;
  onChange: (patch: Partial<ExpenseDocumentHeaderState>) => void;
  onSupplierChange: (supplier: SupplierPickerRow | null) => void;
}

/**
 * EXPENSE-UX-01 (U6) — de dónde viene la condición de pago mostrada:
 * `saved` = ya registrada en el documento (o aún no tocada en esta sesión), `supplier` = pre-llenada
 * con el default del proveedor, `explicit` = elegida por el usuario. Es solo presentación: la
 * resolución real (explícita → default del proveedor → exigir selección) sigue siendo del backend.
 */
type PaymentTermOrigin = "saved" | "supplier" | "explicit";

/** Resultado de la consulta del default del proveedor seleccionado en esta sesión. */
interface SupplierPaymentDefault {
  supplierId: string;
  termId: string | null;
  failed: boolean;
}

export function ExpenseDocumentHeader({
  value,
  supplier,
  paymentTerms,
  sriTaxSupports,
  sriDocTypes,
  disabled,
  errors,
  xmlFields,
  onChange,
  onSupplierChange,
}: Props) {
  const { t } = useI18n();
  // Etiqueta de campo + insignia "XML" cuando el valor viene del comprobante recibido.
  const labelOf = (field: keyof ExpenseDocumentHeaderState, text: string) =>
    xmlFields?.has(field) ? (
      <>
        {text}{" "}
        <Badge label={t("expenses.form.origin.xml")} variant="blue" size="md" />
      </>
    ) : (
      text
    );
  const [paymentTermOrigin, setPaymentTermOrigin] = useState<PaymentTermOrigin>("saved");
  const [supplierDefault, setSupplierDefault] = useState<SupplierPaymentDefault | null>(null);
  // Invalida consultas en vuelo si el proveedor cambia o el usuario elige la condición a mano.
  const lookupSeq = useRef(0);

  const termLabel = (termId: string) => {
    const term = paymentTerms.find((item) => item.id === termId);
    return term ? `${term.code} - ${term.summary}` : termId;
  };

  const handleSupplierChange = (next: SupplierPickerRow | null) => {
    const seq = ++lookupSeq.current;
    onSupplierChange(next);
    // Un valor pre-llenado por el proveedor anterior nunca fue una elección del usuario: se vuelve
    // a evaluar con el proveedor nuevo. Una condición elegida a mano o ya guardada se preserva.
    const wasAutofilled = !!value.paymentTermId && paymentTermOrigin === "supplier";
    const prefillPaymentTerm = !value.paymentTermId || wasAutofilled;
    setSupplierDefault(null);
    onChange({
      supplierId: next?.id ?? "",
      // RETENTIONS-EXPENSE-TAX-SUPPORT-UI-02H — mismo criterio: si el usuario ya
      // escribió algo, se preserva; si no, se sugiere el default del proveedor (solo
      // pre-llenado en el cliente — el backend vuelve a aplicar el mismo fallback si
      // este campo llega vacío).
      taxSupportCode: value.taxSupportCode || next?.supplierConfig?.defaultTaxSupportCode || "",
    });
    if (wasAutofilled && !next) onChange({ paymentTermId: "" });
    // ADR-033: la condición de pago default de compras/gastos vive en
    // CompanyBpPurchaseSettings (empresa activa) — SupplierRoleConfig ya no la tiene.
    if (next && prefillPaymentTerm) {
      businessPartnerPurchaseSettingsFacade
        .getPurchaseSettings(next.id)
        .then((settings) => {
          if (seq !== lookupSeq.current) return;
          const termId = settings.paymentTermId || null;
          setSupplierDefault({ supplierId: next.id, termId, failed: false });
          if (termId) {
            onChange({ paymentTermId: termId });
            setPaymentTermOrigin("supplier");
          } else if (wasAutofilled) {
            onChange({ paymentTermId: "" });
          }
        })
        .catch(() => {
          if (seq !== lookupSeq.current) return;
          setSupplierDefault({ supplierId: next.id, termId: null, failed: true });
          if (wasAutofilled) onChange({ paymentTermId: "" });
        });
    }
  };

  const paymentTermHint = ((): { text: string; type: "muted" | "warning" } | null => {
    if (value.paymentTermId) {
      if (paymentTermOrigin === "supplier")
        return { text: t("expenses.form.paymentTerm.fromSupplier"), type: "muted" };
      if (paymentTermOrigin === "explicit")
        return { text: t("expenses.form.paymentTerm.explicit"), type: "muted" };
      return { text: t("expenses.form.paymentTerm.saved"), type: "muted" };
    }
    if (!value.supplierId)
      return { text: t("expenses.form.paymentTerm.awaitSupplier"), type: "muted" };
    if (supplierDefault?.supplierId !== value.supplierId) return null;
    if (supplierDefault.failed)
      return { text: t("expenses.form.paymentTerm.lookupFailed"), type: "warning" };
    if (supplierDefault.termId)
      return {
        text: t("expenses.form.paymentTerm.willUseSupplier", {
          term: termLabel(supplierDefault.termId),
        }),
        type: "muted",
      };
    return { text: t("expenses.form.paymentTerm.supplierWithout"), type: "warning" };
  })();

  return (
    <section className="exp-doc-section" aria-label={t("expenses.form.header.aria")}>
      <div className="exp-doc-section__heading">
        <div className="exp-doc-section__title-row">
          <h2>{t("expenses.form.header.title")}</h2>
          <ZHFieldHelp helpKey={HELP_KEYS.EXPENSES_DOCUMENT_DATA} />
        </div>
      </div>

      <fieldset className="exp-doc-group">
        <legend className="exp-doc-group__title">{t("expenses.form.header.group.document")}</legend>
        <ZHGrid cols={4}>
        <ZHField className="exp-doc-span-2" label={labelOf("supplierId", t("expenses.form.field.supplier"))} required fieldError={errors?.supplierId}>
          {/* ZH-SUPPLIER-SEARCH-SINGLE-SOURCE-02 — buscador único de proveedores; en Gastos solo
              se ofrecen proveedores activos (mismo criterio que el selector propio anterior). */}
          <SupplierSearchSelect
            activeOnly
            value={supplier}
            disabled={disabled}
            onChange={handleSupplierChange}
          />
        </ZHField>

        <ZHField
          label={labelOf("documentType", t("expenses.form.field.documentType"))}
          required
          fieldError={errors?.documentType}
        >
          <ZhSelect
            value={value.documentType}
            disabled={disabled}
            onChange={(event) => onChange({ documentType: event.target.value })}
          >
            <option value="">{t("expenses.form.field.documentTypeSelect")}</option>
            {value.documentType && !sriDocTypes.some((type) => type.code === value.documentType) && (
              <option value={value.documentType}>
                {t("expenses.form.field.documentTypeUnknown", { code: value.documentType })}
              </option>
            )}
            {sriDocTypes.map((type) => (
              <option key={type.code} value={type.code}>
                {type.code} - {type.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>

        <ZHField
          label={labelOf("documentNumber", t("expenses.form.field.documentNumber"))}
          required
          fieldError={errors?.documentNumber}
        >
          <ZhTextInput
            value={value.documentNumber}
            maxLength={30}
            disabled={disabled}
            onChange={(event) => onChange({ documentNumber: event.target.value })}
          />
        </ZHField>

        <ZHField className="exp-doc-span-3"
          label={labelOf("authorizationNumber", t("expenses.form.field.authorizationNumber"))}
          fieldError={errors?.authorizationNumber}
        >
          <ZhTextInput
            className="exp-doc-code-input"
            value={value.authorizationNumber}
            maxLength={49}
            disabled={disabled}
            onChange={(event) =>
              onChange({ authorizationNumber: event.target.value })
            }
          />
        </ZHField>

        <ZHField
          label={labelOf("authorizationDate", t("expenses.form.field.authorizationDate"))}
          fieldError={errors?.authorizationDate}
        >
          <ZhDateTimeInput
            value={value.authorizationDate}
            disabled={disabled}
            onChange={(event) => onChange({ authorizationDate: event.target.value })}
          />
        </ZHField>
        </ZHGrid>
      </fieldset>

      <fieldset className="exp-doc-group">
        <legend className="exp-doc-group__title">{t("expenses.form.header.group.dates")}</legend>
        <ZHGrid cols={3}>
        <ZHField label={labelOf("issueDate", t("expenses.form.field.issueDate"))} required fieldError={errors?.issueDate}>
          <ZhDateInput
            value={value.issueDate}
            disabled={disabled}
            onChange={(event) => onChange({ issueDate: event.target.value })}
          />
        </ZHField>

        <ZHField
          label={t("expenses.form.field.accountingDate")}
          required
          fieldError={errors?.accountingDate}
        >
          <ZhDateInput
            value={value.accountingDate}
            disabled={disabled}
            onChange={(event) => onChange({ accountingDate: event.target.value })}
          />
        </ZHField>

        <ZHField label={t("expenses.form.field.dueDate")} fieldError={errors?.dueDate}>
          <ZhDateInput
            value={value.dueDate}
            disabled={disabled}
            onChange={(event) => onChange({ dueDate: event.target.value })}
          />
        </ZHField>

        <ZHField
          label={t("expenses.form.field.paymentTerm")}
          fieldError={errors?.paymentTermId}
          hint={paymentTermHint?.text}
          hintType={paymentTermHint?.type}
        >
          <ZhSelect
            value={value.paymentTermId}
            disabled={disabled}
            onChange={(event) => {
              lookupSeq.current++;
              if (event.target.value) setPaymentTermOrigin("explicit");
              onChange({ paymentTermId: event.target.value });
            }}
          >
            <option value="">{t("expenses.form.paymentTerm.useSupplier")}</option>
            {paymentTerms
              .filter((term) => term.isActive || term.id === value.paymentTermId)
              .map((term) => (
                <option key={term.id} value={term.id}>
                  {term.code} - {term.summary}
                </option>
              ))}
          </ZhSelect>
        </ZHField>

        {/* RETENTIONS-EXPENSE-TAX-SUPPORT-UI-02H — mismo catálogo/servicio real ya usado por
            Compras (sriLookupFacade.taxSupportCodes → global.sri_tax_support), nunca una lista
            hardcodeada aquí. */}
        <ZHField className="exp-doc-span-2"
          label={t("expenses.form.field.taxSupport")}
          fieldError={errors?.taxSupportCode}
        >
          {/* La ayuda va DESPUÉS del select: el <label> de ZHField asocia como control el primer
              elemento etiquetable, que no debe ser el botón de ayuda. */}
          <div className="exp-doc-field-with-help">
            <ZhSelect
              value={value.taxSupportCode}
              disabled={disabled}
              onChange={(event) => onChange({ taxSupportCode: event.target.value })}
            >
              <option value="">{t("expenses.form.field.taxSupportNone")}</option>
              {sriTaxSupports.map((s) => (
                <option key={s.code} value={s.code}>
                  {s.code} — {s.name}
                </option>
              ))}
            </ZhSelect>
              <ZHFieldHelp helpKey={HELP_KEYS.EXPENSES_TAX_SUPPORT} />
          </div>
        </ZHField>

        </ZHGrid>
      </fieldset>

      <ZHField label={t("expenses.form.field.notes")} fieldError={errors?.notes}>
        <ZhTextarea
          value={value.notes}
          rows={2}
          maxLength={500}
          disabled={disabled}
          onChange={(event) => onChange({ notes: event.target.value })}
        />
      </ZHField>
    </section>
  );
}
