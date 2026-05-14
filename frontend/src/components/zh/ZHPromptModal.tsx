import { useEffect, useRef, useState } from 'react';
import { Modal } from '../Modal';
import { ZHBtn } from './ZHForm';
import { ZHActionsRow } from './ZHLayout';

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
  confirmLabel = 'Aceptar',
  cancelLabel = 'Cancelar',
  loading = false,
  onCancel,
  onConfirm,
}: ZHPromptModalProps) {
  const [value, setValue] = useState(initialValue);
  const inputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    setValue(initialValue);
  }, [initialValue]);

  useEffect(() => {
    queueMicrotask(() => inputRef.current?.focus());
  }, []);

  const trimmed = value.trim();
  const submit = () => {
    if (!trimmed || loading) return;
    void onConfirm(trimmed);
  };

  return (
    <Modal title={title} onClose={() => (loading ? undefined : onCancel())} size="sm">
      <label className="zh-field-label" htmlFor="zh-prompt-modal-input">
        {label}
      </label>
      <input
        id="zh-prompt-modal-input"
        ref={inputRef}
        className="zh-input"
        value={value}
        placeholder={placeholder}
        onChange={(e) => setValue(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            e.preventDefault();
            submit();
          }
        }}
        disabled={loading}
      />
      <ZHActionsRow className="zh-mt-16">
        <ZHBtn variant="ghost" size="md" type="button" onClick={onCancel} disabled={loading}>
          {cancelLabel}
        </ZHBtn>
        <ZHBtn variant="primary" size="md" type="button" onClick={submit} disabled={loading || !trimmed}>
          {confirmLabel}
        </ZHBtn>
      </ZHActionsRow>
    </Modal>
  );
}

