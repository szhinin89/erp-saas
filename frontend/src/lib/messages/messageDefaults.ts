import type { MessageType } from './messageTypes';

export type DuplicatePolicy = 'ignore' | 'reset-timer' | 'replace';
export type ToastPosition = 'top-right' | 'top-center' | 'bottom-right';

export const MESSAGE_CONFIG = {
  autoCloseDuration: 4000,
  maxVisible: 3,
  duplicatePolicy: 'reset-timer' as DuplicatePolicy,
  position: 'top-right' as ToastPosition,
  icons: {
    success: 'check_circle',
    error: 'error',
    warning: 'warning',
    info: 'info',
  } satisfies Record<MessageType, string>,
};
