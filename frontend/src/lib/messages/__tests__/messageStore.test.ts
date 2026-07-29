import { describe, it, expect, beforeEach } from "vitest";
import { useMessageStore } from "../_internal/messageStore";
import { MESSAGE_CONFIG } from "../messageDefaults";

function store() {
  return useMessageStore.getState();
}

function resetStore() {
  useMessageStore.setState({ queue: [], confirm: null, prompt: null });
}

describe("messageStore — queue", () => {
  beforeEach(resetStore);

  it("push adds a message to the queue", () => {
    store().push("Hello", "success");
    expect(store().queue).toHaveLength(1);
    expect(store().queue[0].message).toBe("Hello");
    expect(store().queue[0].type).toBe("success");
  });

  it("push assigns unique ids", () => {
    store().push("A", "success");
    store().push("B", "error");
    const ids = store().queue.map((m) => m.id);
    expect(new Set(ids).size).toBe(2);
  });

  it("FIFO: oldest message is removed when queue exceeds maxVisible", () => {
    for (let i = 0; i < MESSAGE_CONFIG.maxVisible + 2; i++) {
      store().push(`msg-${i}`, "info");
    }
    expect(store().queue).toHaveLength(MESSAGE_CONFIG.maxVisible);
    expect(store().queue[0].message).toBe(`msg-2`);
    expect(store().queue[MESSAGE_CONFIG.maxVisible - 1].message).toBe(
      `msg-${MESSAGE_CONFIG.maxVisible + 1}`,
    );
  });

  it("dismiss removes a specific message by id", () => {
    store().push("A", "success");
    store().push("B", "error");
    const idToRemove = store().queue[0].id;
    store().dismiss(idToRemove);
    expect(store().queue).toHaveLength(1);
    expect(store().queue[0].message).toBe("B");
  });

  it("dismissAll clears the queue", () => {
    store().push("A", "success");
    store().push("B", "error");
    store().dismissAll();
    expect(store().queue).toHaveLength(0);
  });
});

describe("messageStore — deduplication (reset-timer)", () => {
  beforeEach(resetStore);

  it("duplicate message resets createdAt instead of adding a new entry", () => {
    store().push("Dup", "success");
    const original = store().queue[0].createdAt;

    // small delay to ensure Date.now() differs
    const later = original + 10;
    const origDateNow = Date.now;
    Date.now = () => later;

    store().push("Dup", "success");

    Date.now = origDateNow;

    expect(store().queue).toHaveLength(1);
    expect(store().queue[0].createdAt).toBe(later);
  });

  it("different type is not treated as duplicate", () => {
    store().push("Same text", "success");
    store().push("Same text", "error");
    expect(store().queue).toHaveLength(2);
  });

  it("different message text is not treated as duplicate", () => {
    store().push("A", "success");
    store().push("B", "success");
    expect(store().queue).toHaveLength(2);
  });
});

describe("messageStore — confirm", () => {
  beforeEach(resetStore);

  it("openConfirm sets confirm state", () => {
    const promise = store().openConfirm({ title: "T", message: "M" });
    expect(store().confirm).not.toBeNull();
    expect(store().confirm!.open).toBe(true);
    expect(store().confirm!.options.title).toBe("T");

    store().confirm!.onCancel();
    return promise.then((result) => expect(result).toBe(false));
  });

  it("onConfirm resolves with true", () => {
    const promise = store().openConfirm({ title: "T", message: "M" });
    store().confirm!.onConfirm();
    return promise.then((result) => {
      expect(result).toBe(true);
      expect(store().confirm).toBeNull();
    });
  });

  it("onCancel resolves with false", () => {
    const promise = store().openConfirm({ title: "T", message: "M" });
    store().confirm!.onCancel();
    return promise.then((result) => {
      expect(result).toBe(false);
      expect(store().confirm).toBeNull();
    });
  });
});

describe("messageStore — prompt", () => {
  beforeEach(resetStore);

  it("openPrompt sets prompt state", () => {
    const promise = store().openPrompt({
      title: "T",
      message: "M",
      label: "L",
    });
    expect(store().prompt).not.toBeNull();
    expect(store().prompt!.open).toBe(true);

    store().prompt!.onCancel();
    return promise.then((result) => expect(result).toBeNull());
  });

  it("onConfirm resolves with the entered value", () => {
    const promise = store().openPrompt({
      title: "T",
      message: "M",
      label: "L",
    });
    store().prompt!.onConfirm("user-input");
    return promise.then((result) => {
      expect(result).toBe("user-input");
      expect(store().prompt).toBeNull();
    });
  });

  it("onCancel resolves with null", () => {
    const promise = store().openPrompt({
      title: "T",
      message: "M",
      label: "L",
    });
    store().prompt!.onCancel();
    return promise.then((result) => {
      expect(result).toBeNull();
      expect(store().prompt).toBeNull();
    });
  });
});

describe("messageStore — stress", () => {
  beforeEach(resetStore);

  it("handles rapid fire of 100 messages without exceeding maxVisible", () => {
    for (let i = 0; i < 100; i++) {
      store().push(`stress-${i}`, "info");
    }
    expect(store().queue).toHaveLength(MESSAGE_CONFIG.maxVisible);
    expect(store().queue[0].message).toBe(
      `stress-${100 - MESSAGE_CONFIG.maxVisible}`,
    );
  });

  it("handles alternating push and dismiss without corruption", () => {
    for (let i = 0; i < 50; i++) {
      store().push(`alt-${i}`, "success");
      if (store().queue.length > 1) {
        store().dismiss(store().queue[0].id);
      }
    }
    expect(store().queue.length).toBeGreaterThanOrEqual(1);
    expect(store().queue.length).toBeLessThanOrEqual(MESSAGE_CONFIG.maxVisible);
  });

  it("handles 50 duplicate pushes without growing the queue", () => {
    store().push("same", "success");
    for (let i = 0; i < 50; i++) {
      store().push("same", "success");
    }
    expect(store().queue).toHaveLength(1);
  });
});

describe("messageService — public API", () => {
  beforeEach(resetStore);

  it("message.success pushes a success toast", async () => {
    const { message } = await import("../messageService");
    message.success("ok");
    expect(store().queue).toHaveLength(1);
    expect(store().queue[0].type).toBe("success");
  });

  it("message.error pushes an error toast", async () => {
    const { message } = await import("../messageService");
    message.error("fail");
    expect(store().queue[0].type).toBe("error");
  });

  it("message.warning pushes a warning toast", async () => {
    const { message } = await import("../messageService");
    message.warning("warn");
    expect(store().queue[0].type).toBe("warning");
  });

  it("message.info pushes an info toast", async () => {
    const { message } = await import("../messageService");
    message.info("fyi");
    expect(store().queue[0].type).toBe("info");
  });

  it("message.dismissAll clears everything", async () => {
    const { message } = await import("../messageService");
    message.success("a");
    message.error("b");
    message.dismissAll();
    expect(store().queue).toHaveLength(0);
  });
});
