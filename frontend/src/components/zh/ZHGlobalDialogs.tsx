import { useMessageStore } from "../../lib/messages/_internal/messageStore";
import { ZHConfirmModal, ZHPromptModal } from "./ZHConfirmModal";

export function ZHGlobalDialogs() {
  const confirm = useMessageStore((s) => s.confirm);
  const prompt = useMessageStore((s) => s.prompt);

  return (
    <>
      {confirm && (
        <ZHConfirmModal
          open={confirm.open}
          title={confirm.options.title}
          message={confirm.options.message}
          confirmLabel={confirm.options.confirmLabel}
          cancelLabel={confirm.options.cancelLabel}
          variant={confirm.options.variant}
          onConfirm={confirm.onConfirm}
          onCancel={confirm.onCancel}
        />
      )}
      {prompt && (
        <ZHPromptModal
          open={prompt.open}
          title={prompt.options.title}
          message={
            typeof prompt.options.message === "string"
              ? prompt.options.message
              : undefined
          }
          label={prompt.options.label}
          placeholder={prompt.options.placeholder}
          type={prompt.options.inputType}
          defaultValue={prompt.options.defaultValue}
          required={prompt.options.required}
          confirmLabel={prompt.options.confirmLabel}
          cancelLabel={prompt.options.cancelLabel}
          variant={prompt.options.variant}
          onConfirm={prompt.onConfirm}
          onCancel={prompt.onCancel}
        />
      )}
    </>
  );
}
