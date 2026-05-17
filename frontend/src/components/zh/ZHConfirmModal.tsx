import { useEffect, type ReactNode } from 'react';
import { useI18n } from '../../i18n/i18n';
import { ZHBtn } from './ZHForm';

export function ZHConfirmModal(props: {
  title: string;
  message: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: 'destructive' | 'primary';
  loading?: boolean;
  onConfirm: () => void | Promise<void>;
  onCancel: () => void;
}) {
  const { t } = useI18n();
  const {
    title,
    message,
    confirmLabel,
    cancelLabel,
    variant = 'destructive',
    loading,
    onConfirm,
    onCancel,
  } = props;

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape' && !loading) onCancel(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onCancel, loading]);

  return (
    <div
      className="zh-modal-overlay"
      role="dialog"
      aria-modal="true"
      aria-labelledby="zh-confirm-title"
      onClick={(e) => { if (e.target === e.currentTarget && !loading) onCancel(); }}
    >
      <div className="zh-modal" style={{ maxWidth: 420, width: '100%' }}>

        <div className="zh-modal-header">
          <h2 className="zh-modal-title" id="zh-confirm-title">{title}</h2>
          <button
            type="button"
            className="zh-modal-close"
            onClick={onCancel}
            disabled={loading}
            aria-label={t('common.close')}
          >✕</button>
        </div>

        <div className="zh-modal-body">
          <div style={{ display: 'flex', gap: 'var(--space-4)', alignItems: 'flex-start' }}>
            {variant === 'destructive' && (
              <div style={{
                width: 36, height: 36, borderRadius: '50%', flexShrink: 0,
                background: 'var(--color-error-surface, #fdecea)',
                color: 'var(--color-error)',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontWeight: 700, fontSize: 18,
              }} aria-hidden="true">!</div>
            )}
            <div style={{ fontSize: 'var(--text-body-md-size)', color: 'var(--color-text-primary)', paddingTop: 6 }}>
              {message}
            </div>
          </div>
        </div>

        <div className="pg-actions-bar" style={{ borderTop: '1px solid var(--color-border)', padding: 'var(--space-3) var(--space-5)' }}>
          <div className="pg-actions-info" />
          <div className="pg-actions-buttons">
            <ZHBtn variant="ghost" size="md" type="button" disabled={loading} onClick={onCancel}>
              {cancelLabel ?? t('common.cancel')}
            </ZHBtn>
            <ZHBtn
              variant={variant === 'destructive' ? 'destructive' : 'primary'}
              size="md"
              type="button"
              disabled={loading}
              onClick={() => void onConfirm()}
            >
              {confirmLabel ?? t('common.confirm')}
            </ZHBtn>
          </div>
        </div>

      </div>
    </div>
  );
}
