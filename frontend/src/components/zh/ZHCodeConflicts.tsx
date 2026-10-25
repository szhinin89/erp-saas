import { ZHFormAlert, ZHLinkButton } from "./ZHForm";

export interface ZHCodeConflict {
  code: string;
  label: string;
  description: string;
  href?: string;
}

/** One presentation for code ownership and unsaved-batch conflicts; no lookup or validation. */
export function ZHCodeConflicts({ conflicts, viewLabel, compact = false }: {
  conflicts: ZHCodeConflict[];
  viewLabel: string;
  compact?: boolean;
}) {
  if (!conflicts.length) return null;
  if (compact) return <>{conflicts.map((c, i) => <span key={`${c.label}:${c.code}:${i}`} className="zh-notice-list__detail">
    {c.label}: {c.code} · {c.description}{" "}
    {c.href && <ZHLinkButton to={c.href} target="_blank" rel="noopener noreferrer" variant="ghost" size="sm">{viewLabel}</ZHLinkButton>}
  </span>)}</>;
  return <div role="alert" aria-live="polite">
    {conflicts.map((conflict, index) => <ZHFormAlert key={`${conflict.label}:${conflict.code}:${index}`}
      type="error" message={`${conflict.label}: ${conflict.code}`}
      detail={<>{conflict.description}{" "}{conflict.href &&
        <ZHLinkButton to={conflict.href} target="_blank" rel="noopener noreferrer" variant="ghost" size="sm">
          {viewLabel}
        </ZHLinkButton>}</>} />)}
  </div>;
}
