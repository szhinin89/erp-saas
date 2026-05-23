import { useState } from 'react';
import { readPlatformImpersonationName } from '../lib/session/sessionStorageKeys';

export function getImpersonationSubscriberName(): string | null {
  return readPlatformImpersonationName();
}

type PlatformImpersonationBannerProps = {
  subscriberId: string;
  t: (key: string) => string;
  hasReturnPath: boolean;
  onReturnToSubscriberSheet: () => void | Promise<void>;
  onReturnToGlobal: () => void | Promise<void>;
  returning: boolean;
};

export function PlatformImpersonationBanner({
  subscriberId,
  t,
  hasReturnPath,
  onReturnToSubscriberSheet,
  onReturnToGlobal,
  returning,
}: PlatformImpersonationBannerProps) {
  const [open, setOpen] = useState(false);

  return (
    <div className={`platform-impersonation-banner${open ? ' is-open' : ''}`}>
      <button
        type="button"
        className="platform-impersonation-banner-toggle"
        onClick={() => setOpen((s) => !s)}
        aria-expanded={open}
      >
        <span className="platform-impersonation-banner-dot" aria-hidden="true" />
        <strong className="platform-impersonation-banner-title">{t('platform.banner')}</strong>
        <span className="platform-impersonation-banner-subscriberInline" title={t('platform.subscriber.title')}>
          {getImpersonationSubscriberName() ?? t('platform.subscriber.unknown')}
        </span>
        <span className="platform-impersonation-banner-subscriberIdInline mono" title={t('platform.subscriber.idTitle')}>
          {subscriberId}
        </span>
        <span className="platform-impersonation-banner-caret" aria-hidden="true">{open ? '▾' : '▸'}</span>
      </button>

      {hasReturnPath ? (
        <button
          className="platform-impersonation-banner-btn platform-impersonation-banner-btn--compact"
          onClick={() => void onReturnToSubscriberSheet()}
          type="button"
          disabled={returning}
        >
          {t('platform.backToSubscriberSheet')}
        </button>
      ) : null}

      {open ? (
        <div className="platform-impersonation-banner-details">
          <div className="platform-impersonation-banner-subscriber" title={t('platform.subscriber.title')}>
            {getImpersonationSubscriberName() ?? t('platform.subscriber.unknown')}
          </div>
          <div className="platform-impersonation-banner-subscriberId mono" title={t('platform.subscriber.idTitle')}>
            {subscriberId}
          </div>
          <div className="platform-impersonation-banner-actions">
            {hasReturnPath ? (
              <button
                className="platform-impersonation-banner-btn platform-impersonation-banner-btn--primary"
                onClick={() => void onReturnToSubscriberSheet()}
                type="button"
                disabled={returning}
              >
                {t('platform.backToSubscriberSheet')}
              </button>
            ) : null}
            <button
              className="platform-impersonation-banner-btn"
              onClick={() => void onReturnToGlobal()}
              type="button"
              disabled={returning}
            >
              {t('platform.backToGlobal')}
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
