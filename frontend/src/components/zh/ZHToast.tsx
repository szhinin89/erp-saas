import { useEffect, useCallback } from "react";
import { useMessageStore } from "../../lib/messages/_internal/messageStore";
import { MESSAGE_CONFIG } from "../../lib/messages/messageDefaults";

export function ZHToast() {
  const queue = useMessageStore((s) => s.queue);
  const dismiss = useMessageStore((s) => s.dismiss);

  return (
    <div className="zh-toast-container" aria-live="assertive">
      {queue.map((item) => (
        <ZHToastItem
          key={item.id}
          id={item.id}
          type={item.type}
          message={item.message}
          persistent={item.persistent}
          createdAt={item.createdAt}
          onDismiss={dismiss}
        />
      ))}
    </div>
  );
}

function ZHToastItem(props: {
  id: string;
  type: string;
  message: string;
  persistent: boolean;
  createdAt: number;
  onDismiss: (id: string) => void;
}) {
  const { id, type, message, persistent, onDismiss } = props;

  const handleDismiss = useCallback(() => onDismiss(id), [id, onDismiss]);

  useEffect(() => {
    if (persistent) return;
    const t = setTimeout(handleDismiss, MESSAGE_CONFIG.autoCloseDuration);
    return () => clearTimeout(t);
  }, [persistent, handleDismiss, props.createdAt]);

  return (
    <div className={`zh-toast zh-toast--${type}`} role="alert">
      <span className="material-symbols-outlined zh-toast__icon">
        {MESSAGE_CONFIG.icons[type as keyof typeof MESSAGE_CONFIG.icons]}
      </span>
      <span className="zh-toast__msg">{message}</span>
      <button
        type="button"
        className="zh-toast__close"
        onClick={handleDismiss}
        aria-label="Cerrar notificación"
      >
        <span className="material-symbols-outlined zh-icon-md">close</span>
      </button>
    </div>
  );
}
