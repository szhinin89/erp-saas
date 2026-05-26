import React from 'react';
import {
  allowsLettersKey,
  allowsAlphanumericKey,
} from '../../../lib/validators/textValidators';
import {
  sanitizeLetters,
  sanitizeAlphanumeric,
  uppercase,
} from '../../../lib/sanitizers';
import { setProgrammaticInputValue } from '../../../lib/inputUtils';

export type ZhTextMode = 'text' | 'letters' | 'alphanumeric' | 'uppercase';

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type'> & {
  mode?: ZhTextMode;
};

/**
 * Input de texto con filtrado de caracteres configurable. Compatible con RHF register().
 *
 * - `text`        — sin restricciones (default)
 * - `letters`     — solo letras, acentos, espacios y caracteres de nombre ('. -)
 * - `alphanumeric`— solo letras y dígitos
 * - `uppercase`   — solo letras/dígitos, fuerza mayúsculas en paste
 *
 * @example
 * <ZhTextInput {...register('name')} mode="letters" />
 * <ZhTextInput {...register('sku')} mode="uppercase" />
 */
export const ZhTextInput = React.forwardRef<HTMLInputElement, Props>(
  ({ mode = 'text', onKeyDown, onPaste, ...props }, ref) => {
    const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
      if (mode === 'letters' && !allowsLettersKey(e)) e.preventDefault();
      if ((mode === 'alphanumeric' || mode === 'uppercase') && !allowsAlphanumericKey(e))
        e.preventDefault();
      onKeyDown?.(e);
    };

    const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
      if (mode === 'text') {
        onPaste?.(e);
        return;
      }
      e.preventDefault();
      const raw = e.clipboardData.getData('text');
      let clean = raw;
      if (mode === 'letters') clean = sanitizeLetters(raw);
      if (mode === 'alphanumeric') clean = sanitizeAlphanumeric(raw);
      if (mode === 'uppercase') clean = uppercase(sanitizeAlphanumeric(raw));
      if (clean !== '') setProgrammaticInputValue(e.currentTarget, clean);
      onPaste?.(e);
    };

    return (
      <input
        {...props}
        ref={ref}
        type="text"
        onKeyDown={handleKeyDown}
        onPaste={handlePaste}
      />
    );
  },
);

ZhTextInput.displayName = 'ZhTextInput';
