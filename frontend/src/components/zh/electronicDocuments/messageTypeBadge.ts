import type { BadgeVariant } from "../../PageShell";

/** Variante del componente `Badge` por `messageType` de un mensaje SRI — mismo patrón que `electronicDocumentStateBadgeVariant`. */
export function messageTypeBadgeVariant(messageType: string): BadgeVariant {
  switch (messageType.toUpperCase()) {
    case "ERROR":
      return "red";
    case "ADVERTENCIA":
    case "WARNING":
      return "orange";
    case "INFORMATIVO":
    case "INFO":
      return "blue";
    default:
      return "gray";
  }
}
