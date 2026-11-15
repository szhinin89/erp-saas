import { useEffect, useId, useRef, useState, type ReactNode } from "react";
import { ZHBtn, ZHField } from "./ZHForm";
import { ZhDecimalInput } from "./inputs/ZhDecimalInput";
import type { PrecisionKind } from "../../lib/config/precisionPolicy.config";

interface ConfirmProps {
  open: boolean;
  title: string;
  message: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: "danger" | "warning" | "default";
  onConfirm: () => void;
  onCancel: () => void;
}

const VARIANT_ICON: Record<string, string> = {
  danger: "warning",
  warning: "info",
  default: "help",
};


export function ZHConfirmModal({
  open,
  title,
  message,
  confirmLabel = "Confirmar",
  cancelLabel = "Cancelar",
  variant = "default",
  onConfirm,
  onCancel,
}: ConfirmProps) {
  const titleId = useId();
  const messageId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const cancelRef = useRef(onCancel);
  useEffect(() => {
    cancelRef.current = onCancel;
  }, [onCancel]);

  // Accesibilidad del diálogo de alerta: foco inicial en la acción menos destructiva («Cancelar»),
  // Escape cancela, Tab/Shift+Tab no salen del diálogo y al cerrar el foco vuelve a quien lo abrió.
  useEffect(() => {
    if (!open) return;
    const opener = document.activeElement as HTMLElement | null;
    const focusables = () =>
      Array.from(
        dialogRef.current?.querySelectorAll<HTMLElement>(
          'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
        ) ?? [],
      );
    focusables()[0]?.focus();
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.stopPropagation();
        cancelRef.current();
        return;
      }
      if (e.key !== "Tab") return;
      const items = focusables();
      if (items.length === 0) return;
      const first = items[0];
      const last = items[items.length - 1];
      const active = document.activeElement;
      const inside = !!dialogRef.current?.contains(active);
      if (e.shiftKey && (active === first || !inside)) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && (active === last || !inside)) {
        e.preventDefault();
        first.focus();
      }
    };
    document.addEventListener("keydown", onKeyDown, true);
    return () => {
      document.removeEventListener("keydown", onKeyDown, true);
      if (opener?.isConnected) opener.focus();
    };
  }, [open]);

  if (!open) return null;
  return (
    <div
      className="zh-modal-overlay"
      onClick={(e) => {
        if (e.target === e.currentTarget) onCancel();
      }}
    >
      <div
        ref={dialogRef}
        className="zh-modal zh-modal--sm zh-confirm-dialog"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={messageId}
      >
        <div className="zh-confirm-header">
          <span
            className={`material-symbols-outlined zh-confirm-icon zh-confirm-icon--${variant}`}
            aria-hidden="true"
          >
            {VARIANT_ICON[variant]}
          </span>
          <h3 className="zh-confirm-title" id={titleId}>
            {title}
          </h3>
        </div>
        <div className="zh-confirm-body" id={messageId}>
          {typeof message === "string" ? (
            <p className="zh-confirm-message">{message}</p>
          ) : (
            message
          )}
        </div>
        <div className="zh-modal-footer">
          <ZHBtn type="button" variant="ghost" size="md" onClick={onCancel}>
            {cancelLabel}
          </ZHBtn>
          <ZHBtn
            type="button"
            variant={variant === "danger" ? "destructive" : "primary"}
            size="md"
            onClick={onConfirm}
          >
            {confirmLabel}
          </ZHBtn>
        </div>
      </div>
    </div>
  );
}

interface PromptBaseProps {
  open: boolean;
  title: string;
  message?: string;
  label: string;
  placeholder?: string;
  positiveOnly?: boolean;
  defaultValue?: string;
  required?: boolean;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: "danger" | "warning" | "default";
  onConfirm: (value: string) => void;
  onCancel: () => void;
}

/**
 * ZH-DESIGN-SYSTEM-PRECISION-06 — `type="decimal"` exige `precision` (semántica del valor pedido);
 * no existe `decimals` público.
 */
type PromptProps = PromptBaseProps &
  (
    | { type?: "text" | "number" | "date" | "datetime-local"; precision?: never }
    | { type: "decimal"; precision: PrecisionKind }
  );

export function ZHPromptModal(props: PromptProps) {
  const {
  open,
  title,
  message,
  label,
  placeholder,
  type = "text",
  positiveOnly,
  defaultValue = "",
  required = true,
  confirmLabel = "Aceptar",
  cancelLabel = "Cancelar",
  variant = "default",
  onConfirm,
  onCancel,
  } = props;
  const [value, setValue] = useState(defaultValue);
  if (!open) return null;
  return (
    <div
      className="zh-modal-overlay"
      onClick={(e) => {
        if (e.target === e.currentTarget) onCancel();
      }}
    >
      <div
        className="zh-modal zh-modal--sm zh-confirm-dialog"
        role="alertdialog"
        aria-modal="true"
      >
        <div className="zh-confirm-header">
          <span
            className={`material-symbols-outlined zh-confirm-icon zh-confirm-icon--${variant}`}
          >
            {variant === "danger" ? "warning" : "edit"}
          </span>
          <h3 className="zh-confirm-title">{title}</h3>
        </div>
        <div className="zh-confirm-body">
          {message && <p className="zh-confirm-message">{message}</p>}
          <ZHField density="compact" label={label}>
            {props.type === "decimal" ? (
              <ZhDecimalInput
                precision={props.precision}
                positiveOnly={positiveOnly}
                value={value}
                onChange={(e) => setValue(e.target.value)}
                placeholder={placeholder}
                autoFocus
              />
            ) : (
              <input
                type={type}
                value={value}
                onChange={(e) => setValue(e.target.value)}
                placeholder={placeholder}
                autoFocus
              />
            )}
          </ZHField>
        </div>
        <div className="zh-modal-footer">
          <ZHBtn type="button" variant="ghost" size="md" onClick={onCancel}>
            {cancelLabel}
          </ZHBtn>
          <ZHBtn
            type="button"
            variant={variant === "danger" ? "destructive" : "primary"}
            size="md"
            disabled={required && !value.trim()}
            onClick={() => onConfirm(value.trim())}
          >
            {confirmLabel}
          </ZHBtn>
        </div>
      </div>
    </div>
  );
}


