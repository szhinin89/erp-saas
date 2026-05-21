import { useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useAuthStore } from '../../store/authStore';
import { useI18n } from '../../i18n/i18n';
import { RuntimeModeBadge } from '../RuntimeModeBadge';
import { SUPERADMIN_IMPERSONATION_NAME_KEY } from '../../lib/session/sessionStorageKeys';
import './ZHForm.css';

function getImpersonationSubscriberName(): string | null {
  return localStorage.getItem(SUPERADMIN_IMPERSONATION_NAME_KEY);
}

function initials(name: string) {
  const parts = name.trim().split(/\s+/).slice(0, 2);
  const init = parts.map((p) => p[0]?.toUpperCase() ?? '').join('');
  return init || 'ZH';
}

export function ZHAppSubscriberHeader(props: {
  bottomLeft?: React.ReactNode;
  rightExtra?: React.ReactNode;
  leftExtra?: React.ReactNode;
  onLogout?: () => void;
}) {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const anchorRef = useRef<HTMLButtonElement | null>(null);
  const popoverRef = useRef<HTMLDivElement | null>(null);
  const [menuPos, setMenuPos] = useState<{ top: number; right: number } | null>(null);

  const isGlobalSuperAdmin =
    (user?.role ?? '') === 'SuperAdmin' &&
    (user?.subscriberId ?? '') === '00000000-0000-0000-0000-000000000000';
  const isImpersonatingSubscriber =
    (user?.role ?? '') === 'SuperAdmin' &&
    !!user?.subscriberId &&
    user.subscriberId !== '00000000-0000-0000-0000-000000000000';

  const subscriberName = useMemo(() => {
    if (!user) return '';
    if (isGlobalSuperAdmin) return user.fullName || t('superadmin.title');
    if (isImpersonatingSubscriber) return getImpersonationSubscriberName() ?? t('superadmin.subscriber.unknown');
    return t('app.subscriber.defaultName');
  }, [isGlobalSuperAdmin, isImpersonatingSubscriber, t, user]);

  const subtitle = useMemo(() => {
    if (!user) return '';
    if (isGlobalSuperAdmin) return t('superadmin.subtitle');
    return user.email;
  }, [isGlobalSuperAdmin, t, user]);

  useEffect(() => {
    if (!userMenuOpen) return;
    const btn = anchorRef.current;
    if (btn) {
      const r = btn.getBoundingClientRect();
      setMenuPos({
        top: Math.round(r.bottom + 10),
        right: Math.max(12, Math.round(window.innerWidth - r.right)),
      });
    }
    const onDown = (e: MouseEvent | TouchEvent) => {
      const target = e.target;
      if (!(target instanceof Node)) return;
      const anchor = anchorRef.current;
      const pop = popoverRef.current;
      if (anchor && anchor.contains(target)) return;
      if (pop && pop.contains(target)) return;
      setUserMenuOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setUserMenuOpen(false);
    };
    window.addEventListener('pointerdown', onDown as unknown as EventListener, true);
    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('pointerdown', onDown as unknown as EventListener, true);
      window.removeEventListener('keydown', onKey);
    };
  }, [userMenuOpen]);

  if (!user) return null;

  return (
    <div className="zh-subscriber-header zh-app-subscriberHeader">
      <div className="zh-subscriber-top">
        <div className="zh-app-subscriberLeftGroup">
          {props.leftExtra ? <div className="zh-app-subscriberLeftExtra">{props.leftExtra}</div> : null}
          <div className="zh-subscriber-logo" aria-hidden="true">
            <span className="zh-subscriber-initials">{initials(subscriberName || user.fullName || 'ZH')}</span>
          </div>
        </div>

        <div className="zh-subscriber-info">
          <div className="zh-subscriber-name">{subscriberName}</div>
          <div className="zh-subscriber-sub">
            <span className="zh-subscriber-badge">{user.role}</span>
            <span className="zh-subscriber-subtext">{subtitle}</span>
          </div>
        </div>

        <div className="zh-subscriber-right" aria-label={t('app.header.actions')}>
          <RuntimeModeBadge />
          <button type="button" className="zh-app-subscriberIconBtn" aria-label={t('app.header.notifications')}>
            🔔
          </button>
          <button type="button" className="zh-app-subscriberIconBtn" aria-label={t('app.header.apps')}>
            ⊞
          </button>
          {props.rightExtra ? <div className="zh-app-subscriberRightExtra">{props.rightExtra}</div> : null}
          <div className="zh-app-userMenu">
            <button
              type="button"
              className="zh-app-subscriberAvatarBtn"
              aria-label={t('app.header.userMenu')}
              aria-haspopup="menu"
              aria-expanded={userMenuOpen}
              onClick={() => setUserMenuOpen((s) => !s)}
              ref={anchorRef}
            >
              <span className="zh-app-subscriberAvatar" aria-hidden="true">
                {(user.fullName?.[0] ?? user.email?.[0] ?? 'U').toUpperCase()}
              </span>
            </button>
            {userMenuOpen && menuPos
              ? createPortal(
                  <div
                    className="zh-app-userMenuPopover"
                    role="menu"
                    aria-label={t('app.header.sessionMenu')}
                    style={{ position: 'fixed', top: menuPos.top, right: menuPos.right }}
                    ref={popoverRef}
                  >
                    <div className="zh-app-userMenuHeader">
                      <div className="zh-app-userMenuName">{user.fullName ?? user.email}</div>
                      <div className="zh-app-userMenuRole">{user.role}</div>
                    </div>
                    <button
                      type="button"
                      className="zh-app-userMenuItem"
                      role="menuitem"
                      onClick={() => {
                        setUserMenuOpen(false);
                        props.onLogout?.();
                      }}
                    >
                      {t('app.logout')}
                    </button>
                  </div>,
                  document.body
                )
              : null}
          </div>
        </div>
      </div>

      {props.bottomLeft ? (
        <div className="zh-subscriber-bottom">
          <div className="zh-subscriber-status" aria-label={t('app.header.bottomNav')}>
            {props.bottomLeft}
          </div>
          <div className="zh-subscriber-credit" aria-label={t('app.header.vendorCredit')}>
            <span className="zh-subscriber-credit-text">{t('app.zh.brandName')}</span>
          </div>
        </div>
      ) : null}
    </div>
  );
}

