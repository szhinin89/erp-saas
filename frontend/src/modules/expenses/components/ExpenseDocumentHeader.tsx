import { useCallback, useEffect, useRef, useState } from "react";
import { Badge } from "../../../components/PageShell";
import { ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhDateTimeInput } from "../../../components/zh/inputs/ZhDateTimeInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhTextarea } from "../../../components/zh/inputs/ZhTextarea";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZHPickerResultItem } from "../../../components/zh/ZHPickerResultItem";
import { ZHPickerSelectedValue } from "../../../components/zh/ZHPickerSelectedValue";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";
import type { PaymentTermDto } from "../../masterData/api/paymentTermService";
import type { SupplierPickerRow } from "../../masterData/types/businessPartner.types";
import type { SriTaxSupportLookup } from "../../items/facades/sriLookupFacade";

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
          <ExpenseSupplierSelector
            value={supplier}
            disabled={disabled}
            onChange={(next) => {
              onSupplierChange(next);
              onChange({
                supplierId: next?.id ?? "",
                paymentTermId: value.paymentTermId || next?.supplierConfig?.paymentTermId || "",
                // RETENTIONS-EXPENSE-TAX-SUPPORT-UI-02H — mismo criterio que paymentTermId: si el
                // usuario ya escribió algo, se preserva; si no, se sugiere el default del
                // proveedor (solo pre-llenado en el cliente — el backend vuelve a aplicar el
                // mismo fallback si este campo llega vacío).
                taxSupportCode:
                  value.taxSupportCode || next?.supplierConfig?.defaultTaxSupportCode || "",
              });
            }}
          />
        </ZHField>

        <ZHField label="Tipo de documento" required fieldError={errors?.documentType}>
          <ZhTextInput
            value={value.documentType}
            mode="uppercase"
            maxLength={5}
            disabled={disabled}
            onChange={(event) => onChange({ documentType: event.target.value })}
          />
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

function ExpenseSupplierSelector({
  value,
  disabled,
  onChange,
}: {
  value: SupplierPickerRow | null;
  disabled?: boolean;
  onChange: (supplier: SupplierPickerRow | null) => void;
}) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<SupplierPickerRow[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [focusIdx, setFocusIdx] = useState(-1);
  const wrapRef = useRef<HTMLDivElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const search = useCallback(async (q: string) => {
    if (q.trim().length < 2) {
      setResults([]);
      return;
    }
    setLoading(true);
    try {
      setResults(await businessPartnerFacade.searchSuppliersForPicker(q.trim()));
      setFocusIdx(-1);
    } catch {
      setResults([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (!open || query.length < 2) return;
    debounceRef.current = setTimeout(() => search(query), 300);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [open, query, search]);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(event.target as Node))
        setOpen(false);
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  if (value) {
    return (
      <ZHPickerSelectedValue
        title={value.fullName}
        subtitle={value.identificationNumber}
        meta={
          !value.isActive && (
            <Badge variant="warning" label="Proveedor inactivo" />
          )
        }
        clearLabel="Cambiar proveedor"
        onClear={disabled ? undefined : () => onChange(null)}
      />
    );
  }

  const handleSelect = (row: SupplierPickerRow) => {
    onChange(row);
    setOpen(false);
    setQuery("");
    setResults([]);
  };

  return (
    <div ref={wrapRef} className="zh-picker">
      <div className="zh-picker__input-wrap">
        <ZhTextInput
          value={query}
          disabled={disabled}
          placeholder="Buscar por RUC, razon social o nombre..."
          onChange={(event) => {
            setQuery(event.target.value);
            setOpen(true);
          }}
          onFocus={() => {
            if (query.length >= 2) setOpen(true);
          }}
          onKeyDown={(event) => {
            if (!open || results.length === 0) return;
            if (event.key === "ArrowDown") {
              event.preventDefault();
              setFocusIdx((idx) => Math.min(idx + 1, results.length - 1));
            }
            if (event.key === "ArrowUp") {
              event.preventDefault();
              setFocusIdx((idx) => Math.max(idx - 1, 0));
            }
            if (event.key === "Enter" && focusIdx >= 0) {
              event.preventDefault();
              handleSelect(results[focusIdx]!);
            }
            if (event.key === "Escape") setOpen(false);
          }}
        />
        {loading && <span className="zh-picker__loading">Buscando...</span>}
      </div>

      {open && query.length >= 2 && (
        <div className="zh-picker__dropdown">
          {results.length === 0 && !loading && (
            <div className="zh-picker__empty">Sin proveedores activos.</div>
          )}
          {results.map((row, index) => (
            <ZHPickerResultItem
              key={row.id}
              title={row.fullName}
              subtitle={row.identificationNumber}
              meta={!row.isActive ? "Proveedor inactivo" : null}
              selected={index === focusIdx}
              onClick={() => handleSelect(row)}
              onMouseEnter={() => setFocusIdx(index)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
