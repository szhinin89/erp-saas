import { useCallback, useEffect, useRef, useState } from "react";
import { salesService, type SalesListItemDto } from "../api/salesService";
import { formatMoney } from "../../../lib/sanitizers";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { ZHPickerResultItem } from "../../../components/zh/ZHPickerResultItem";
import { ZHPickerSelectedValue } from "../../../components/zh/ZHPickerSelectedValue";

type Props = {
  value: SalesListItemDto | null;
  onChange: (invoice: SalesListItemDto | null) => void;
  disabled?: boolean;
};

/**
 * Busca facturas autorizadas por número (única fuente válida de origen de una
 * devolución — `CreateSalesReturnDraftCommand` exige `SalesInvoiceId` de una
 * factura `Authorized`). Reutiliza `salesService.list` (mismo endpoint que la
 * pantalla de Ventas), sin crear un endpoint ni un cliente HTTP nuevo.
 */
export function SalesReturnInvoicePicker({ value, onChange, disabled }: Props) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<SalesListItemDto[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  const search = useCallback(async (q: string) => {
    setLoading(true);
    try {
      const r = await salesService.list(q.trim() || undefined, "Authorized", 1, 10);
      setResults(r.items);
    } catch {
      setResults([]);
    }
    setLoading(false);
  }, []);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    if (!open) return;
    debounceRef.current = setTimeout(() => void search(query), 300);
    return () => clearTimeout(debounceRef.current);
  }, [query, open, search]);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const handleSelect = (row: SalesListItemDto) => {
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
        meta={`${value.customerName} — ${formatDate(value.issueDate)} — ${formatMoney(value.grandTotal)}`}
        clearLabel="Cambiar factura"
        onClear={disabled ? undefined : handleClear}
      />
    );
  }

  return (
    <div ref={wrapRef} className="sr-invoice-picker">
      <input
        className="zh-input"
        value={query}
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        placeholder="Buscar factura autorizada por número o cliente..."
        disabled={disabled}
      />
      {open && (
        <div className="sr-invoice-picker__menu">
          {loading && <div className="sr-invoice-picker__hint">Buscando...</div>}
          {!loading && results.length === 0 && (
            <div className="sr-invoice-picker__hint">
              Sin facturas autorizadas que coincidan.
            </div>
          )}
          {!loading &&
            results.map((row) => (
              <ZHPickerResultItem
                key={row.id}
                title={row.invoiceNumber}
                meta={`${row.customerName} — ${formatDate(row.issueDate)} — ${formatMoney(row.grandTotal)}`}
                onClick={() => handleSelect(row)}
              />
            ))}
        </div>
      )}
    </div>
  );
}
