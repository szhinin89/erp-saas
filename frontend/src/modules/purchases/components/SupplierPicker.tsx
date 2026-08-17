import { useState, useEffect, useRef, useCallback } from "react";
import { Badge } from "../../../components/PageShell";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZHPickerResultItem } from "../../../components/zh/ZHPickerResultItem";
import { ZHPickerSelectedValue } from "../../../components/zh/ZHPickerSelectedValue";
import { useI18n } from "../../../i18n/i18n";
import { businessPartnerFacade } from "../../masterData/api/businessPartnerFacade";
import {
  RoleTypeEnum,
  type SupplierPickerRow,
} from "../../masterData/types/businessPartner.types";
import { buildSupplierPickerRow } from "../utils/supplierProfile";

type Props = {
  value: string | null;
  onChange: (supplier: SupplierPickerRow | null) => void;
  disabled?: boolean;
};

export function SupplierPicker({ value, onChange, disabled }: Props) {
  const { t } = useI18n();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<SupplierPickerRow[]>([]);
  const [selected, setSelected] = useState<SupplierPickerRow | null>(null);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [focusIdx, setFocusIdx] = useState(-1);
  const wrapRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  const search = useCallback(async (q: string) => {
    if (q.trim().length < 2) {
      setResults([]);
      return;
    }
    setLoading(true);
    try {
      const rows = await businessPartnerFacade.searchBusinessPartners({
        q: q.trim(),
        roles: [RoleTypeEnum.Supplier],
        take: 100,
      });
      setResults(
        rows.map((bp) => ({
          id: bp.id,
          identificationNumber: bp.identificationNumber,
          fullName: bp.tradeName?.trim() || bp.legalName,
          isActive: bp.isActive,
          hasSupplierRole: true,
          supplierConfig: null,
        })),
      );
      setFocusIdx(-1);
    } catch {
      setResults([]);
    }
    setLoading(false);
  }, []);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    if (!open || query.length < 2) return;
    debounceRef.current = setTimeout(() => search(query), 300);
    return () => clearTimeout(debounceRef.current);
  }, [query, open, search]);

  useEffect(() => {
    if (value && !selected) {
      businessPartnerFacade
        .getBusinessPartner(value)
        .then((bp) => setSelected(buildSupplierPickerRow(bp)))
        .catch(() => {});
    }
    if (!value && selected) {
      setSelected(null);
    }
  }, [selected, value]);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node))
        setOpen(false);
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const handleSelect = (row: SupplierPickerRow) => {
    setSelected(row);
    setOpen(false);
    setQuery("");
    onChange(row);
  };

  const handleClear = () => {
    setSelected(null);
    setQuery("");
    setResults([]);
    onChange(null);
    inputRef.current?.focus();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (!open || results.length === 0) return;
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setFocusIdx((i) => Math.min(i + 1, results.length - 1));
    }
    if (e.key === "ArrowUp") {
      e.preventDefault();
      setFocusIdx((i) => Math.max(i - 1, 0));
    }
    if (e.key === "Enter" && focusIdx >= 0) {
      e.preventDefault();
      handleSelect(results[focusIdx]);
    }
    if (e.key === "Escape") {
      setOpen(false);
    }
  };

  if (selected) {
    return (
      <ZHPickerSelectedValue
        title={selected.fullName}
        subtitle={selected.identificationNumber}
        meta={
          !selected.isActive && (
            <Badge
              variant="warning"
              label={t("purchases.supplier.inactiveBadge", "Proveedor inactivo")}
            />
          )
        }
        clearLabel={t("purchases.supplierPicker.change", "Cambiar proveedor")}
        onClear={disabled ? undefined : handleClear}
      />
    );
  }

  return (
    <div ref={wrapRef} className="zh-picker">
      <div className="zh-picker__input-wrap">
        <ZhTextInput
          ref={inputRef}
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setOpen(true);
          }}
          onFocus={() => {
            if (query.length >= 2) setOpen(true);
          }}
          onKeyDown={handleKeyDown}
          placeholder={t(
            "purchases.supplierPicker.placeholder",
            "Buscar por RUC, razón social o nombre...",
          )}
          disabled={disabled}
        />
        {loading && (
          <span className="zh-picker__loading">
            {t("common.searching", "Buscando...")}
          </span>
        )}
      </div>

      {open && query.length >= 2 && (
        <div className="zh-picker__dropdown">
          {results.length === 0 && !loading && (
            <div className="zh-picker__empty">
              <div>{t("purchases.supplierPicker.noResults", { query })}</div>
              <div className="zh-picker__empty-help">
                {t(
                  "purchases.supplierPicker.emptyHelp",
                  "¿Aún no tiene proveedores registrados?",
                )}{" "}
                <a
                  href="/masterdata/suppliers"
                  className="zh-picker__link"
                >
                  {t("purchases.supplierPicker.register", "Registre un proveedor")}
                </a>
              </div>
            </div>
          )}
          {results.map((row, i) => (
            <ZHPickerResultItem
              key={row.id}
              title={row.fullName}
              subtitle={row.identificationNumber}
              meta={
                !row.isActive &&
                t("purchases.supplier.inactiveBadge", "Proveedor inactivo")
              }
              selected={i === focusIdx}
              onClick={() => handleSelect(row)}
              onMouseEnter={() => setFocusIdx(i)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
