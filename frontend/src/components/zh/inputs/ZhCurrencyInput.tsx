import React from 'react';
import { allowsDecimalKey } from '../../../lib/validators/numericValidators';
import { sanitizeDecimal } from '../../../lib/sanitizers';
import { setProgrammaticInputValue } from '../../../lib/inputUtils';
import { getDecimalConfig } from '../../../lib/config/decimal.config';
import './ZhInputs.css';

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type'> & {
  decimals?: number;
  currency?: string;
};

/**
 * Input de moneda con símbolo de divisa y limitación de decimales.
 * Compatible con RHF register() via forwardRef.
 *
 * @example
 * <ZhCurrencyInput {...register('price')} decimals={decimalConfig.sales} />
 * <ZhCurrencyInput {...register('cost')} decimals={decimalConfig.purchases} currency="USD" />
 */
export const ZhCurrencyInput = React.forwardRef<HTMLInputElement, Props>(
  ({ decimals = getDecimalConfig().salesUnitPrice, currency = 'USD', onKeyDown, onPaste, disabled, className, ...props }, ref) => {
    const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
      if (!allowsDecimalKey(e, decimals, true)) e.preventDefault();
      onKeyDown?.(e);
    };

    const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
      e.preventDefault();
      const raw = e.clipboardData.getData('text');
      const clean = sanitizeDecimal(raw, decimals, true);
      if (clean !== '') setProgrammaticInputValue(e.currentTarget, clean);
      onPaste?.(e);
    };

    return (
      <div className={`zh-prefixed-input${disabled ? ' zh-prefixed-input--disabled' : ''}`}>
        <span className="zh-input-prefix" aria-hidden="true">{currency}</span>
        <input
          {...props}
          ref={ref}
          type="text"
          inputMode="decimal"
          disabled={disabled}
          className={className ? `zh-numeric-input ${className}` : 'zh-numeric-input'}
          onKeyDown={handleKeyDown}
          onPaste={handlePaste}
        />
      </div>
    );
  },
);

ZhCurrencyInput.displayName = 'ZhCurrencyInput';
