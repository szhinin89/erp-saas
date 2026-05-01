import { useI18n } from '../i18n/i18n';
import type { Locale } from '../i18n/dictionaries';
import './LanguageSwitcher.css';

const options: Array<{ value: Locale; label: string }> = [
  { value: 'es', label: 'ES' },
  { value: 'en', label: 'EN' },
  { value: 'qu', label: 'QU' },
];

export function LanguageSwitcher() {
  const { locale, setLocale } = useI18n();

  return (
    <select
      className="lang-switcher"
      value={locale}
      onChange={(e) => setLocale(e.target.value as Locale)}
      aria-label="Language switcher"
    >
      {options.map((o) => (
        <option key={o.value} value={o.value}>
          {o.label}
        </option>
      ))}
    </select>
  );
}

