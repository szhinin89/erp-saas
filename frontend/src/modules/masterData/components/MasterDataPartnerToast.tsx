import { useEffect } from 'react';
import type { StoreApi, UseBoundStore } from 'zustand';
import type { PartnerUiState } from '../store/masterDataPartnerUiStore';

type UiStore = UseBoundStore<StoreApi<PartnerUiState>>;

export function MasterDataPartnerToast({ store }: { store: UiStore }) {
  const toast = store((s) => s.toast);
  const dismiss = store((s) => s.dismissToast);

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(dismiss, 3000);
    return () => clearTimeout(t);
  }, [toast, dismiss]);

  if (!toast) return null;

  const icons: Record<string, string> = {
    success: 'check_circle',
    error: 'error',
    info: 'info',
  };

  return (
    <div
      className={`prd-toast prd-toast--${toast.type}`}
      role="alert"
      aria-live="assertive"
    >
      <span className="material-symbols-outlined prd-toast__icon">
        {icons[toast.type]}
      </span>
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
