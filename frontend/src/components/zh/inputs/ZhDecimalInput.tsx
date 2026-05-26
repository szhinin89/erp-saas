import React from 'react';
import { allowsDecimalKey } from '../../../lib/validators/numericValidators';
import { sanitizeDecimal } from '../../../lib/sanitizers';
import { setProgrammaticInputValue } from '../../../lib/inputUtils';

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type'> & {
  decimals?: number;
  positiveOnly?: boolean;
};

/**
 * Input decimal. Compatible con RHF register() via forwardRef.
 * Bloquea teclas inválidas, limita decimales y sanitiza paste.
 *
 * @example
 * <ZhDecimalInput {...register('price')} decimals={decimalConfig.sales} positiveOnly />
 */
export const ZhDecimalInput = React.forwardRef<HTMLInputElement, Props>(
  ({ decimals = 2, positiveOnly = false, onKeyDown, onPaste, ...props }, ref) => {
    const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
      if (!allowsDecimalKey(e, decimals, positiveOnly)) e.preventDefault();
      onKeyDown?.(e);
    };

    const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
      e.preventDefault();
      const raw = e.clipboardData.getData('text');
      const clean = sanitizeDecimal(raw, decimals, positiveOnly);
      if (clean !== '') setProgrammaticInputValue(e.currentTarget, clean);
      onPaste?.(e);
    };

    return (
      <input
        {...props}
        ref={ref}
        type="text"
        inputMode="decimal"
        onKeyDown={handleKeyDown}
        onPaste={handlePaste}
      />
    );
  },
);

ZhDecimalInput.displayName = 'ZhDecimalInput';
