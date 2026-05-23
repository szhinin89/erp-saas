import type { ReactNode } from 'react';
import './LayoutFrame.css';

export type LayoutFrameVariant = 'tenant' | 'platform';

export type LayoutFrameProps = {
  children: ReactNode;
  /** Banner above utilities (impersonation, alerts). */
  banner?: ReactNode;
  /** Sticky header / top utilities above scrollable page content. */
  topUtilities?: ReactNode;
  variant?: LayoutFrameVariant;
  className?: string;
};

/**
 * Shared content-area frame for tenant and platform shells.
 * Navigation, sidebars and auth stay in AppLayout / PlatformLayout.
 *
 * @architecture Do not import from route pages — only AppLayout / PlatformLayout.
 * @see docs/frontend-layout-conventions.md
 */
export function LayoutFrame({
  children,
  banner,
  topUtilities,
  variant = 'tenant',
  className,
}: LayoutFrameProps) {
  const rootClass = [
    'shell-content-frame',
    `shell-content-frame--${variant}`,
    className,
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <main className={rootClass}>
      {banner ? <div className="shell-content-frame__banner">{banner}</div> : null}
      {topUtilities ? (
        <div className="shell-content-frame__utilities">{topUtilities}</div>
      ) : null}
      <div className="shell-content-frame__scroll">
        <div className="shell-content-frame__inner">{children}</div>
      </div>
    </main>
  );
}
