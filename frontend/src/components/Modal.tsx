import { useEffect } from 'react';
import { useI18n } from '../i18n/i18n';

interface Props {
  title?: string;
  onClose: () => void;
  children: React.ReactNode;
  width?: number;
  size?: 'sm' | 'md' | 'lg';
  header?: React.ReactNode;
}

const SIZE_MAX: Record<string, number> = { sm: 480, md: 640, lg: 900 };

export function Modal({ title, onClose, children, width, size = 'md', header }: Props) {
  const { t } = useI18n();

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose]);

  const maxWidth = typeof width === 'number' ? width : (SIZE_MAX[size] ?? 640);

  return (
    <div
      className="zh-modal-overlay"
      role="dialog"
      aria-modal="true"
      onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div
        className="zh-modal"
        style={{ maxWidth: `min(${maxWidth}px, 95vw)`, width: '100%' }}
        onClick={(e) => e.stopPropagation()}
      >
        {header ?? (
          <div className="zh-modal-header">
            <h2 className="zh-modal-title">{title ?? ''}</h2>
            <button
              className="zh-modal-close"
              onClick={onClose}
              type="button"
              aria-label={t('common.close')}
            >✕</button>
          </div>
        )}
        <div className="zh-modal-body">{children}</div>
      </div>
    </div>
  );
}
