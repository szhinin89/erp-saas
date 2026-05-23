import type { ReactNode } from 'react';

interface Props {
  open: boolean;
  title: string;
  message: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: 'danger' | 'default';
  onConfirm: () => void;
  onCancel: () => void;
}

export function WarehouseConfirmModal({
  open, title, message,
  confirmLabel = 'Confirmar',
  cancelLabel  = 'Cancelar',
  variant = 'default',
  onConfirm, onCancel,
}: Props) {
  if (!open) return null;
  return (
    <div
      className="prd-modal-backdrop"
      role="dialog"
      aria-modal="true"
      aria-labelledby="wh-confirm-title"
      onClick={(e) => { if (e.target === e.currentTarget) onCancel(); }}
    >
      <div className="prd-modal">
        <div className={`prd-modal__header prd-modal__header--${variant}`}>
          <span className="material-symbols-outlined prd-modal__icon">
            {variant === 'danger' ? 'warning' : 'help'}
          </span>
          <h3 id="wh-confirm-title" className="prd-modal__title">{title}</h3>
        </div>
        <div className="prd-modal__body">
          <p className="prd-modal__message">{message}</p>
        </div>
        <div className="prd-modal__footer">
          <button type="button" className="zh-btn zh-btn--ghost zh-btn--md" onClick={onCancel}>
            {cancelLabel}
          </button>
          <button
            type="button"
            className={`zh-btn zh-btn--md ${variant === 'danger' ? 'prd-btn--danger' : 'zh-btn--primary'}`}
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
