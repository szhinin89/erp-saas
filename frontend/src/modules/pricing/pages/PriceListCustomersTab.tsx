import { useCallback, useEffect, useRef, useState } from "react";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHConfirmModal } from "../../../components/zh/ZHConfirmModal";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhTextInput } from "../../../components/zh/inputs";
import { formatApiRequestError } from "../../lib/apiError";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { customerLookupFacade } from "../../masterData/facades/customerLookupFacade";
import type { CustomerPickerRow } from "../../masterData/facades/customerLookupFacade";
import {
  priceListCustomerService,
  type PriceListDto,
  type PriceListCustomerDto,
} from "../api/pricingService";

// PRICING-CUSTOMER-PRICE-LIST-ADMIN-05B: administración de PriceListCustomer desde
// /products/pricing → tab "Clientes de la lista". Reutiliza el patrón ya establecido de
// RemoteItemPicker (PriceListExceptionsTab) para el buscador, y el patrón de confirmación
// ExistsInactive/ExistsInactive→enable de PricingRule para el conflicto "otra lista activa" →
// aquí Conflict→assign(confirmSwitch=true). Todavía SIN consumo desde Sales.

export function PriceListCustomersTab({ priceList }: { priceList: PriceListDto }) {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const [rows, setRows] = useState<PriceListCustomerDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [assigning, setAssigning] = useState(false);

  const [conflict, setConflict] = useState<{
    customer: CustomerPickerRow;
    conflictingPriceListName: string | null;
  } | null>(null);

  const fetchAll = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const list = await priceListCustomerService.list(priceList.id);
      setRows(list);
    } catch (e: unknown) {
      setError(formatApiRequestError(e, { generic: t("pricing.ux.error") }));
    }
    setLoading(false);
  }, [priceList.id, t]);

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  const assignCustomer = useCallback(
    async (customer: CustomerPickerRow, confirmSwitch = false) => {
      if (!canShow("pricing.update")) return;
      setAssigning(true);
      setError("");
      try {
        const result = await priceListCustomerService.assign(
          priceList.id,
          customer.id,
          confirmSwitch,
        );
        if (result.status === "Conflict") {
          setConflict({ customer, conflictingPriceListName: result.conflictingPriceListName });
          return;
        }
        setConflict(null);
        if (result.status === "Switched") {
          message.success(t("pricing.ux.customerSwitched"));
        } else if (result.status === "Assigned") {
          message.success(t("pricing.ux.customerAssigned"));
        }
        await fetchAll();
      } catch (e: unknown) {
        setError(formatApiRequestError(e, { generic: t("pricing.ux.error") }));
      } finally {
        setAssigning(false);
      }
    },
    [priceList.id, canShow, fetchAll, t],
  );

  const removeCustomer = useCallback(
    async (row: PriceListCustomerDto) => {
      if (!canShow("pricing.update")) return;
      const confirmed = await message.confirm({
        title: t("pricing.ux.removeCustomer"),
        message: t("pricing.ux.removeCustomerConfirm", { name: row.customerName }),
        confirmLabel: t("pricing.ux.removeCustomer"),
        cancelLabel: t("pricing.ux.cancel"),
      });
      if (!confirmed) return;
      try {
        await priceListCustomerService.remove(priceList.id, row.customerId);
        message.success(t("pricing.ux.customerRemoved"));
        await fetchAll();
      } catch (e: unknown) {
        setError(formatApiRequestError(e, { generic: t("pricing.ux.error") }));
      }
    },
    [priceList.id, canShow, fetchAll, t],
  );

  const columns: ZHDataTableColumn<PriceListCustomerDto>[] = [
    { key: "customerName", header: t("pricing.ux.customer"), render: (row) => row.customerName },
    {
      key: "identification",
      header: t("pricing.ux.identification"),
      render: (row) => (
        <span className="prd-td-code">{row.customerIdentificationNumber ?? "—"}</span>
      ),
    },
    {
      key: "actions",
      header: t("pricing.ux.actions"),
      render: (row) =>
        canShow("pricing.update") ? (
          <ZHIconButton
            icon="person_remove"
            title={t("pricing.ux.removeCustomer")}
            variant="danger"
            onClick={() => void removeCustomer(row)}
          />
        ) : null,
    },
  ];

  return (
    <div className="prd-section">
      {error && <div className="prd-error-banner">{error}</div>}

      <p className="zh-text-muted">{t("pricing.ux.customersHint")}</p>

      {canShow("pricing.update") && priceList.isActive && (
        <div className="zh-field">
          <label className="zh-field-label">{t("pricing.ux.assignCustomer")}</label>
          {assigning ? (
            <p>{t("pricing.ux.saving")}</p>
          ) : (
            <RemoteCustomerPicker onSelect={(customer) => void assignCustomer(customer)} />
          )}
        </div>
      )}

      <ZHDataTable
        columns={columns}
        rows={rows}
        rowKey={(row) => row.customerId}
        loading={loading}
        showRowNumber
        emptyMessage={t("pricing.ux.noCustomers")}
      />

      <ZHConfirmModal
        open={conflict !== null}
        variant="default"
        title={t("pricing.ux.switchConfirmTitle")}
        message={t("pricing.ux.switchConfirmMessage", {
          currentList: conflict?.conflictingPriceListName ?? "",
          newList: priceList.name,
        })}
        confirmLabel={t("pricing.ux.switchConfirm")}
        cancelLabel={t("pricing.ux.cancel")}
        onConfirm={() => {
          if (conflict) void assignCustomer(conflict.customer, true);
        }}
        onCancel={() => setConflict(null)}
      />
    </div>
  );
}

// ── Buscador remoto de clientes (nombre / identificación) — mismo patrón que
// RemoteItemPicker (PriceListExceptionsTab.tsx), sin cargar el catálogo completo ──

function RemoteCustomerPicker({
  onSelect,
}: {
  onSelect: (customer: CustomerPickerRow) => void;
}) {
  const { t } = useI18n();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<CustomerPickerRow[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [searchError, setSearchError] = useState("");
  const wrapRef = useRef<HTMLDivElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  useEffect(() => {
    let current = true;
    clearTimeout(debounceRef.current);
    if (query.trim().length < 2) {
      setResults([]);
      return;
    }
    debounceRef.current = setTimeout(async () => {
      setLoading(true);
      setSearchError("");
      try {
        const rows = await customerLookupFacade.searchCustomers(query.trim());
        if (current) setResults(rows);
      } catch (e) {
        if (current) {
          setResults([]);
          setSearchError(formatApiRequestError(e, { generic: t("pricing.ux.error") }));
        }
      }
      if (current) setLoading(false);
    }, 300);
    return () => {
      current = false;
      clearTimeout(debounceRef.current);
    };
  }, [query, t]);

  return (
    <div ref={wrapRef} className="zh-picker">
      {searchError && <p role="alert">{searchError}</p>}
      <ZhTextInput
        className="zh-input"
        value={query}
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
        onFocus={() => {
          if (query.length >= 2) setOpen(true);
        }}
        placeholder={t("pricing.ux.searchCustomer")}
      />
      {open && query.length >= 2 && (
        <div className="zh-picker__dropdown">
          {loading ? (
            <div className="zh-picker__loading">{t("pricing.ux.loading")}</div>
          ) : results.length === 0 ? (
            <div className="zh-picker__empty">{t("pricing.ux.noResults")}</div>
          ) : (
            results.map((customer) => (
              <button
                key={customer.id}
                type="button"
                className="zh-picker__result"
                onClick={() => {
                  onSelect(customer);
                  setQuery("");
                  setResults([]);
                  setOpen(false);
                }}
              >
                <div className="zh-picker__result-main">
                  <span className="zh-picker__result-code">
                    {customer.identificationNumber}
                  </span>
                  {customer.fullName}
                </div>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  );
}
