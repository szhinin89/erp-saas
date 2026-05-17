import { useEffect, useRef, useState } from 'react';
import { useI18n } from '../../i18n/i18n';
import { ZHBtn } from './ZHForm';

type ZHPromptModalProps = {
  title: string;
  label: string;
  initialValue?: string;
  placeholder?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  loading?: boolean;
  onCancel: () => void;
  onConfirm: (value: string) => void | Promise<void>;
};

export function ZHPromptModal({
  title,
  label,
  initialValue = '',
  placeholder,
  confirmLabel,
  cancelLabel,
  loading = false,
  onCancel,
  onConfirm,
}: ZHPromptModalProps) {
  const { t } = useI18n();
  const [value, setValue] = useState(initialValue);
  const inputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => { setValue(initialValue); }, [initialValue]);

  useEffect(() => {
    queueMicrotask(() => {
      inputRef.current?.focus();
      inputRef.current?.select();
    });
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape' && !loading) onCancel(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onCancel, loading]);

  const trimmed = value.trim();

  const submit = () => {
    if (!trimmed || loading) return;
    void onConfirm(trimmed);
  };

  return (
    <div
      className="zh-modal-overlay"
      role="dialog"
      aria-modal="true"
      aria-labelledby="zh-prompt-title"
      onClick={(e) => { if (e.target === e.currentTarget && !loading) onCancel(); }}
    >
      <div className="zh-modal" style={{ maxWidth: 'min(480px, 95vw)', width: '100%' }}>

        <div className="zh-modal-header">
          <h2 className="zh-modal-title" id="zh-prompt-title">{title}</h2>
          <button
            type="button"
            className="zh-modal-close"
            onClick={onCancel}
            disabled={loading}
            aria-label={t('common.close')}
          >✕</button>
        </div>

        <div className="zh-modal-body">
          <label
            htmlFor="zh-prompt-input"
            style={{ display: 'block', fontSize: 'var(--text-label-sm-size)', fontWeight: 600,
              color: 'var(--color-text-secondary)', marginBottom: 'var(--space-2)',
              textTransform: 'uppercase', letterSpacing: '0.04em' }}
          >
            {label}
          </label>
          <input
            id="zh-prompt-input"
            ref={inputRef}
            className="zh-input"
            value={value}
            placeholder={placeholder}
            onChange={(e) => setValue(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); submit(); } }}
            disabled={loading}
            style={{ width: '100%' }}
          />
        </div>

        <div className="pg-actions-bar" style={{ borderTop: '1px solid var(--color-border)', padding: 'var(--space-3) var(--space-5)' }}>
          <div className="pg-actions-info" />
          <div className="pg-actions-buttons">
            <ZHBtn variant="ghost" size="md" type="button" disabled={loading} onClick={onCancel}>
              {cancelLabel ?? t('common.cancel')}
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="button" disabled={loading || !trimmed} onClick={submit}>
              {confirmLabel ?? t('common.confirm')}
            </ZHBtn>
          </div>
        </div>

      </div>
    </div>
  );
}
