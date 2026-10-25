import { describe, expect, it, vi } from "vitest";
import { codeConflictRevision, itemCodeMutation, subscribeCodeConflicts } from "./itemCodeConflicts";

describe("itemCodeMutation", () => {
  it("refreshes code conflicts after a save rejected by a concurrent duplicate", async () => {
    const listener = vi.fn();
    const unsubscribe = subscribeCodeConflicts(listener);
    const before = codeConflictRevision();
    await expect(itemCodeMutation(Promise.reject(new Error("BARCODE_DUPLICATE")))).rejects.toThrow("BARCODE_DUPLICATE");
    expect(codeConflictRevision()).toBe(before + 1);
    expect(listener).toHaveBeenCalledOnce();
    await expect(itemCodeMutation(Promise.resolve("ok"))).resolves.toBe("ok");
    expect(codeConflictRevision()).toBe(before + 2);
    unsubscribe();
  });
});
