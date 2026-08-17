import { ZHIconButton } from "../../../../components/zh/ZHIconButton";
import { Badge } from "../../../../components/PageShell";
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
  electronicDocumentStateIcon,
} from "../utils/stateBadge";
import { copyToClipboard } from "../utils/clipboard";
import "./electronic-documents-monitor.css";

type Props = {
  items: ElectronicDocumentListItemDto[];
  loading: boolean;
  total: number;
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onSelect: (id: string) => void;
};

export function ElectronicDocumentsTable({
  items,
  loading,
  total,
  page,
  pageSize,
  onPageChange,
  onSelect,
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

  const columns: ZHDataTableColumn<ElectronicDocumentListItemDto>[] = [
    {
      key: "state",
      header: t("electronicDocuments.monitor.table.state"),
      render: (row) => (
        <Badge
          variant={electronicDocumentStateBadgeVariant(row.currentState)}
          label={
            <span className="edm-state-badge-label">
              <span className="material-symbols-outlined zh-icon-sm">
                {electronicDocumentStateIcon(row.currentState)}
              </span>
              {t(`electronicDocuments.monitor.state.${row.currentState}`)}
              {row.retryCount > 0 && row.currentState !== "Authorized" && (
                <span className="edm-retry-suffix">
                  {" "}
                  (
                  {t("electronicDocuments.monitor.table.retryNumber", {
                    count: row.retryCount,
                  })}
                  )
                </span>
              )}
            </span>
          }
        />
      ),
    },
    {
      key: "documentType",
      header: t("electronicDocuments.monitor.table.documentType"),
      render: (row) =>
        t(`electronicDocuments.monitor.documentType.${row.documentType}`),
    },
    {
      key: "documentNumber",
      header: t("electronicDocuments.monitor.table.documentNumber"),
      render: (row) => row.documentNumber ?? "—",
    },
    {
      key: "counterparty",
      header: t("electronicDocuments.monitor.table.counterparty"),
      render: (row) => row.counterpartyName ?? "—",
    },
    {
      key: "company",
      header: t("electronicDocuments.monitor.table.company"),
      render: (row) => row.companyName,
    },
    {
      key: "date",
      header: t("electronicDocuments.monitor.table.date"),
      render: (row) => formatDateTime(row.createdAt),
    },
    {
      key: "environment",
      header: t("electronicDocuments.monitor.table.environment"),
      render: (row) => environmentLabel(row.environment),
    },
    {
      key: "accessKey",
      header: t("electronicDocuments.monitor.table.accessKey"),
      render: (row) =>
        row.accessKey ? (
          <span className="edm-access-key zh-code-value">
            {row.accessKey.slice(0, 10)}…
            <ZHIconButton
              icon="content_copy"
              title={t("electronicDocuments.monitor.table.copyAccessKey")}
              onClick={(e) => void handleCopyAccessKey(e, row.accessKey!)}
            />
          </span>
        ) : (
          "—"
        ),
    },
    {
      key: "retries",
      header: t("electronicDocuments.monitor.table.retries"),
      align: "center",
      render: (row) => row.retryCount,
    },
    {
      key: "updatedAt",
      header: t("electronicDocuments.monitor.table.updatedAt"),
      render: (row) => (row.updatedAt ? formatDateTime(row.updatedAt) : "—"),
    },
    {
      key: "lastMessage",
      header: t("electronicDocuments.monitor.table.lastMessage"),
      render: (row) =>
        row.lastMessage ? (
          <span className="edm-last-message" title={row.lastMessage}>
            {row.lastMessage}
          </span>
        ) : (
          "—"
        ),
    },
  ];

  return (
    <ZHDataTable
      columns={columns}
      rows={items}
      rowKey={(row) => row.id}
      onRowClick={(row) => onSelect(row.id)}
      loading={loading}
      emptyMessage={t("electronicDocuments.monitor.table.empty")}
      page={page}
      pageSize={pageSize}
      total={total}
      onPageChange={onPageChange}
    />
  );
}
