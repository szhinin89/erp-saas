import { useEffect } from 'react';
import './Modal.css';
import { useI18n } from '../i18n/i18n';

interface Props {
  title?: string;
  onClose: () => void;
  children: React.ReactNode;
  width?: number;
  size?: 'sm' | 'md' | 'lg';
  header?: React.ReactNode;
}

export function Modal({ title, onClose, children, width, size = 'md', header }: Props) {
  const { t } = useI18n();
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose]);

  const sizeCls = `modal-card--${size}`;
  const widthCls =
    typeof width === 'number'
      ? width <= 420 ? 'modal-card--w420' :
        width <= 480 ? 'modal-card--w480' :
        width <= 560 ? 'modal-card--w560' :
        width <= 680 ? 'modal-card--w680' :
        'modal-card--w820'
      : '';

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className={['modal-card', sizeCls, widthCls].filter(Boolean).join(' ')}
        onClick={(e) => e.stopPropagation()}
      >
        {header ? (
          header
        ) : (
          <div className="modal-header">
            <h2 className="modal-title">{title ?? ''}</h2>
            <button className="modal-close" onClick={onClose} type="button" aria-label={t('common.close')}>✕</button>
          </div>
        )}
        <div className="modal-body">{children}</div>
      </div>
    </div>
  );
}
