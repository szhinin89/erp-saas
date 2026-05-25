import './ZHForm.css';
import { useMemo, useState } from 'react';
import { useI18n } from '../../i18n/i18n';

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
  const { title, subtitle, badge, zhLogoSrc, right, showZhBrandLogo = true } = props;
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
          <img className="zh-form-zh-logo" src={zhLogoSrc ?? '/zh-logo.svg'} alt={t('app.zh.brandName')} />
        ) : null}
      </div>
    </div>
  );
}

export type ZHSubscriberHeaderModuleCrumb = { label: string; icon?: React.ReactNode; active?: boolean };

export function ZHSubscriberShellHeader(props: {
  subscriberName: string;
  subscriberMeta?: string[]; // e.g. ["Quito, Ecuador", "RUC 179001...", "Plan Empresarial"]
  subscriberBadge?: string | null; // e.g. Plan
  subscriberInitials?: string; // fallback when no logo
  subscriberLogoUrl?: string | null;
  subscriberColor?: string | null; // used when no logo image
  fiscalYear?: string | number | null;
  modules?: ZHSubscriberHeaderModuleCrumb[];
  statusText?: string | null; // e.g. "Sistema operativo · Subscriber #TEN-0014"
  zhLogoSrc?: string | null; // defaults to /zh-logo.svg (public)
  right?: React.ReactNode;
}) {
  const {
    subscriberName,
    subscriberMeta,
    subscriberBadge,
    subscriberInitials,
    subscriberLogoUrl,
    subscriberColor,
    fiscalYear,
    modules,
    statusText,
    zhLogoSrc,
    right,
  } = props;

  const { t } = useI18n();

  const initials = (subscriberInitials ?? subscriberName)
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((x) => x[0]!.toUpperCase())
    .join('');

  return (
    <div className="zh-subscriber-header">
      <div className="zh-subscriber-top">
        <div
          className="zh-subscriber-logo"
          style={subscriberLogoUrl ? undefined : subscriberColor ? { background: subscriberColor } : undefined}
        >
          {subscriberLogoUrl ? <img src={subscriberLogoUrl} alt="" /> : <span className="zh-subscriber-initials">{initials}</span>}
        </div>

        <div className="zh-subscriber-info">
          <div className="zh-subscriber-name">{subscriberName}</div>
          {(subscriberMeta?.length ?? 0) > 0 ? (
            <div className="zh-subscriber-sub">
              {subscriberMeta!.map((x, idx) => (
                <span key={`${idx}-${x}`}>{x}</span>
              ))}
              {subscriberBadge ? <span className="zh-subscriber-badge">{subscriberBadge}</span> : null}
            </div>
          ) : subscriberBadge ? (
            <div className="zh-subscriber-sub">
              <span className="zh-subscriber-badge">{subscriberBadge}</span>
            </div>
          ) : null}
        </div>

        <div className="zh-subscriber-right">
          {right}
          {fiscalYear ? (
            <div className="zh-subscriber-fy">
              <div className="zh-subscriber-fy-label">{t('app.fiscalYear.label')}</div>
              <div className="zh-subscriber-fy-value">{fiscalYear}</div>
            </div>
          ) : null}
        </div>
      </div>

      {(modules?.length ?? 0) > 0 ? (
        <div className="zh-subscriber-modules">
          {modules!.map((m, idx) => (
            <div key={`${idx}-${m.label}`} className={`zh-subscriber-module-pill${m.active ? ' is-active' : ''}`}>
              {m.icon ? <span className="zh-subscriber-module-ico" aria-hidden="true">{m.icon}</span> : null}
              {m.label}
            </div>
          ))}
        </div>
      ) : null}

      <div className="zh-subscriber-bottom">
        <div className="zh-subscriber-status">
          <span className="zh-subscriber-status-dot" aria-hidden="true" />
          <span className="zh-subscriber-status-text">{statusText ?? ''}</span>
        </div>
        <div className="zh-subscriber-credit" title={t('app.zh.developedByTitle')}>
          <img className="zh-subscriber-credit-logo" src={zhLogoSrc ?? '/zh-logo.svg'} alt={t('app.zh.brandName')} />
        </div>
      </div>
    </div>
  );
}

export function ZHFormBody(props: { children: React.ReactNode; standalone?: boolean }) {
  return (
    <div className={props.standalone ? 'zh-form-body zh-form-body--standalone' : 'zh-form-body'}>
      {props.children}
    </div>
  );
}

export function ZHFormSection(props: { title: string; children: React.ReactNode }) {
  return (
    <section className="zh-form-section">
      <h3 className="zh-form-section-title">{props.title}</h3>
      {props.children}
    </section>
  );
}

export function ZHGrid(props: { cols: 1 | 2 | 3; children: React.ReactNode }) {
  return <div className={`zh-grid zh-grid--${props.cols}`}>{props.children}</div>;
}

export type ZHFieldHintType = 'success' | 'error' | 'warning' | 'muted' | 'info';

export function ZHField(props: {
  label: string;
  required?: boolean;
  /** Mensaje de validación (p. ej. Zod + react-hook-form); en español. Si hay `fieldError`, sustituye al `hint` visual de ayuda. */
  fieldError?: string | null;
  /** Alias de `fieldError` (formularios legacy). */
  error?: string | null;
  hint?: string | null;
  hintType?: ZHFieldHintType;
  readOnly?: boolean;
  style?: React.CSSProperties;
  children: React.ReactNode;
}) {
  const { label, required, fieldError: fieldErrorProp, error, hint, hintType, readOnly, style, children } = props;
  const fieldError = fieldErrorProp ?? error;
  const effectiveHint = fieldError?.trim() ? fieldError : hint;
  const effectiveHintType: ZHFieldHintType | undefined = fieldError?.trim() ? 'error' : hintType;
  const variantClass = effectiveHintType ? `zh-field--${effectiveHintType}` : '';
  const roClass = readOnly ? 'zh-field--readonly' : '';
  const cls = ['zh-field', variantClass, roClass].filter(Boolean).join(' ');

  return (
    <label className={cls} style={style}>
      <span className="zh-field-label">
        {label}
        {required ? <span className="zh-field-required">*</span> : null}
      </span>
      <div className="zh-field-control">{children}</div>
      {effectiveHint ? (
        <p className={`zh-field-hint${effectiveHintType ? ` zh-field-hint--${effectiveHintType}` : ''}`}>{effectiveHint}</p>
      ) : null}
    </label>
  );
}

export type ZHFormAlertType = 'success' | 'error' | 'warning' | 'info';

function alertIcon(type: ZHFormAlertType) {
  const common = { width: 18, height: 18, viewBox: '0 0 24 24', fill: 'none' as const };
  if (type === 'success') {
    return (
      <svg {...common}>
        <path d="M20 6 9 17l-5-5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    );
  }
  if (type === 'error') {
    return (
      <svg {...common}>
        <path d="M12 9v4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
        <path d="M12 17h.01" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
        <path d="M10.3 3.6h3.4L21 20H3l7.3-16.4Z" stroke="currentColor" strokeWidth="1.6" strokeLinejoin="round" />
      </svg>
    );
  }
  if (type === 'warning') {
    return (
      <svg {...common}>
        <path d="M12 9v4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
        <path d="M12 17h.01" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
        <path d="M10.3 3.6h3.4L21 20H3l7.3-16.4Z" stroke="currentColor" strokeWidth="1.6" strokeLinejoin="round" />
      </svg>
    );
  }
  return (
    <svg {...common}>
      <path d="M12 22a10 10 0 1 0-10-10 10 10 0 0 0 10 10Z" stroke="currentColor" strokeWidth="1.7" />
      <path d="M12 10v6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M12 7h.01" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
    </svg>
  );
}

export function ZHFormAlert(props: { type: ZHFormAlertType; message: string; detail?: string | null }) {
  const { type, message, detail } = props;
  return (
    <div className={`zh-form-alert zh-form-alert--${type}`}>
      <span className={`zh-form-alert-icon zh-form-alert-icon--${type}`}>
        {alertIcon(type)}
      </span>
      <div>
        <p className="zh-form-alert-message">{message}</p>
        {detail ? <p className="zh-form-alert-detail">{detail}</p> : null}
      </div>
    </div>
  );
}

export type ZHBtnVariant = 'primary' | 'secondary' | 'ghost' | 'destructive';
export type ZHBtnSize = 'md' | 'sm' | 'xs';

export function ZHBtn(
  props: React.ButtonHTMLAttributes<HTMLButtonElement> & {
    variant: ZHBtnVariant;
    size?: ZHBtnSize;
  }
) {
  const { variant, size, className, ...rest } = props;
  const cls = useMemo(
    () => ['zh-btn', `zh-btn--${variant}`, size ? `zh-btn--${size}` : '', className].filter(Boolean).join(' '),
    [variant, size, className]
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
  draftButtonType?: 'button' | 'submit';
  saveButtonType?: 'button' | 'submit';
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
  const allowDraftWithoutHandler = (draftButtonType ?? 'button') === 'submit';
  const allowSaveWithoutHandler = (saveButtonType ?? 'button') === 'submit';
  const sz = buttonSize !== undefined ? { size: buttonSize } : {};
  return (
    <div className="zh-actions">
      {!hideCancel ? (
        <ZHBtn type="button" variant="ghost" {...sz} onClick={onCancel} disabled={!onCancel}>
          {labels?.cancel ?? t('common.cancel')}
        </ZHBtn>
      ) : null}
      {!hideDraft ? (
        <ZHBtn
          type={draftButtonType ?? 'button'}
          variant="secondary"
          {...sz}
          onClick={onDraft}
          disabled={(!allowDraftWithoutHandler && !onDraft) || disableDraft}
        >
          {labels?.draft ?? t('common.saveDraft')}
        </ZHBtn>
      ) : null}
      {!hideSave ? (
        <ZHBtn
          type={saveButtonType ?? 'button'}
          variant="primary"
          {...sz}
          onClick={onSave}
          disabled={(!allowSaveWithoutHandler && !onSave) || disableSave}
        >
          {labels?.save ?? t('common.saveChanges')}
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
        className={`zh-toggle-switch${pressed ? ' is-pressed' : ''}`}
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

