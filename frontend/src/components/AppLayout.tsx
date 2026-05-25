import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useEffect, useRef, useState, startTransition } from 'react';
import { createPortal } from 'react-dom';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import { LoadingState } from './PageShell';
import { ZHAppSubscriberHeader } from './zh/ZHAppSubscriberHeader';
import { CompanySwitcher } from './CompanySwitcher';
import { LanguageSwitcher } from './LanguageSwitcher';
import { GLOBAL_SUBSCRIBER_ID } from '../constants/subscriberIds';
import { isJwtPlatformOperatorRole } from '../constants/platformAuth';
import { fullLogout } from '../lib/session/fullLogout';
import {
  exitPlatformImpersonation,
  hasPlatformImpersonationReturnPath,
} from '../navigation/platformImpersonationNav';
import { MainMenuList } from './AppLayoutMainMenu';
import { PlatformImpersonationBanner } from './PlatformImpersonationBanner';
import { LayoutFrame } from './layout/LayoutFrame';
import { useAppLayoutNavigation } from './useAppLayoutNavigation';
import './AppLayout.css';

export function AppLayout() {
  const { login } = useAuthStore();
  const { clearPermissions } = usePermissionsStore();
  const navigate = useNavigate();
  const location = useLocation();
  const [platformReturning, setPlatformReturning] = useState(false);

  const {
    user,
    t,
    isGlobalPlatformOperator,
    sessionMenuResolved,
    mainMenuGroups,
    showPlanVerticalNav,
    isFavorite,
    toggleFavorite,
  } = useAppLayoutNavigation();

  const [mainMenuOpenId, setMainMenuOpenId] = useState<string | null>(null);
  const [mainMenuPos, setMainMenuPos] = useState<{ top: number; left: number } | null>(null);
  const mainMenuBarRef = useRef<HTMLDivElement | null>(null);
  const mainMenuPopoverRef = useRef<HTMLDivElement | null>(null);
  const closeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const openGroup = (id: string, rect: DOMRect) => {
    if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
    setMainMenuOpenId(id);
    setMainMenuPos({ top: Math.round(rect.bottom + 4), left: Math.round(rect.left) });
  };

  const scheduleClose = () => {
    closeTimerRef.current = setTimeout(() => setMainMenuOpenId(null), 150);
  };

  const cancelClose = () => {
    if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
  };

  useEffect(() => {
    return () => {
      if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
    };
  }, []);

  useEffect(() => {
    if (!mainMenuOpenId) return;
    const pop = mainMenuPopoverRef.current;
    if (pop && mainMenuPos) {
      pop.style.position = 'fixed';
      pop.style.top = `${mainMenuPos.top}px`;
      pop.style.left = `${mainMenuPos.left}px`;
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setMainMenuOpenId(null);
    };
    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('keydown', onKey);
    };
  }, [mainMenuOpenId, mainMenuPos]);

  useEffect(() => {
    startTransition(() => {
      setMainMenuOpenId(null);
    });
  }, [location.pathname]);

  const handleLogout = () => {
    fullLogout();
    navigate('/login');
  };

  const returnToSubscriberSheet = async () => {
    if (platformReturning) return;
    setPlatformReturning(true);
    try {
      await exitPlatformImpersonation({
        destination: 'return-path',
        navigate,
        login,
        clearPermissions,
      });
    } catch {
      navigate('/platform/subscribers');
    } finally {
      setPlatformReturning(false);
    }
  };

  const returnToGlobal = async () => {
    if (platformReturning) return;
    setPlatformReturning(true);
    try {
      await exitPlatformImpersonation({
        destination: 'global-overview',
        navigate,
        login,
        clearPermissions,
      });
    } catch {
      navigate('/platform/overview');
    } finally {
      setPlatformReturning(false);
    }
  };

  const showImpersonationBanner =
    isJwtPlatformOperatorRole(user?.role) &&
    user?.subscriberId &&
    user.subscriberId !== GLOBAL_SUBSCRIBER_ID;

  return (
    <div className="layout app-layout">
      <LayoutFrame
        variant="subscriber"
        className="app-layout__frame"
        banner={
          showImpersonationBanner ? (
            <PlatformImpersonationBanner
              subscriberId={user.subscriberId}
              t={t}
              hasReturnPath={hasPlatformImpersonationReturnPath()}
              onReturnToSubscriberSheet={returnToSubscriberSheet}
              onReturnToGlobal={returnToGlobal}
              returning={platformReturning}
            />
          ) : null
        }
        topUtilities={
          <div className="app-subscriberHeaderWrap">
            <ZHAppSubscriberHeader
              onLogout={handleLogout}
              leftExtra={!isGlobalPlatformOperator ? <CompanySwitcher /> : null}
              rightExtra={<LanguageSwitcher />}
              bottomLeft={
                !isGlobalPlatformOperator && user && !sessionMenuResolved ? (
                  <div className="app-mainmenu app-mainmenu--loading" role="status" aria-live="polite" aria-busy="true">
                    <LoadingState />
                  </div>
                ) : mainMenuGroups.length > 0 ? (
                  showPlanVerticalNav ? (
                    <div
                      ref={mainMenuBarRef}
                      className="app-mainmenu app-mainmenu--planVertical"
                      role="navigation"
                      aria-label={t('app.layout.mainNav')}
                    >
                      {mainMenuGroups.map((g) => (
                        <div key={g.id} className="app-mainmenu-verticalSection">
                          {mainMenuGroups.length > 1 ? (
                            <div className="app-mainmenu-verticalSectionLabel">{g.label}</div>
                          ) : null}
                          <MainMenuList
                            items={g.items}
                            depth={0}
                            onClose={() => {}}
                            isFavorite={isFavorite}
                            toggleFavorite={toggleFavorite}
                            t={t}
                            showFavoriteStars={!isGlobalPlatformOperator}
                          />
                        </div>
                      ))}
                    </div>
                  ) : (
                    <div ref={mainMenuBarRef} className="app-mainmenu" role="navigation" aria-label={t('app.layout.mainNav')}>
                      {mainMenuGroups.map((g) => (
                        <button
                          key={g.id}
                          type="button"
                          className={`app-mainmenu-item${g.isActive ? ' is-active' : ''}${mainMenuOpenId === g.id ? ' is-open' : ''}`}
                          aria-haspopup="menu"
                          aria-expanded={mainMenuOpenId === g.id}
                          onMouseEnter={(e) => openGroup(g.id, e.currentTarget.getBoundingClientRect())}
                          onMouseLeave={scheduleClose}
                          onClick={(e) => {
                            if (mainMenuOpenId === g.id) {
                              setMainMenuOpenId(null);
                            } else {
                              openGroup(g.id, e.currentTarget.getBoundingClientRect());
                            }
                          }}
                        >
                          {g.icon ? <span className="app-mainmenu-groupIcon" aria-hidden="true">{g.icon}</span> : null}
                          <span className="app-mainmenu-groupLabel">{g.label}</span>
                          <span className="app-mainmenu-caret" aria-hidden="true">▾</span>
                        </button>
                      ))}
                    </div>
                  )
                ) : null
              }
            />
          </div>
        }
      >
        <Outlet />
      </LayoutFrame>

      {mainMenuOpenId && mainMenuPos && !showPlanVerticalNav
        ? createPortal(
            <div
              ref={mainMenuPopoverRef}
              className="app-mainmenu-popover"
              role="menu"
              aria-label={t('app.layout.groupMenu')}
              data-top={mainMenuPos.top}
              data-left={mainMenuPos.left}
              onMouseEnter={cancelClose}
              onMouseLeave={scheduleClose}
            >
              <MainMenuList
                items={mainMenuGroups.find((g) => g.id === mainMenuOpenId)?.items ?? []}
                depth={0}
                onClose={() => setMainMenuOpenId(null)}
                isFavorite={isFavorite}
                toggleFavorite={toggleFavorite}
                t={t}
                showFavoriteStars={!isGlobalPlatformOperator}
              />
            </div>,
            document.body,
          )
        : null}
    </div>
  );
}
