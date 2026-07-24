import { useState, type ReactNode } from 'react';
import { ZHBtn, ZHField } from './ZHForm';
import { ZhDecimalInput } from './inputs/ZhDecimalInput';

interface ConfirmProps {
  open: boolean;
  title: string;
  message: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: 'danger' | 'warning' | 'default';
  onConfirm: () => void;
  onCancel: () => void;
}

const VARIANT_ICON: Record<string, string> = {
  danger: 'warning',
  warning: 'info',
  default: 'help',
};

const VARIANT_COLOR: Record<string, string> = {
  danger: 'var(--color-error)',
  warning: 'var(--color-warning)',
  default: 'var(--color-primary)',
};

export function ZHConfirmModal({
  open, title, message,
  confirmLabel = 'Confirmar',
  cancelLabel = 'Cancelar',
  variant = 'default',
  onConfirm, onCancel,
}: ConfirmProps) {
  if (!open) return null;
  return (
    <div className="zh-modal-overlay"
      onClick={(e) => { if (e.target === e.currentTarget) onCancel(); }}>
      <div className="zh-modal zh-modal--sm zh-confirm-dialog" role="alertdialog" aria-modal="true">
        <div className="zh-confirm-header">
          <span className="material-symbols-outlined zh-confirm-icon" style={{ color: VARIANT_COLOR[variant] }}>
            {VARIANT_ICON[variant]}
          </span>
          <h3 className="zh-confirm-title">{title}</h3>
        </div>
        <div className="zh-confirm-body">
          {typeof message === 'string' ? <p className="zh-confirm-message">{message}</p> : message}
        </div>
        <div className="zh-modal-footer">
          <ZHBtn type="button" variant="ghost" size="md" onClick={onCancel}>{cancelLabel}</ZHBtn>
          <ZHBtn type="button" variant={variant === 'danger' ? 'destructive' : 'primary'} size="md"
            onClick={onConfirm}>{confirmLabel}</ZHBtn>
        </div>
      </div>
    </div>
  );
}

interface PromptProps {
  open: boolean;
  title: string;
  message?: string;
  label: string;
  placeholder?: string;
  type?: 'text' | 'number' | 'date' | 'datetime-local' | 'decimal';
  /** Solo aplica con type="decimal" — cantidad de decimales permitidos (ej. getDecimalConfig().percentage). */
  decimals?: number;
  positiveOnly?: boolean;
  defaultValue?: string;
  required?: boolean;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: 'danger' | 'warning' | 'default';
  onConfirm: (value: string) => void;
  onCancel: () => void;
}

export function ZHPromptModal({
  open, title, message, label, placeholder, type = 'text', decimals, positiveOnly, defaultValue = '',
  required = true, confirmLabel = 'Aceptar', cancelLabel = 'Cancelar',
  variant = 'default', onConfirm, onCancel,
}: PromptProps) {
  const [value, setValue] = useState(defaultValue);
  if (!open) return null;
  return (
    <div className="zh-modal-overlay"
      onClick={(e) => { if (e.target === e.currentTarget) onCancel(); }}>
      <div className="zh-modal zh-modal--sm zh-confirm-dialog" role="alertdialog" aria-modal="true">
        <div className="zh-confirm-header">
          <span className="material-symbols-outlined zh-confirm-icon" style={{ color: VARIANT_COLOR[variant] }}>
            {variant === 'danger' ? 'warning' : 'edit'}
          </span>
          <h3 className="zh-confirm-title">{title}</h3>
        </div>
        <div className="zh-confirm-body">
          {message && <p className="zh-confirm-message">{message}</p>}
          <ZHField density="compact" label={label}>
            {type === 'decimal' ? (
              <ZhDecimalInput decimals={decimals} positiveOnly={positiveOnly}
                value={value} onChange={e => setValue(e.target.value)}
                placeholder={placeholder} autoFocus />
            ) : (
              <input type={type} value={value} onChange={e => setValue(e.target.value)}
                placeholder={placeholder} autoFocus />
            )}
          </ZHField>
        </div>
        <div className="zh-modal-footer">
          <ZHBtn type="button" variant="ghost" size="md" onClick={onCancel}>{cancelLabel}</ZHBtn>
          <ZHBtn type="button" variant={variant === 'danger' ? 'destructive' : 'primary'} size="md"
            disabled={required && !value.trim()} onClick={() => onConfirm(value.trim())}>{confirmLabel}</ZHBtn>
        </div>
      </div>
    </div>
  );
}
