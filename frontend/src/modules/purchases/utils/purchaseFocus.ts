// Priority order: a line's issue tag, then an invalid control, then the first usable control.
const FOCUS_TARGETS = [
  ".zh-line-issues button",
  '[aria-invalid="true"]:not(:disabled)',
  "input:not(:disabled), select:not(:disabled), button:not(:disabled)",
];

/** Navigation only: validation remains owned by the purchase form and its existing rules. */
export function focusPurchaseTarget(id: string) {
  const section = document.getElementById(id);
  if (!section) return;
  section.scrollIntoView?.({ block: "center" });
  const control = FOCUS_TARGETS.map(selector => section.querySelector<HTMLElement>(selector)).find(Boolean);
  (control ?? section).focus();
}
