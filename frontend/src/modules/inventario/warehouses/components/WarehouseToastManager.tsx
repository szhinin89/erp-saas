import { useEffect } from 'react';
import { useWarehouseUiStore } from '../store/warehouseUiStore';

export function WarehouseToastManager() {
  const toast   = useWarehouseUiStore((s) => s.toast);
  const dismiss = useWarehouseUiStore((s) => s.dismissToast);

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(dismiss, 3000);
    return () => clearTimeout(t);
  }, [toast, dismiss]);

  if (!toast) return null;

  const icons: Record<string, string> = {
    success: 'check_circle',
    error:   'error',
    info:    'info',
  };

  return (
    <div className={`prd-toast prd-toast--${toast.type}`} role="alert" aria-live="assertive">
      <span className="material-symbols-outlined prd-toast__icon">{icons[toast.type]}</span>
      <span className="prd-toast__msg">{toast.message}</span>
      <button
        type="button"
        className="prd-toast__close"
        onClick={dismiss}
        aria-label="Cerrar notificación"
      >
        <span className="material-symbols-outlined" style={{ fontSize: 16 }}>close</span>
      </button>
    </div>
  );
}
