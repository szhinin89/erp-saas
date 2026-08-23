import { Badge, type BadgeVariant } from "../../PageShell";
import type { NoticeSeverity } from "../../../notices";

const SEVERITY_TO_BADGE_VARIANT: Record<NoticeSeverity, BadgeVariant> = {
  info: "info",
  success: "success",
  warning: "warning",
  danger: "error",
};

interface ZHNoticeBadgeProps {
  id?: string;
  label: string;
  severity: NoticeSeverity;
  expanded: boolean;
  describedById?: string;
  onClick: () => void;
  onMouseEnter?: () => void;
  triggerRef: React.RefObject<HTMLButtonElement | null>;
}

/** Trigger de aviso compacto: envuelve Badge (visual, sin ref/aria propios) en un <button> que
 * aporta el ref de anclaje + semántica de disclosure — mismo patrón aria que ZHHelpIcon. */
export function ZHNoticeBadge({
  id,
  label,
  severity,
  expanded,
  describedById,
  onClick,
  onMouseEnter,
  triggerRef,
}: ZHNoticeBadgeProps) {
  return (
    <button
      id={id}
      ref={triggerRef}
      type="button"
      className="zh-notice-badge-trigger"
      aria-haspopup="dialog"
      aria-expanded={expanded}
      aria-describedby={expanded ? describedById : undefined}
      onClick={onClick}
      onMouseEnter={onMouseEnter}
      onFocus={onMouseEnter}
    >
      <Badge variant={SEVERITY_TO_BADGE_VARIANT[severity]} label={label} size="md" />
    </button>
  );
}
