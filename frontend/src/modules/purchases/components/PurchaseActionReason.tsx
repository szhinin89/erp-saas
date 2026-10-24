import { useId, useState } from "react";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { useI18n } from "../../../i18n/i18n";

/** On-demand explanation beside a disabled action; also operable by touch and keyboard. */
export function PurchaseActionReason({ reason, action, compact = true }: { reason?: string | null; action: string; compact?: boolean }) {
  const [open, setOpen] = useState(false);
  const id = useId();
  const { t } = useI18n();
  if (!reason) return null;
  return <span className="zh-stack">
    <ZHBtn type="button" variant="secondary" size={compact ? "sm" : undefined} expanded={open} aria-controls={id}
      aria-label={`${t("purchases.validation.actionReason", "Ver motivo")}: ${action}`}
      onClick={() => setOpen(value => !value)}>{t("purchases.validation.actionReason", "Ver motivo")}</ZHBtn>
    {open && <span id={id} role="status"><ZHDataValue variant="muted">{reason}</ZHDataValue></span>}
  </span>;
}
