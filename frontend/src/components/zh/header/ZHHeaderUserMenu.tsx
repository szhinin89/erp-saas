import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import type { TranslateFn } from "../../../nav/navConfig";

/** Avatar + menú de sesión del usuario autenticado (popover con nombre, username/email y logout). El rol ya se muestra en el badge del header — no se repite aquí. */
export function ZHHeaderUserMenu(props: {
  fullName: string;
  username: string;
  email: string | null;
  onLogout?: () => void;
  t: TranslateFn;
}) {
  const { fullName, username, email, onLogout, t } = props;
  const [open, setOpen] = useState(false);
  const anchorRef = useRef<HTMLButtonElement | null>(null);
  const popoverRef = useRef<HTMLDivElement | null>(null);
  const [menuPos, setMenuPos] = useState<{ top: number; right: number } | null>(
    null,
  );

  useEffect(() => {
    if (!open) return;
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
      setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    window.addEventListener(
      "pointerdown",
      onDown as unknown as EventListener,
      true,
    );
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener(
        "pointerdown",
        onDown as unknown as EventListener,
        true,
      );
      window.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <div className="zh-app-userMenu">
      <button
        type="button"
        className="zh-app-tenantAvatarBtn"
        aria-label={t("app.header.userMenu")}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((s) => !s)}
        ref={anchorRef}
      >
        <span className="zh-app-tenantAvatar" aria-hidden="true">
          {(fullName?.[0] ?? username?.[0] ?? "U").toUpperCase()}
        </span>
        <span className="zh-app-tenantAvatarStatus" aria-hidden="true" />
      </button>
      {open && menuPos
        ? createPortal(
            <div
              className="zh-app-userMenuPopover"
              role="menu"
              aria-label={t("app.header.sessionMenu")}
              style={{
                position: "fixed",
                top: menuPos.top,
                right: menuPos.right,
              }}
              ref={popoverRef}
            >
              <div className="zh-app-userMenuHeader">
                <div className="zh-app-userMenuName">
                  {fullName ?? username}
                </div>
                <div className="zh-app-userMenuEmail">{email ?? username}</div>
              </div>
              <button
                type="button"
                className="zh-app-userMenuItem"
                role="menuitem"
                onClick={() => {
                  setOpen(false);
                  onLogout?.();
                }}
              >
                {t("app.logout")}
              </button>
            </div>,
            document.body,
          )
        : null}
    </div>
  );
}
