import { Badge } from "../../PageShell";
import { useI18n } from "../../../i18n/i18n";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import { messageTypeBadgeVariant } from "./messageTypeBadge";
import type { ElectronicDocumentMessageDto } from "./electronicDocumentDiagnosticTypes";
import "./electronic-documents.css";

type Props = { messages: ElectronicDocumentMessageDto[] };

/**
 * Lista completa de mensajes SRI — cada uno se muestra verbatim (código, tipo, mensaje,
 * información adicional), tal como el SRI lo envió. No resume, no traduce, no trunca.
 */
export function ElectronicDocumentSriMessages({ messages }: Props) {
  const { t } = useI18n();

  if (messages.length === 0) {
    return (
      <p className="zh-edoc-hint-sm">
        {t("electronicDocuments.diagnostic.messagesEmpty")}
      </p>
    );
  }

  return (
    <div className="zh-edoc-messages">
      {messages.map((m, idx) => (
        <div key={idx} className="zh-edoc-message-card">
          <div className="zh-edoc-message-header">
            <Badge
              variant={messageTypeBadgeVariant(m.messageType)}
              label={m.messageType}
              upper
            />
            {m.code && <span className="zh-edoc-message-code zh-code-value">#{m.code}</span>}
            <span className="zh-edoc-hint-sm">
              {formatDateTime(m.occurredAtUtc)}
            </span>
          </div>
          <div className="zh-edoc-message-text">{m.message}</div>
          {m.additionalInfo && (
            <div className="zh-edoc-message-additional">{m.additionalInfo}</div>
          )}
        </div>
      ))}
    </div>
  );
}
