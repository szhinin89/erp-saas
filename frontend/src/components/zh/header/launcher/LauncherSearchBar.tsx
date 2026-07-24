import type { Ref } from 'react';
import type { TranslateFn } from '../../../../nav/navConfig';

type LauncherSearchBarProps = {
  value: string;
  onChange: (value: string) => void;
  inputRef: Ref<HTMLInputElement>;
  t: TranslateFn;
};

/** Nivel 1 — Buscador global: localiza módulos, categorías y formularios por nombre. */
export function LauncherSearchBar({ value, onChange, inputRef, t }: LauncherSearchBarProps) {
  return (
    <div className="zh-launcher__searchWrap">
      <span className="material-symbols-outlined zh-launcher__searchIcon" aria-hidden="true">search</span>
      <input
        ref={inputRef}
        type="search"
        className="zh-launcher__search"
        placeholder={t('app.header.appLauncher.searchPlaceholder')}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        aria-label={t('app.header.appLauncher.searchPlaceholder')}
      />
    </div>
  );
}
