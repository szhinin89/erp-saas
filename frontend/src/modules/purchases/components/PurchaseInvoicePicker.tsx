import { useCallback, useEffect, useRef, useState } from "react";
import { ZHPickerResultItem } from "../../../components/zh/ZHPickerResultItem";
import { ZHPickerSelectedValue } from "../../../components/zh/ZHPickerSelectedValue";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { useI18n } from "../../../i18n/i18n";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { purchaseService, type PurchaseListItemDto } from "../api/purchaseService";

type Props = {
  supplierId: string;
  value: PurchaseListItemDto | null;
  onChange: (invoice: PurchaseListItemDto | null) => void;
  disabled?: boolean;
};

/**
 * PURCHASE-CREDIT-NOTE-ENTRY-SCREEN-DUAL-MODE-01 — busca facturas de compra
 * `Confirmed` de un proveedor ya elegido (única fuente válida para afectar con
 * una NC — `CreateDraftPurchaseCreditNoteHandler` rechaza cualquier otro
 * estado). Reutiliza `purchaseService.list` (mismo endpoint que `PurchasesPage`,
 * ahora con `supplierId` opcional) y el patrón visual de `SupplierPicker.tsx`
 * (clases `zh-picker*` compartidas) — sin crear un endpoint, cliente HTTP ni
 * componente de picker paralelo.
 */
export function PurchaseInvoicePicker({ supplierId, value, onChange, disabled }: Props) {
  const { t } = useI18n();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<PurchaseListItemDto[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  const search = useCallback(
    async (q: string) => {
      setLoading(true);
      try {
        const r = await purchaseService.list(q.trim() || undefined, "Confirmed", 1, 10, supplierId);
        setResults(r.items);
      } catch {
        setResults([]);
      }
      setLoading(false);
    },
    [supplierId],
  );

  useEffect(() => {
    clearTimeout(debounceRef.current);
    if (!open) return;
    debounceRef.current = setTimeout(() => void search(query), 300);
    return () => clearTimeout(debounceRef.current);
  }, [query, open, search]);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const handleSelect = (row: PurchaseListItemDto) => {
    onChange(row);
    setOpen(false);
    setQuery("");
  };

  const handleClear = () => {
    onChange(null);
    setQuery("");
    setResults([]);
  };

  if (value) {
    return (
      <ZHPickerSelectedValue
        title={value.invoiceNumber}
        subtitle={formatDate(value.issueDate)}
        clearLabel={t("purchases.creditNote.invoicePicker.change", "Cambiar factura")}
        onClear={disabled ? undefined : handleClear}
      />
    );
  }

  return (
    <div ref={wrapRef} className="zh-picker">
      <div className="zh-picker__input-wrap">
        <ZhTextInput
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          placeholder={t(
            "purchases.creditNote.invoicePicker.placeholder",
            "Buscar factura confirmada por número...",
          )}
          disabled={disabled}
        />
        {loading && (
          <span className="zh-picker__loading">{t("common.searching", "Buscando...")}</span>
        )}
      </div>
      {open && (
        <div className="zh-picker__dropdown">
          {!loading && results.length === 0 && (
            <div className="zh-picker__empty">
              {t(
                "purchases.creditNote.invoicePicker.noResults",
                "Sin facturas confirmadas de este proveedor.",
              )}
            </div>
          )}
          {!loading &&
            results.map((row) => (
              <ZHPickerResultItem
                key={row.id}
                title={row.invoiceNumber}
                subtitle={formatDate(row.issueDate)}
                onClick={() => handleSelect(row)}
              />
            ))}
        </div>
      )}
    </div>
  );
}
