import { useMemo, useState } from "react";
import { useI18n } from "../../i18n/i18n";

export function ZHFormHeader(props: {
  title: string;
  subtitle: string;
  badge?: string | null;
  zhLogoSrc?: string | null; // defaults to /zh-logo.svg (public)
  /** Si es false, no se muestra la imagen de marca (p. ej. modales con cabezal compacto). */
  showZhBrandLogo?: boolean;
  right?: React.ReactNode;
}) {
  const { t } = useI18n();
  const {
    title,
    subtitle,
    badge,
    zhLogoSrc,
    right,
    showZhBrandLogo = true,
  } = props;
  return (
    <div className="zh-form-header">
      <div className="zh-form-header-left">
        <div className="zh-form-logo" aria-hidden="true">
          {/* Hexagon + circuit (inline SVG to avoid external assets) */}
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none">
            <path
              d="M8 3.2 4 5.6v4.8l4 2.4 4-2.4V5.6L8 3.2Z"
              stroke="rgba(255,255,255,0.92)"
              strokeWidth="1.3"
              strokeLinejoin="round"
            />
            <path
              d="M12 8h3.2M15.2 8v3.2M15.2 11.2H18"
              stroke="rgba(255,255,255,0.85)"
              strokeWidth="1.2"
              strokeLinecap="round"
            />
            <circle cx="18.2" cy="11.2" r="1.1" fill="rgba(255,255,255,0.85)" />
          </svg>
        </div>
        <div className="zh-form-header-text">
          <h2 className="zh-form-title">{title}</h2>
          <p className="zh-form-subtitle">{subtitle}</p>
        </div>
      </div>
      <div className="zh-form-header-right">
        {right}
        {badge ? <span className="zh-form-badge">{badge}</span> : null}
        {showZhBrandLogo ? (
          <img
            className="zh-form-zh-logo"
            src={zhLogoSrc ?? "/zh-logo.svg"}
            alt={t("app.zh.brandName")}
          />
        ) : null}
      </div>
    </div>
  );
}

export function ZHFormBody(props: {
  children: React.ReactNode;
  standalone?: boolean;
}) {
  return (
    <div
      className={
        props.standalone
          ? "zh-form-body zh-form-body--standalone"
          : "zh-form-body"
      }
    >
      {props.children}
    </div>
  );
}

export function ZHFormSection(props: {
  title?: string;
  description?: string;
  children: React.ReactNode;
}) {
  return (
    <section className="zh-form-section">
      {props.title && <h3 className="zh-form-section-title">{props.title}</h3>}
      {props.description && (
        <p className="zh-form-section-desc">{props.description}</p>
      )}
      {props.children}
    </section>
  );
}

export function ZHGrid(props: {
  cols: 1 | 2 | 3 | 4;
  children: React.ReactNode;
}) {
  return (
    <div className={`zh-grid zh-grid--${props.cols}`}>{props.children}</div>
  );
}

export type ZHFieldHintType =
  "success" | "error" | "warning" | "muted" | "info";

export type ZHFieldDensity = "default" | "compact";

export function ZHField(props: {
  /** Omitir cuando el campo ya tiene rótulo por contexto (p. ej. dentro de una sección colapsable con su propio título). */
  label?: string;
  required?: boolean;
  /** Mensaje de validación (p. ej. Zod + react-hook-form); en español. Si hay `fieldError`, sustituye al `hint` visual de ayuda. */
  fieldError?: string | null;
  /** Alias de `fieldError` (formularios legacy). */
  error?: string | null;
  hint?: string | null;
  hintType?: ZHFieldHintType;
  readOnly?: boolean;
  /** `compact` reproduce la densidad de grilla de líneas (antes un sistema paralelo en Compras): label 11px uppercase, control 13px. */
  density?: ZHFieldDensity;
  className?: string;
  style?: React.CSSProperties;
  children: React.ReactNode;
}) {
  const {
    label,
    required,
    fieldError: fieldErrorProp,
    error,
    hint,
    hintType,
    readOnly,
    density,
    className,
    style,
    children,
  } = props;
  const fieldError = fieldErrorProp ?? error;
  const effectiveHint = fieldError?.trim() ? fieldError : hint;
  const effectiveHintType: ZHFieldHintType | undefined = fieldError?.trim()
    ? "error"
    : hintType;
  const variantClass = effectiveHintType
    ? `zh-field--${effectiveHintType}`
    : "";
  const roClass = readOnly ? "zh-field--readonly" : "";
  const densityClass = density === "compact" ? "zh-field--compact" : "";
  const cls = ["zh-field", variantClass, roClass, densityClass, className]
    .filter(Boolean)
    .join(" ");

  return (
    <label className={cls} style={style}>
      {label ? (
        <span className="zh-field-label">
          {label}
          {required ? <span className="zh-field-required">*</span> : null}
        </span>
      ) : null}
      <div className="zh-field-control">{children}</div>
      {effectiveHint ? (
        <p
          className={`zh-field-hint${effectiveHintType ? ` zh-field-hint--${effectiveHintType}` : ""}`}
        >
          {effectiveHint}
        </p>
      ) : null}
    </label>
  );
}

/**
 * `attention` (naranja) y `neutral` (gris) — variantes oficiales para estados intermedios/
 * inactivos (ni éxito ni error/aviso clásico), p. ej. "configuración incompleta" o
 * "funcionalidad deshabilitada". Mismos tokens de color que `.badge--orange`/`.badge--gray`
 * (`design-tokens.css`) — no son colores nuevos, son la aplicación de tokens ya oficiales
 * a esta variante de alerta.
 */
export type ZHFormAlertType =
  "success" | "error" | "warning" | "info" | "attention" | "neutral";

function alertIcon(type: ZHFormAlertType) {
  const common = {
    width: 18,
    height: 18,
    viewBox: "0 0 24 24",
    fill: "none" as const,
  };
  if (type === "success") {
    return (
      <svg {...common}>
        <path
          d="M20 6 9 17l-5-5"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </svg>
    );
  }
  if (type === "error") {
    return (
      <svg {...common}>
        <path
          d="M12 9v4"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
        />
        <path
          d="M12 17h.01"
          stroke="currentColor"
          strokeWidth="3"
          strokeLinecap="round"
        />
        <path
          d="M10.3 3.6h3.4L21 20H3l7.3-16.4Z"
          stroke="currentColor"
          strokeWidth="1.6"
          strokeLinejoin="round"
        />
      </svg>
    );
  }
  if (type === "warning" || type === "attention") {
    return (
      <svg {...common}>
        <path
          d="M12 9v4"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
        />
        <path
          d="M12 17h.01"
          stroke="currentColor"
          strokeWidth="3"
          strokeLinecap="round"
        />
        <path
          d="M10.3 3.6h3.4L21 20H3l7.3-16.4Z"
          stroke="currentColor"
          strokeWidth="1.6"
          strokeLinejoin="round"
        />
      </svg>
    );
  }
  if (type === "neutral") {
    return (
      <svg {...common}>
        <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="1.7" />
        <path
          d="M9 12h6"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
        />
      </svg>
    );
  }
  return (
    <svg {...common}>
      <path
        d="M12 22a10 10 0 1 0-10-10 10 10 0 0 0 10 10Z"
        stroke="currentColor"
        strokeWidth="1.7"
      />
      <path
        d="M12 10v6"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
      <path
        d="M12 7h.01"
        stroke="currentColor"
        strokeWidth="3"
        strokeLinecap="round"
      />
    </svg>
  );
}

export function ZHFormAlert(props: {
  type: ZHFormAlertType;
  message: string;
  detail?: string | null;
  /** Sobrescribe el icono por defecto de la variante con un Material Symbol (p. ej. para distinguir estados que comparten color). */
  icon?: string;
}) {
  const { type, message, detail, icon } = props;
  return (
    <div className={`zh-form-alert zh-form-alert--${type}`}>
      <span className={`zh-form-alert-icon zh-form-alert-icon--${type}`}>
        {icon ? (
          <span className="material-symbols-outlined" aria-hidden="true">
            {icon}
          </span>
        ) : (
          alertIcon(type)
        )}
      </span>
      <div>
        <p className="zh-form-alert-message">{message}</p>
        {detail ? <p className="zh-form-alert-detail">{detail}</p> : null}
      </div>
    </div>
  );
}

export type ZHBtnVariant = "primary" | "secondary" | "ghost" | "destructive";
export type ZHBtnSize = "md" | "sm" | "xs";

export function ZHBtn(
  props: React.ButtonHTMLAttributes<HTMLButtonElement> & {
    variant?: ZHBtnVariant;
    size?: ZHBtnSize;
  },
) {
  const { variant = "secondary", size, className, ...rest } = props;
  const cls = useMemo(
    () =>
      ["zh-btn", `zh-btn--${variant}`, size ? `zh-btn--${size}` : "", className]
        .filter(Boolean)
        .join(" "),
    [variant, size, className],
  );
  return <button {...rest} className={cls} />;
}

export function ZHFormActions(props: {
  onCancel?: () => void;
  onDraft?: () => void;
  onSave?: () => void;
  hideCancel?: boolean;
  hideDraft?: boolean;
  hideSave?: boolean;
  disableDraft?: boolean;
  disableSave?: boolean;
  draftButtonType?: "button" | "submit";
  saveButtonType?: "button" | "submit";
  labels?: { cancel?: string; draft?: string; save?: string };
  /** Si se define (p. ej. `md`), unifica tamaño de cancelar / borrador / guardar en formularios de datos. Omitir en login público. */
  buttonSize?: ZHBtnSize;
}) {
  const { t } = useI18n();
  const {
    onCancel,
    onDraft,
    onSave,
    hideCancel,
    hideDraft,
    hideSave,
    disableDraft,
    disableSave,
    labels,
    draftButtonType,
    saveButtonType,
    buttonSize,
  } = props;
  const allowDraftWithoutHandler = (draftButtonType ?? "button") === "submit";
  const allowSaveWithoutHandler = (saveButtonType ?? "button") === "submit";
  const sz = buttonSize !== undefined ? { size: buttonSize } : {};
  return (
    <div className="zh-actions">
      {!hideCancel ? (
        <ZHBtn
          type="button"
          variant="ghost"
          {...sz}
          onClick={onCancel}
          disabled={!onCancel}
        >
          {labels?.cancel ?? t("common.cancel")}
        </ZHBtn>
      ) : null}
      {!hideDraft ? (
        <ZHBtn
          type={draftButtonType ?? "button"}
          variant="secondary"
          {...sz}
          onClick={onDraft}
          disabled={(!allowDraftWithoutHandler && !onDraft) || disableDraft}
        >
          {labels?.draft ?? t("common.saveDraft")}
        </ZHBtn>
      ) : null}
      {!hideSave ? (
        <ZHBtn
          type={saveButtonType ?? "button"}
          variant="primary"
          {...sz}
          onClick={onSave}
          disabled={(!allowSaveWithoutHandler && !onSave) || disableSave}
        >
          {labels?.save ?? t("common.saveChanges")}
        </ZHBtn>
      ) : null}
    </div>
  );
}

export function ZHToggle(props: {
  label: string;
  description: string;
  value: boolean;
  onChange: (next: boolean) => void;
  disabled?: boolean;
}) {
  const { label, description, value, onChange, disabled } = props;
  const [pressed, setPressed] = useState(false);

  const toggle = () => {
    if (disabled) return;
    setPressed(true);
    onChange(!value);
    window.setTimeout(() => setPressed(false), 120);
  };

  return (
    <div className="zh-toggle">
      <div className="zh-toggle-text">
        <div className="zh-toggle-label">{label}</div>
        <div className="zh-toggle-desc">{description}</div>
      </div>
      <button
        type="button"
        className={`zh-toggle-switch${pressed ? " is-pressed" : ""}`}
        role="switch"
        aria-checked={value}
        aria-disabled={disabled}
        onClick={toggle}
      >
        <span className="zh-toggle-thumb" />
      </button>
    </div>
  );
}
