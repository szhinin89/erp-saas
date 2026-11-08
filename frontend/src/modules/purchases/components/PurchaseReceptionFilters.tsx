import { useId, useState, type ChangeEvent, type ReactNode } from "react";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import {
  ZhCurrencyInput,
  ZhDateInput,
  ZhSearchSelect,
  ZhSelect,
  ZhTextInput,
} from "../../../components/zh/inputs";
import { useI18n } from "../../../i18n/i18n";
import type {
  ReceptionDateSortOrder,
  ReceptionSupplierOption,
} from "../hooks/useReceptionFilters";
import {
  RECEPTION_DOC_TYPES,
  RECEPTION_DOCUMENT_STATUSES,
  RECEPTION_PROCESSING_STATUSES,
  type ReceptionFilters,
} from "../utils/receptionFilters";
import {
  buildDocumentStatusLabel,
  buildProcessingStatusLabel,
} from "../pages/purchaseReceptionLabels";

// Getters estables (module-level) para ZhSearchSelect: el índice de búsqueda local se memoiza por
// referencia, así 2.000+ proveedores del TXT se normalizan una sola vez por importación.
const RECEPTION_SUPPLIER_MAX_RESULTS = 30;
const getSupplierKey = (o: ReceptionSupplierOption) => o.ruc;
const getSupplierName = (o: ReceptionSupplierOption) => o.name;
const getSupplierSearchText = (o: ReceptionSupplierOption) => `${o.name} ${o.ruc}`;
const getSupplierChipLabel = (o: ReceptionSupplierOption) => `${o.name} — ${o.ruc}`;

type SetFilter = <K extends keyof ReceptionFilters>(key: K, value: ReceptionFilters[K]) => void;

export interface PurchaseReceptionFiltersProps {
  filters: ReceptionFilters;
  setFilter: SetFilter;
  advancedFilterCount: number;
  supplierOptions: ReceptionSupplierOption[];
  selectedSupplierOptions: ReceptionSupplierOption[];
  setSupplierFilter: (rucs: readonly string[]) => void;
  dateSortOrder: ReceptionDateSortOrder;
  setDateSortOrder: (order: ReceptionDateSortOrder) => void;
  clearFilters: () => void;
  /** Comprobantes únicos filtrados (N) y del lote completo (M). */
  shownCount: number;
  totalCount: number;
  disabled?: boolean;
  /** Estado inicial de la zona plegable (por defecto cerrada). */
  defaultAdvancedOpen?: boolean;
}

/**
 * PURCHASES-RECEPTION-FILTERS-01 — barra de filtros compacta de `/purchases/reception`
 * (ZHFilterBar `plain`, sin tarjetas ni rótulos de altura extra) para que la tabla empiece lo más
 * arriba posible. Línea 1: búsqueda, proveedor (selector existente) y orden. Línea 2: "Solo
 * requieren atención" (checkbox del DS + ayuda contextual), "Más filtros", contador y "Limpiar
 * filtros". Criterios avanzados en una cuadrícula plegable, cerrada al inicio.
 */
export function PurchaseReceptionFilters(props: Readonly<PurchaseReceptionFiltersProps>) {
  const { filters, setFilter, disabled } = props;
  const { t } = useI18n();
  const advancedId = useId();
  const [advancedOpen, setAdvancedOpen] = useState(props.defaultAdvancedOpen ?? false);
  const documentStatusLabel = buildDocumentStatusLabel(t);
  const processingStatusLabel = buildProcessingStatusLabel(t);
  const all = t("purchases.reception.filters.all", "Todos");
  const sortLabel = t("purchases.reception.filters.dateSort", "Orden por emisión");

  const select = <K extends keyof ReceptionFilters>(
    key: K,
    label: string,
    options: ReadonlyArray<readonly [ReceptionFilters[K] & string, string]>,
  ): ReactNode => (
    <ZHField key={key} label={label} density="compact">
      <ZhSelect
        value={filters[key] as string}
        disabled={disabled}
        onChange={(e) => setFilter(key, e.target.value as ReceptionFilters[K])}
      >
        <option value="all">{all}</option>
        {options.map(([value, text]) => (
          <option key={value} value={value}>
            {text}
          </option>
        ))}
      </ZhSelect>
    </ZHField>
  );

  const textField = (
    key: "issueDateFrom" | "issueDateTo" | "totalMin" | "totalMax",
    label: string,
  ): ReactNode => {
    const inputProps = {
      value: filters[key],
      disabled,
      "aria-label": label,
      onChange: (e: ChangeEvent<HTMLInputElement>) => setFilter(key, e.target.value),
    };
    return (
      <ZHField key={key} label={label} density="compact">
        {key.startsWith("issueDate") ? (
          <ZhDateInput density="compact" {...inputProps} />
        ) : (
          <ZhCurrencyInput precision="money" {...inputProps} />
        )}
      </ZHField>
    );
  };

  return (
    <ZHFilterBar plain>
      {/* Línea 1 — sin rótulo visible: placeholder + aria-label (cada control se explica solo). */}
      <div className="zh-filterbar__field zh-filterbar__field--grow">
        <ZHField density="compact">
          <ZhTextInput
            density="compact"
            className="zh-input"
            value={filters.search}
            disabled={disabled}
            aria-label={t("purchases.reception.filters.search", "Buscar")}
            placeholder={t(
              "purchases.reception.filters.searchPlaceholder",
              "Buscar RUC, proveedor, número o clave de acceso",
            )}
            onChange={(e) => setFilter("search", e.target.value)}
          />
        </ZHField>
      </div>
      <div className="zh-filterbar__field zh-filterbar__field--grow">
        {/* ZH-SUPPLIER-SEARCH-REUSABLE-01 — buscador del DS sobre un datasource LOCAL (incluye
            proveedores no registrados del TXT; SupplierSearchSelect solo busca BP registrados). */}
        <ZHField density="compact">
          <ZhSearchSelect
            mode="multiple"
            options={props.supplierOptions}
            value={props.selectedSupplierOptions}
            onChange={(selected) => props.setSupplierFilter(selected.map((o) => o.ruc))}
            getOptionKey={getSupplierKey}
            getOptionLabel={getSupplierName}
            getOptionDescription={getSupplierKey}
            getOptionSearchText={getSupplierSearchText}
            getChipLabel={getSupplierChipLabel}
            maxResults={RECEPTION_SUPPLIER_MAX_RESULTS}
            disabled={disabled}
            aria-label={t("purchases.reception.filters.supplier", "Proveedor")}
            placeholder={t(
              "purchases.reception.filters.supplierSearch",
              "Buscar proveedor por nombre o RUC...",
            )}
            emptyText={t(
              "purchases.reception.filters.supplierEmpty",
              "Ningún proveedor del archivo coincide",
            )}
            truncatedText={t("supplierSearch.truncated", "Siga escribiendo para refinar la búsqueda.")}
            clearLabel={t("supplierSearch.clear", "Limpiar selección")}
            removeLabel={t("purchases.reception.filters.removeSupplier", "Quitar proveedor")}
          />
        </ZHField>
      </div>
      <div className="zh-filterbar__field">
        {/* Uso suelto con `density="compact"` (sin ZHField): la regla oficial
            `select.zh-input--compact` reserva el espacio del chevron, que el padding de
            `.zh-field--compact` anula dentro de un ZHField. */}
        <ZhSelect
          density="compact"
          className="zh-input"
          aria-label={sortLabel}
          title={sortLabel}
          value={props.dateSortOrder}
          disabled={disabled}
          onChange={(e) => props.setDateSortOrder(e.target.value as ReceptionDateSortOrder)}
        >
          <option value="desc">{t("purchases.reception.filters.sortDesc", "Más reciente primero")}</option>
          <option value="asc">{t("purchases.reception.filters.sortAsc", "Más antiguo primero")}</option>
        </ZhSelect>
      </div>

      {/* Línea 2 — acciones de filtro y contador en una sola fila. */}
      <div className="pur-reception-filters__actions">
        <span className="pur-reception-filters__attention">
          <label className="zh-checkbox-label">
            <input
              type="checkbox"
              checked={filters.attentionOnly}
              disabled={disabled}
              onChange={(e) => setFilter("attentionOnly", e.target.checked)}
            />
            {t("purchases.reception.filters.attention", "Solo requieren atención")}
          </label>
          <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_RECEPTION_ATTENTION} />
        </span>
        <ZHBtn
          type="button"
          size="sm"
          icon="tune"
          expanded={advancedOpen}
          aria-controls={advancedId}
          onClick={() => setAdvancedOpen((open) => !open)}
        >
          {props.advancedFilterCount > 0
            ? t("purchases.reception.filters.moreActive", { count: props.advancedFilterCount })
            : t("purchases.reception.filters.more", "Más filtros")}
        </ZHBtn>
        <span className="subtle" aria-live="polite">
          {t("purchases.reception.filters.showing", {
            shown: props.shownCount,
            total: props.totalCount,
          })}
        </span>
        <ZHBtn
          variant="ghost"
          size="sm"
          type="button"
          icon="filter_alt_off"
          className="zh-filterbar__clear"
          onClick={props.clearFilters}
          disabled={disabled}
        >
          {t("purchases.reception.filters.clear", "Limpiar filtros")}
        </ZHBtn>
      </div>

      {advancedOpen && (
        <div id={advancedId} className="pur-reception-filters__advanced">
          {/* Misma situación que las tarjetas de resumen (un único estado: erpSituation). */}
          {select("erpSituation", t("purchases.reception.filters.erpSituation", "Situación ERP"), [
            ["registered", t("purchases.reception.summary.registered.title", "Registrados en ERP")],
            ["toProcess", t("purchases.reception.summary.toProcess.title", "Por procesar")],
            [
              "pendingSupplier",
              t("purchases.reception.summary.pendingSupplier.title", "Pendientes de proveedor"),
            ],
            ["unregistered", t("purchases.reception.filters.erp.unregistered", "Sin registrar")],
            [
              "cancelledHistory",
              t("purchases.reception.filters.erp.cancelledHistory", "Historial anulado"),
            ],
            ["review", t("purchases.reception.erpRecord.inReview", "En revisión")],
          ])}
          {select("supplierState", t("purchases.reception.filters.supplierState", "Estado proveedor"), [
            ["registered", t("purchases.reception.supplier.registered", "Proveedor registrado")],
            ["inactive", t("purchases.reception.supplier.inactive", "Proveedor inactivo")],
            ["notRegistered", t("purchases.reception.supplier.notRegistered", "Proveedor no registrado")],
          ])}
          {select("xmlState", t("purchases.reception.filters.xml", "XML"), [
            ["stored", t("purchases.reception.xml.stored", "Almacenado")],
            ["pending", t("purchases.reception.xml.pending", "Pendiente")],
            ["loading", t("purchases.reception.filters.xmlLoadingSession", "Consultando (esta sesión)")],
            ["error", t("purchases.reception.xml.sessionError", "Error en esta sesión")],
          ])}
          {select(
            "documentStatus",
            t("purchases.reception.filters.documentStatus", "Recepción"),
            RECEPTION_DOCUMENT_STATUSES.map((s) => [s, documentStatusLabel[s]] as const),
          )}
          {select(
            "processingStatus",
            t("purchases.reception.filters.processingStatus", "Lectura"),
            RECEPTION_PROCESSING_STATUSES.map((s) => [s, processingStatusLabel[s]] as const),
          )}
          {select(
            "docType",
            t("purchases.reception.filters.docType", "Tipo"),
            RECEPTION_DOC_TYPES.map(
              (s) =>
                [
                  s,
                  s === "INVOICE"
                    ? t("purchases.reception.filters.docType.invoice", "Factura")
                    : t("purchases.reception.filters.docType.creditNote", "Nota de crédito"),
                ] as const,
            ),
          )}
          {textField("issueDateFrom", t("purchases.reception.filters.issueDateFrom", "Emisión desde"))}
          {textField("issueDateTo", t("purchases.reception.filters.issueDateTo", "Emisión hasta"))}
          {textField("totalMin", t("purchases.reception.filters.totalMin", "Total mínimo"))}
          {textField("totalMax", t("purchases.reception.filters.totalMax", "Total máximo"))}
        </div>
      )}
    </ZHFilterBar>
  );
}
