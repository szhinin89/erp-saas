import { useMessageStore } from './_internal/messageStore';
import type { ConfirmOptions, PromptOptions } from './messageTypes';

function push(message: string, type: 'success' | 'error' | 'warning' | 'info') {
  useMessageStore.getState().push(message, type);
}

export const message = {
  success: (text: string) => push(text, 'success'),
  error:   (text: string) => push(text, 'error'),
  warning: (text: string) => push(text, 'warning'),
  info:    (text: string) => push(text, 'info'),

  confirm: (options: ConfirmOptions): Promise<boolean> =>
    useMessageStore.getState().openConfirm(options),

  prompt: (options: PromptOptions): Promise<string | null> =>
    useMessageStore.getState().openPrompt(options),

  dismissAll: () => useMessageStore.getState().dismissAll(),
} as const;
