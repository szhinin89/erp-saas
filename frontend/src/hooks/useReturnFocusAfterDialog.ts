import { useCallback, useEffect, useRef } from "react";

const DIALOG_SELECTOR = '[aria-modal="true"]';
const WAIT_FOR_DIALOG_MS = 10_000;

/** Row-local focus return: after a menu action opens a modal (ZHModal / message.confirm),
 * focus goes back to `getTarget()` once that modal closes and focus was left on <body>.
 * Does not touch the shared modal infrastructure; stops if no modal appears in time. */
export function useReturnFocusAfterDialog(getTarget: () => HTMLElement | null | undefined) {
  const stopRef = useRef<(() => void) | null>(null);
  const targetRef = useRef(getTarget);
  useEffect(() => { targetRef.current = getTarget; }, [getTarget]);
  useEffect(() => () => stopRef.current?.(), []);

  return useCallback(() => {
    stopRef.current?.();
    let sawDialog = false;
    const observer = new MutationObserver(() => {
      if (document.querySelector(DIALOG_SELECTOR)) { sawDialog = true; return; }
      if (!sawDialog) return;
      stop();
      const active = document.activeElement;
      if (active && active !== document.body && active.isConnected) return;
      const target = targetRef.current();
      if (target?.isConnected) target.focus();
    });
    const timer = window.setTimeout(() => { if (!sawDialog) stop(); }, WAIT_FOR_DIALOG_MS);
    function stop() {
      observer.disconnect();
      window.clearTimeout(timer);
      if (stopRef.current === stop) stopRef.current = null;
    }
    observer.observe(document.body, { childList: true, subtree: true });
    stopRef.current = stop;
  }, []);
}
