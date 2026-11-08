import { ZHIconButton } from "../../../../components/zh/ZHIconButton";
import { Badge } from "../../../../components/PageShell";
import { ZHDataValue } from "../../../../components/zh/ZHDataValue";
import {
  ZHDataTable,
  type ZHDataTableColumn,
} from "../../../../components/zh/ZHDataTable";
import { useI18n } from "../../../../i18n/i18n";
import { message } from "../../../../lib/messages";
import { formatDateTime } from "../../../../lib/formatters/dateFormatters";
import type { ElectronicDocumentListItemDto } from "../api/electronicDocumentsMonitorService";
import {
  electronicDocumentStateBadgeVariant,
  electronicDocumentStateHelpKey,
  electronicDocumentStateIcon,
} from "../utils/stateBadge";
import { copyToClipboard } from "../../../../lib/clipboard";
import "./electronic-documents-monitor.css";
import "../../../../components/zh/electronicDocuments/electronic-documents.css";

type Props = {
  items: ElectronicDocumentListItemDto[];
  loading: boolean;
  onSelect: (id: string) => void;
  /**
   * `full` (Documentos): todas las columnas y paginación del servidor. `attention` (Resumen):
   * columnas de seguimiento, sin paginación ni numeración — mismos renders, sin tabla paralela.
   */
  variant?: "full" | "attention";
  total?: number;
  page?: number;
  pageSize?: number;
  onPageChange?: (page: number) => void;
  emptyMessage?: string;
};

/** Resumen («Requieren atención»): mismas columnas agrupadas, sin contraparte ni numeración. */
const ATTENTION_COLUMNS = new Set(["view", "document", "state", "tracking", "sri"]);

export function ElectronicDocumentsTable({
  items,
  loading,
  onSelect,
  variant = "full",
  total = 0,
  page = 1,
  pageSize = items.length,
  onPageChange,
  emptyMessage,
}: Props) {
  const { t } = useI18n();

  const handleCopyAccessKey = async (
    e: React.MouseEvent,
    accessKey: string,
  ) => {
    e.stopPropagation();
    const ok = await copyToClipboard(accessKey);
    if (ok)
      message.success(t("electronicDocuments.monitor.table.accessKeyCopied"));
    else
      message.error(t("electronicDocuments.monitor.table.accessKeyCopyFailed"));
  };

  const environmentLabel = (env: string | null) => {
    if (!env) return "—";
    return t(
      env === "2"
        ? "settings.electronicInvoicing.sri.environmentProd"
        : "settings.electronicInvoicing.sri.environmentTest",
    );
  };

  const stateHelp = (state: string) => {
    const key = electronicDocumentStateHelpKey(state);
    return key ? t(key) : undefined;
  };

  const documentLabel = (row: ElectronicDocumentListItemDto) =>
    row.documentNumber ??
    t(`electronicDocuments.monitor.documentType.${row.documentType}`);

  /**
   * ZH-DS-TABLE-DENSITY-01 — celdas agrupadas por significado: línea principal (`ZHDataValue`) y
   * secundaria (`ZHDataValue variant="muted"`), sin CSS propio. El detalle completo sigue en la ficha.
   */
  const stacked = (primary: React.ReactNode, secondary?: React.ReactNode) => (
    <>
      <div>{primary}</div>
      {secondary !== undefined && secondary !== null && <div>{secondary}</div>}
    </>
  );

  const allColumns: ZHDataTableColumn<ElectronicDocumentListItemDto>[] = [
    // Acceso explícito al detalle (botón nativo, operable con teclado). Va primero: así queda a la
    // vista aunque la tabla se desplace dentro de su contenedor en móvil.
    {
      key: "view",
      header: t("electronicDocuments.monitor.table.actions"),
      align: "center",
      render: (row) => (
        <ZHIconButton
          icon="visibility"
          variant="ghost"
          title={t("electronicDocuments.monitor.table.viewDetail")}
          ariaLabel={t("electronicDocuments.monitor.table.viewDetailOf", {
            document: documentLabel(row),
          })}
          onClick={(e) => {
            e.stopPropagation();
            onSelect(row.id);
          }}
        />
      ),
    },
    {
      key: "document",
      header: t("electronicDocuments.monitor.table.document"),
      render: (row) =>
        stacked(
          // ZH-DS-ERP-PATTERNS-01: número de documento = identificador principal, sin ajuste de línea.
          row.documentNumber ? (
            <span className="zh-code-value zh-code-value--document">{row.documentNumber}</span>
          ) : (
            <ZHDataValue variant="muted">—</ZHDataValue>
          ),
          <ZHDataValue variant="muted">
            {t(`electronicDocuments.monitor.documentType.${row.documentType}`)}
          </ZHDataValue>,
        ),
    },
    {
      key: "state",
      header: t("electronicDocuments.monitor.table.state"),
      render: (row) =>
        stacked(
          <Badge
            variant={electronicDocumentStateBadgeVariant(row.currentState)}
            title={stateHelp(row.currentState)}
            label={
              <span className="zh-edoc-state-badge-label">
                <span className="material-symbols-outlined zh-icon-sm">
                  {electronicDocumentStateIcon(row.currentState)}
                </span>
                {t(`electronicDocuments.monitor.state.${row.currentState}`)}
              </span>
            }
          />,
          <ZHDataValue variant="muted">{environmentLabel(row.environment)}</ZHDataValue>,
        ),
    },
    {
      // El listado abarca todas las empresas del tenant cuando no hay contexto de empresa: la
      // empresa (secundaria) distingue documentos de empresas distintas.
      key: "counterparty",
      header: t("electronicDocuments.monitor.table.counterpartyCompany"),
      render: (row) =>
        stacked(
          <ZHDataValue>{row.counterpartyName ?? "—"}</ZHDataValue>,
          <ZHDataValue variant="muted">{row.companyName}</ZHDataValue>,
        ),
    },
    {
      key: "tracking",
      header: t("electronicDocuments.monitor.table.tracking"),
      render: (row) =>
        stacked(
          <ZHDataValue>{formatDateTime(row.createdAt)}</ZHDataValue>,
          <ZHDataValue variant="muted">
            {t("electronicDocuments.monitor.table.updatedShort", {
              date: row.updatedAt ? formatDateTime(row.updatedAt) : "—",
            })}{" "}
            · {t("electronicDocuments.monitor.table.attemptsShort", {
              count: row.retryCount,
            })}
          </ZHDataValue>,
        ),
    },
    {
      key: "sri",
      header: t("electronicDocuments.monitor.table.sriAccess"),
      render: (row) =>
        stacked(
          row.lastMessage ? (
            <span className="edm-last-message" title={row.lastMessage}>
              {row.lastMessage}
            </span>
          ) : (
            <ZHDataValue variant="muted">
              {t("electronicDocuments.monitor.table.noMessage")}
            </ZHDataValue>
          ),
          row.accessKey ? (
            <span className="edm-access-key">
              <ZHDataValue variant="code">{row.accessKey.slice(0, 10)}…</ZHDataValue>
              <ZHIconButton
                icon="content_copy"
                title={t("electronicDocuments.monitor.table.copyAccessKey")}
                onClick={(e) => void handleCopyAccessKey(e, row.accessKey!)}
              />
            </span>
          ) : (
            <ZHDataValue variant="muted">
              {t("electronicDocuments.monitor.sheet.accessKeyPending")}
            </ZHDataValue>
          ),
        ),
    },
  ];

  const columns =
    variant === "attention"
      ? allColumns.filter((c) => ATTENTION_COLUMNS.has(c.key))
      : allColumns;

  const paginated = variant === "full" && onPageChange !== undefined;

  return (
    <ZHDataTable
      columns={columns}
      rows={items}
      rowKey={(row) => row.id}
      onRowClick={(row) => onSelect(row.id)}
      loading={loading}
      tableClassName="table--compact table--align-top"
      showRowNumber={variant === "full"}
      rowNumberOffset={(page - 1) * pageSize}
      emptyMessage={emptyMessage ?? t("electronicDocuments.monitor.table.empty")}
      page={paginated ? page : undefined}
      pageSize={paginated ? pageSize : undefined}
      total={paginated ? total : undefined}
      onPageChange={paginated ? onPageChange : undefined}
    />
  );
}
