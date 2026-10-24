// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import type { NoticeVM } from "../../../notices";
import { ZHLineIssuesTag } from "./ZHLineIssuesTag";
const issue: NoticeVM = { severity: "warning", intent: "actionRequired", source: "frontend-state", label: "Review" };
const props = { label: (top: NoticeVM, count: number) => `${top.label} +${count}`, title: (count: number) => `Issues (${count})` };
beforeEach(() => {
  vi.stubGlobal("requestAnimationFrame", (fn: FrameRequestCallback) => { fn(0); return 1; });
  vi.stubGlobal("cancelAnimationFrame", vi.fn());
  vi.stubGlobal("CSS", { escape: (value: string) => value });
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
it("renders nothing for no issues or success only", () => {
  const { container, rerender } = render(<ZHLineIssuesTag {...props} issues={[]} />);
  expect(container.textContent).toBe("");
  rerender(<ZHLineIssuesTag {...props} issues={[{ ...issue, severity: "success" }]} />);
  expect(container.textContent).toBe("");
});
it("preserves consumer priority, excludes success from the counter and list", () => {
  render(<ZHLineIssuesTag {...props} issues={[issue, { ...issue, label: "Other", severity: "danger" }, { ...issue, severity: "success" }]} />);
  fireEvent.click(screen.getByRole("button", { name: "Review +1" }));
  expect(screen.getByRole("dialog", { name: "Issues (2)" })).toBeTruthy();
  expect(screen.getAllByRole("listitem")).toHaveLength(2);
});
it("keyboard activation focuses the action; correction closes and returns focus", () => {
  const onAction = vi.fn();
  render(<ZHLineIssuesTag {...props} issues={[{ ...issue, actionLabel: "Correct", onAction }]} />);
  const trigger = screen.getByRole("button", { name: "Review +0" });
  fireEvent.click(trigger, { detail: 0 });
  const action = screen.getByRole("button", { name: "Correct" });
  expect(document.activeElement).toBe(action);
  fireEvent.click(action);
  expect(onAction).toHaveBeenCalledOnce();
  expect(screen.queryByRole("dialog")).toBeNull();
  expect(document.activeElement).toBe(trigger);
});
