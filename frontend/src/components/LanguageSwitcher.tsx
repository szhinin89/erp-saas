import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useI18n } from '../i18n/i18n';
import type { Locale } from '../i18n/dictionaries';
import './LanguageSwitcher.css';

type LangOption = { value: Locale; label: string; optionTitle?: string };

/**
 * Etiquetas del menú según idioma activo (product owner):
 * - UI español → Español, Inglés, Kichwa
 * - UI inglés → Spanish, English, Kichwa
 * - UI kichwa → Español, Inglés, Kichwa
 */
export function LanguageSwitcher() {
  const { locale, setLocale, t } = useI18n();
  const [open, setOpen] = useState(false);
  const [menuPos, setMenuPos] = useState<{ top: number; right: number } | null>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  const options = useMemo<LangOption[]>(
    () => [
      { value: 'es', label: t('app.langMenu.spanish') },
      { value: 'en', label: t('app.langMenu.english') },
      { value: 'qu', label: t('app.langMenu.kichwa'), optionTitle: t('app.language.kichwa') },
    ],
    [t],
  );

  const currentLabel = useMemo(() => options.find((o) => o.value === locale)?.label ?? locale, [locale, options]);

  const updateMenuPosition = useCallback(() => {
    const el = triggerRef.current;
    if (!el) return;
    const r = el.getBoundingClientRect();
    setMenuPos({
      top: Math.round(r.bottom + 6),
      right: Math.max(12, Math.round(window.innerWidth - r.right)),
    });
  }, []);

  useEffect(() => {
    if (!open) return;
    updateMenuPosition();
    const onReposition = () => updateMenuPosition();
    window.addEventListener('resize', onReposition);
    window.addEventListener('scroll', onReposition, true);
    return () => {
      window.removeEventListener('resize', onReposition);
      window.removeEventListener('scroll', onReposition, true);
    };
  }, [open, updateMenuPosition]);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent | TouchEvent) => {
      const target = e.target;
      if (!(target instanceof Node)) return;
      if (triggerRef.current?.contains(target)) return;
      if (menuRef.current?.contains(target)) return;
      setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    window.addEventListener('pointerdown', onDown as unknown as EventListener, true);
    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('pointerdown', onDown as unknown as EventListener, true);
      window.removeEventListener('keydown', onKey);
    };
  }, [open]);

  useLayoutEffect(() => {
    const el = menuRef.current;
    if (!open || !menuPos || !el) return;
    el.style.setProperty('position', 'fixed');
    el.style.setProperty('top', `${menuPos.top}px`);
    el.style.setProperty('right', `${menuPos.right}px`);
  }, [open, menuPos]);

  const pick = (next: Locale) => {
    setLocale(next);
    setOpen(false);
  };

  return (
    <div className="lang-switcher">
      <button
        ref={triggerRef}
        type="button"
        className="lang-switcher__trigger"
        aria-label={t('app.languageSwitcher.aria')}
        aria-haspopup="listbox"
        aria-expanded={open}
        title={locale === 'qu' ? t('app.language.kichwa') : undefined}
        onClick={() => {
          setOpen((s) => !s);
        }}
      >
        <span className="lang-switcher__triggerText">{currentLabel}</span>
        <span className="lang-switcher__chevron" aria-hidden>
          ▾
        </span>
      </button>
      {open && menuPos
        ? createPortal(
            <div
              ref={menuRef}
              className="lang-switcher__popover"
              role="listbox"
              aria-label={t('app.languageSwitcher.aria')}
            >
              {options.map((o) => (
                <button
                  key={o.value}
                  type="button"
                  role="option"
                  className="lang-switcher__option"
                  aria-selected={locale === o.value}
                  title={o.optionTitle}
                  onClick={() => pick(o.value)}
                >
                  {o.label}
                </button>
              ))}
            </div>,
            document.body,
          )
        : null}
    </div>
  );
}
