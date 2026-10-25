import type { ReactNode } from "react";
import { useEffect, useId, useRef, useState } from "react";
import type { NoticeVM } from "../../../notices";
import { ZHBtn } from "../ZHForm";
import { ZHNoticeBadge } from "./ZHNoticeBadge";
import { ZHNoticePopover } from "./ZHNoticePopover";
import { ZHNoticeList } from "./ZHNoticeList";

export interface ZHLineIssuesTagProps {
  /** Ordered by the consumer's business priority; success is never an issue. */
  issues: NoticeVM[];
  children?: ReactNode;
  label: (primary: NoticeVM, additionalCount: number) => string;
  title: (count: number) => string;
}

/** ZH-DS-LINE-ISSUES-01: one floating disclosure, without business rules. */
export function ZHLineIssuesTag({ issues, label, title, children }: ZHLineIssuesTagProps) {
  const [open, setOpen] = useState(false);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const titleId = useId();
  const openedByKeyboard = useRef(false);
  const visibleIssues = issues.filter((issue) => issue.severity !== "success");
  const top = visibleIssues[0];
  const actions = visibleIssues.filter((n) => n.actionLabel && n.onAction);

  // Apertura por teclado: el panel vive en un portal (se posiciona un render después), así que
  // el foco pasa a su primera acción en cuanto el panel existe (máx. ~10 frames).
  useEffect(() => {
    if (!open || !openedByKeyboard.current) return;
    let frame = 0;
    let attempts = 0;
    const focusFirstAction = () => {
      const action = document.querySelector<HTMLElement>(
        `[aria-labelledby="${CSS.escape(titleId)}"] button, [aria-labelledby="${CSS.escape(titleId)}"] a[href]`,
      );
      if (action) action.focus();
      else if (++attempts < 10) frame = requestAnimationFrame(focusFirstAction);
    };
    frame = requestAnimationFrame(focusFirstAction);
    return () => cancelAnimationFrame(frame);
  }, [open, titleId]);

  if (!top) return null;

  return (
    <span className="zh-line-issues">
      <ZHNoticeBadge
        label={label(top, visibleIssues.length - 1)}
        severity={top.severity}
        expanded={open}
        describedById={titleId}
        triggerRef={triggerRef}
        onClick={(event) => {
          openedByKeyboard.current = event.detail === 0;
          setOpen((current) => !current);
        }}
      />
      <ZHNoticePopover
        open={open}
        onClose={() => setOpen(false)}
        anchorRef={triggerRef}
        titleId={titleId}
        title={title(visibleIssues.length)}
        severity={top.severity}
        anchored
      >
        <ZHNoticeList notices={visibleIssues} />
        {children}
        {actions.map((n) => (
          <ZHBtn
            key={n.actionLabel}
            type="button"
            variant="secondary"
            size="sm"
            className="zh-notice-popover__action"
            onClick={() => {
              n.onAction?.();
              setOpen(false);
              triggerRef.current?.focus();
            }}
          >
            {n.actionLabel}
          </ZHBtn>
        ))}
      </ZHNoticePopover>
    </span>
  );
}
