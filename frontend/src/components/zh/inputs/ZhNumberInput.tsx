import React from 'react';
import { allowsIntegerKey } from '../../../lib/validators/numericValidators';
import { sanitizeInteger } from '../../../lib/sanitizers';
import { setProgrammaticInputValue } from '../../../lib/inputUtils';

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type'> & {
  positiveOnly?: boolean;
};

/**
 * Input para enteros. Compatible con RHF register() via forwardRef.
 * Bloquea teclas inválidas y sanitiza paste.
 *
 * @example
 * // Con register():
 * <ZhNumberInput {...register('quantity', { valueAsNumber: true })} positiveOnly />
 *
 * // Con z.coerce.number() (recomendado — no requiere valueAsNumber):
 * <ZhNumberInput {...register('stock')} positiveOnly />
 */
export const ZhNumberInput = React.forwardRef<HTMLInputElement, Props>(
  ({ positiveOnly = false, onKeyDown, onPaste, ...props }, ref) => {
    const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
      if (!allowsIntegerKey(e, positiveOnly)) e.preventDefault();
      onKeyDown?.(e);
    };

    const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
      e.preventDefault();
      const raw = e.clipboardData.getData('text');
      const clean = sanitizeInteger(raw, positiveOnly);
      if (clean !== '') setProgrammaticInputValue(e.currentTarget, clean);
      onPaste?.(e);
    };

    return (
      <input
        {...props}
        ref={ref}
        type="text"
        inputMode="numeric"
        onKeyDown={handleKeyDown}
        onPaste={handlePaste}
      />
    );
  },
);

ZhNumberInput.displayName = 'ZhNumberInput';
