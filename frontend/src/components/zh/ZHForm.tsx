import './ZHForm.css';
import { useMemo, useState } from 'react';
import { useI18n } from '../../i18n/i18n';

export function ZHFormHeader(props: {
  title: string;
  subtitle: string;
  badge?: string | null;
  zhLogoSrc?: string | null; // defaults to /zh-logo.png
  right?: React.ReactNode;
}) {
  const { t } = useI18n();
  const { title, subtitle, badge, zhLogoSrc, right } = props;
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
        <img className="zh-form-zh-logo" src={zhLogoSrc ?? '/zh-logo.png'} alt={t('app.zh.brandName')} />
      </div>
    </div>
  );
}

export type ZHTenantHeaderModuleCrumb = { label: string; icon?: React.ReactNode; active?: boolean };

export function ZHMultiTenantHeader(props: {
  tenantName: string;
  tenantMeta?: string[]; // e.g. ["Quito, Ecuador", "RUC 179001...", "Plan Empresarial"]
  tenantBadge?: string | null; // e.g. Plan
  tenantInitials?: string; // fallback when no logo
  tenantLogoUrl?: string | null;
  tenantColor?: string | null; // used when no logo image
  fiscalYear?: string | number | null;
  modules?: ZHTenantHeaderModuleCrumb[];
  statusText?: string | null; // e.g. "Sistema operativo · Tenant #TEN-0014"
  zhLogoSrc?: string | null; // defaults to /zh-logo.png
  right?: React.ReactNode;
}) {
  const {
    tenantName,
    tenantMeta,
    tenantBadge,
    tenantInitials,
    tenantLogoUrl,
    tenantColor,
    fiscalYear,
    modules,
    statusText,
    zhLogoSrc,
    right,
  } = props;

  const { t } = useI18n();

  const initials = (tenantInitials ?? tenantName)
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((x) => x[0]!.toUpperCase())
    .join('');

  return (
    <div className="zh-tenant-header">
      <div className="zh-tenant-top">
        <div
          className="zh-tenant-logo"
          style={tenantLogoUrl ? undefined : tenantColor ? { background: tenantColor } : undefined}
        >
          {tenantLogoUrl ? <img src={tenantLogoUrl} alt="" /> : <span className="zh-tenant-initials">{initials}</span>}
        </div>

        <div className="zh-tenant-info">
          <div className="zh-tenant-name">{tenantName}</div>
          {(tenantMeta?.length ?? 0) > 0 ? (
            <div className="zh-tenant-sub">
              {tenantMeta!.map((x, idx) => (
                <span key={`${idx}-${x}`}>{x}</span>
              ))}
              {tenantBadge ? <span className="zh-tenant-badge">{tenantBadge}</span> : null}
            </div>
          ) : tenantBadge ? (
            <div className="zh-tenant-sub">
              <span className="zh-tenant-badge">{tenantBadge}</span>
            </div>
          ) : null}
        </div>

        <div className="zh-tenant-right">
          {right}
          {fiscalYear ? (
            <div className="zh-tenant-fy">
              <div className="zh-tenant-fy-label">{t('app.fiscalYear.label')}</div>
              <div className="zh-tenant-fy-value">{fiscalYear}</div>
            </div>
          ) : null}
        </div>
      </div>

      {(modules?.length ?? 0) > 0 ? (
        <div className="zh-tenant-modules">
          {modules!.map((m, idx) => (
            <div key={`${idx}-${m.label}`} className={`zh-tenant-module-pill${m.active ? ' is-active' : ''}`}>
              {m.icon ? <span className="zh-tenant-module-ico" aria-hidden="true">{m.icon}</span> : null}
              {m.label}
            </div>
          ))}
        </div>
      ) : null}

      <div className="zh-tenant-bottom">
        <div className="zh-tenant-status">
          <span className="zh-tenant-status-dot" aria-hidden="true" />
          <span className="zh-tenant-status-text">{statusText ?? ''}</span>
        </div>
        <div className="zh-tenant-credit" title={t('app.zh.developedByTitle')}>
          <img className="zh-tenant-credit-logo" src={zhLogoSrc ?? '/zh-logo.png'} alt={t('app.zh.brandName')} />
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
  hint?: string | null;
  hintType?: ZHFieldHintType;
  readOnly?: boolean;
  children: React.ReactNode;
}) {
  const { label, required, hint, hintType, readOnly, children } = props;
  const variantClass = hintType ? `zh-field--${hintType}` : '';
  const roClass = readOnly ? 'zh-field--readonly' : '';
  const cls = ['zh-field', variantClass, roClass].filter(Boolean).join(' ');

  return (
    <label className={cls}>
      <span className="zh-field-label">
        {label}
        {required ? <span className="zh-field-required">*</span> : null}
      </span>
      <div className="zh-field-control">{children}</div>
      {hint ? <p className={`zh-field-hint${hintType ? ` zh-field-hint--${hintType}` : ''}`}>{hint}</p> : null}
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
      <span className="zh-form-alert-icon" style={{ color: type === 'success' ? 'var(--zh-success)' : type === 'error' ? 'var(--zh-danger)' : type === 'warning' ? 'var(--zh-warning)' : 'var(--zh-blue)' }}>
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
        className="zh-toggle-switch"
        role="switch"
        aria-checked={value}
        aria-disabled={disabled}
        onClick={toggle}
        style={pressed ? { boxShadow: '0 0 0 3px rgba(79, 123, 166, 0.15)' } : undefined}
      >
        <span className="zh-toggle-thumb" />
      </button>
    </div>
  );
}

