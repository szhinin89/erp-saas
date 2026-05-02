import { Modal } from '../Modal';
import { useI18n } from '../../i18n/i18n';
import { ZHBtn } from './ZHForm';
import { ZHActionsRow } from './ZHLayout';

export function ZHConfirmModal(props: {
  title: string;
  message: string;
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

  return (
    <Modal title={title} onClose={onCancel} size="sm">
      <div className="zh-text-muted">{message}</div>
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

