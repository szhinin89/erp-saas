import type { ReactNode } from 'react';
import { Modal } from '../Modal';
import { useI18n } from '../../i18n/i18n';
import { ZHBtn } from './ZHForm';
import { ZHActionsRow } from './ZHLayout';
import './ZHConfirmModal.css';

export function ZHConfirmModal(props: {
  title: string;
  /** Texto o fragmento (p. ej. nombre del registro en negrita). */
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

  const destructive = variant === 'destructive';

  return (
    <Modal title={title} onClose={() => (loading ? undefined : onCancel())} size="sm">
      <div className={destructive ? 'zh-confirm-modal zh-confirm-modal--destructive' : 'zh-confirm-modal'}>
        {destructive ? (
          <div className="zh-confirm-modal__icon" aria-hidden>
            !
          </div>
        ) : null}
        <div className="zh-confirm-modal__body">
          <div className="zh-confirm-modal__message">{message}</div>
        </div>
      </div>
      <ZHActionsRow className="zh-mt-16">
        <ZHBtn variant="ghost" size="md" type="button" onClick={onCancel} disabled={loading}>
          {cancelLabel ?? t('common.cancel')}
        </ZHBtn>
        <ZHBtn
          variant={variant === 'destructive' ? 'destructive' : 'primary'}
          size="md"
          type="button"
          onClick={() => void onConfirm()}
          disabled={loading}
        >
          {confirmLabel ?? t('common.confirm')}
        </ZHBtn>
      </ZHActionsRow>
    </Modal>
  );
}

