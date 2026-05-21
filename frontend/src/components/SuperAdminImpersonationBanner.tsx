import { useState } from 'react';
import { SUPERADMIN_IMPERSONATION_NAME_KEY } from '../lib/session/sessionStorageKeys';

export function getImpersonationSubscriberName(): string | null {
  return localStorage.getItem(SUPERADMIN_IMPERSONATION_NAME_KEY);
}

type SuperAdminImpersonationBannerProps = {
  subscriberId: string;
  t: (key: string) => string;
  onReturnToGlobal: () => void | Promise<void>;
  returningGlobal: boolean;
};

export function SuperAdminImpersonationBanner({
  subscriberId,
  t,
  onReturnToGlobal,
  returningGlobal,
}: SuperAdminImpersonationBannerProps) {
  const [open, setOpen] = useState(false);

  return (
    <div className={`superadmin-banner${open ? ' is-open' : ''}`}>
      <button
        type="button"
        className="superadmin-banner-toggle"
        onClick={() => setOpen((s) => !s)}
        aria-expanded={open}
      >
        <span className="superadmin-banner-dot" aria-hidden="true" />
        <strong className="superadmin-banner-title">{t('superadmin.banner')}</strong>
        <span className="superadmin-banner-subscriberInline" title={t('superadmin.subscriber.title')}>
          {getImpersonationSubscriberName() ?? t('superadmin.subscriber.unknown')}
        </span>
        <span className="superadmin-banner-subscriberIdInline mono" title={t('superadmin.subscriber.idTitle')}>
          {subscriberId}
        </span>
        <span className="superadmin-banner-caret" aria-hidden="true">{open ? '▾' : '▸'}</span>
      </button>

      {open ? (
        <div className="superadmin-banner-details">
          <div className="superadmin-banner-subscriber" title={t('superadmin.subscriber.title')}>
            {getImpersonationSubscriberName() ?? t('superadmin.subscriber.unknown')}
          </div>
          <div className="superadmin-banner-subscriberId mono" title={t('superadmin.subscriber.idTitle')}>
            {subscriberId}
          </div>
          <button
            className="superadmin-banner-btn"
            onClick={() => void onReturnToGlobal()}
            type="button"
            disabled={returningGlobal}
          >
            {t('superadmin.backToGlobal')}
          </button>
        </div>
      ) : null}
    </div>
  );
}
