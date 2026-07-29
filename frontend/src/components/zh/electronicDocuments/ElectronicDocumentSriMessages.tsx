import { Badge } from "../../PageShell";
import { useI18n } from "../../../i18n/i18n";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import { messageTypeBadgeVariant } from "./messageTypeBadge";
import type { ElectronicDocumentMessageDto } from "./electronicDocumentDiagnosticTypes";

type Props = { messages: ElectronicDocumentMessageDto[] };

/**
 * Lista completa de mensajes SRI — cada uno se muestra verbatim (código, tipo, mensaje,
 * información adicional), tal como el SRI lo envió. No resume, no traduce, no trunca.
 */
export function ElectronicDocumentSriMessages({ messages }: Props) {
  const { t } = useI18n();

  if (messages.length === 0) {
    return (
      <p className="edm-hint-sm">
        {t("electronicDocuments.diagnostic.messagesEmpty")}
      </p>
    );
  }

  return (
    <div className="edm-messages">
      {messages.map((m, idx) => (
        <div key={idx} className="edm-message-card">
          <div className="edm-message-header">
            <Badge
              variant={messageTypeBadgeVariant(m.messageType)}
              label={m.messageType}
              upper
            />
            {m.code && <span className="edm-message-code">#{m.code}</span>}
            <span className="edm-hint-sm">
              {formatDateTime(m.occurredAtUtc)}
            </span>
          </div>
          <div className="edm-message-text">{m.message}</div>
          {m.additionalInfo && (
            <div className="edm-message-additional">{m.additionalInfo}</div>
          )}
        </div>
      ))}
    </div>
  );
}
