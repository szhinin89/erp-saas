import React, { useId } from 'react';
import { allowsPhoneDigitKey } from '../../../lib/validators/textValidators';
import { sanitizePhoneLocal } from '../../../lib/sanitizers';
import { setProgrammaticInputValue } from '../../../lib/inputUtils';
import { phoneConfig } from '../../../lib/config/phone.config';
import './ZhInputs.css';

type Props = {
  value?: string;
  onChange?: (value: string) => void;
  onBlur?: () => void;
  disabled?: boolean;
  id?: string;
  name?: string;
  placeholder?: string;
};

/**
 * Input de teléfono con prefijo de país configurable (default: Ecuador +593).
 * Almacena el número completo (prefix + dígitos locales).
 * Debe usarse con RHF `Controller`.
 *
 * Cambiar país: modificar `frontend/src/lib/config/phone.config.ts`.
 *
 * @example
 * <Controller
 *   control={control}
 *   name="phone"
 *   render={({ field }) => <ZhPhoneInput {...field} />}
 * />
 */
export function ZhPhoneInput({ value, onChange, onBlur, disabled, id, name, placeholder }: Props) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const prefix = phoneConfig.defaultCountryCode;

  const localValue = React.useMemo(() => {
    if (!value) return '';
    const clean = value.replace(/\s/g, '');
    return clean.startsWith(prefix) ? clean.slice(prefix.length) : clean.replace(/[^\d]/g, '');
  }, [value, prefix]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.currentTarget.value.length >= phoneConfig.localLength && !allowsPhoneDigitKey(e)) {
      // still allow nav keys (handled inside allowsPhoneDigitKey)
    }
    if (!allowsPhoneDigitKey(e)) e.preventDefault();
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const local = sanitizePhoneLocal(e.target.value, phoneConfig.localLength);
    const full = local.length > 0 ? `${prefix}${local}` : '';
    onChange?.(full);
  };

  const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
    e.preventDefault();
    const raw = e.clipboardData.getData('text');
    const strippedPrefix = raw.replace(prefix, '');
    const local = sanitizePhoneLocal(strippedPrefix, phoneConfig.localLength);
    const full = local.length > 0 ? `${prefix}${local}` : '';
    setProgrammaticInputValue(e.currentTarget, local);
    onChange?.(full);
  };

  return (
    <div className={`zh-prefixed-input${disabled ? ' zh-prefixed-input--disabled' : ''}`}>
      <span className="zh-input-prefix" aria-hidden="true">{prefix}</span>
      <input
        id={inputId}
        name={name}
        type="tel"
        inputMode="tel"
        value={localValue}
        placeholder={placeholder ?? phoneConfig.placeholder}
        disabled={disabled}
        onKeyDown={handleKeyDown}
        onChange={handleChange}
        onPaste={handlePaste}
        onBlur={onBlur}
        maxLength={phoneConfig.localLength}
        autoComplete="tel-national"
      />
    </div>
  );
}
