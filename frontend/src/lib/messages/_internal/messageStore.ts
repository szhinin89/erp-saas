import { create } from "zustand";
import type {
  MessageItem,
  MessageType,
  ConfirmOptions,
  PromptOptions,
} from "../messageTypes";
import { MESSAGE_CONFIG } from "../messageDefaults";

interface ConfirmState {
  open: boolean;
  options: ConfirmOptions;
  onConfirm: () => void;
  onCancel: () => void;
}

interface PromptState {
  open: boolean;
  options: PromptOptions;
  onConfirm: (value: string) => void;
  onCancel: () => void;
}

interface MessageStoreState {
  queue: MessageItem[];
  confirm: ConfirmState | null;
  prompt: PromptState | null;

  push: (message: string, type: MessageType, persistent?: boolean) => void;
  dismiss: (id: string) => void;
  dismissAll: () => void;

  openConfirm: (options: ConfirmOptions) => Promise<boolean>;
  openPrompt: (options: PromptOptions) => Promise<string | null>;
}

let idCounter = 0;
function nextId() {
  return `msg_${Date.now()}_${++idCounter}`;
}

export const useMessageStore = create<MessageStoreState>((set, get) => ({
  queue: [],
  confirm: null,
  prompt: null,

  push: (message, type, persistent = false) => {
    const { queue } = get();

    if (MESSAGE_CONFIG.duplicatePolicy === "ignore") {
      const dup = queue.find((m) => m.message === message && m.type === type);
      if (dup) return;
    }

    if (MESSAGE_CONFIG.duplicatePolicy === "reset-timer") {
      const dupIdx = queue.findIndex(
        (m) => m.message === message && m.type === type,
      );
      if (dupIdx >= 0) {
        const updated = [...queue];
        updated[dupIdx] = { ...updated[dupIdx], createdAt: Date.now() };
        set({ queue: updated });
        return;
      }
    }

    const item: MessageItem = {
      id: nextId(),
      type,
      message,
      persistent,
      createdAt: Date.now(),
    };

    const next = [...queue, item];
    if (next.length > MESSAGE_CONFIG.maxVisible) {
      next.shift();
    }
    set({ queue: next });
  },

  dismiss: (id) => {
    set((s) => ({ queue: s.queue.filter((m) => m.id !== id) }));
  },

  dismissAll: () => set({ queue: [] }),

  openConfirm: (options) =>
    new Promise<boolean>((resolve) => {
      set({
        confirm: {
          open: true,
          options,
          onConfirm: () => {
            set({ confirm: null });
            resolve(true);
          },
          onCancel: () => {
            set({ confirm: null });
            resolve(false);
          },
        },
      });
    }),

  openPrompt: (options) =>
    new Promise<string | null>((resolve) => {
      set({
        prompt: {
          open: true,
          options,
          onConfirm: (value) => {
            set({ prompt: null });
            resolve(value);
          },
          onCancel: () => {
            set({ prompt: null });
            resolve(null);
          },
        },
      });
    }),
}));
