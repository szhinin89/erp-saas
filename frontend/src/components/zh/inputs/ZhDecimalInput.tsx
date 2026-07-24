import React from 'react';
import { allowsDecimalKey } from '../../../lib/validators/numericValidators';
import { sanitizeDecimal } from '../../../lib/sanitizers';
import { setProgrammaticInputValue } from '../../../lib/inputUtils';

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type' | 'value' | 'defaultValue'> & {
  decimals?: number;
  positiveOnly?: boolean;
  value?: string | number;
  defaultValue?: string | number;
};

/**
 * Input decimal. Compatible con RHF register() via forwardRef.
 * Bloquea teclas inválidas, limita decimales y sanitiza paste.
 * Si value/defaultValue llega como number, se formatea con `decimals` — evita que cada
 * pantalla repita formatMoney(x, decimals) para mostrar el valor inicial correctamente.
 *
 * @example
 * <ZhDecimalInput {...register('price')} decimals={decimalConfig.sales} positiveOnly />
 */
export const ZhDecimalInput = React.forwardRef<HTMLInputElement, Props>(
  ({ decimals = 2, positiveOnly = false, onKeyDown, onPaste, onBlur, value, defaultValue, className, ...props }, ref) => {
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

    // Re-formatea al perder el foco (ej. "5" → "5.0000", "12.2" → "12.200000") — el input es
    // no controlado mientras se edita, así que sin esto el DOM se queda con lo que el usuario
    // tecleó literalmente en vez de respetar `decimals` configurado por empresa.
    const handleBlur = (e: React.FocusEvent<HTMLInputElement>) => {
      const raw = e.currentTarget.value.trim();
      if (raw !== '') {
        const num = parseFloat(raw);
        if (!Number.isNaN(num)) {
          const formatted = (positiveOnly ? Math.max(0, num) : num).toFixed(decimals);
          if (formatted !== raw) setProgrammaticInputValue(e.currentTarget, formatted);
        }
      }
      onBlur?.(e);
    };

    return (
      <input
        {...props}
        ref={ref}
        type="text"
        inputMode="decimal"
        className={className ? `zh-numeric-input ${className}` : 'zh-numeric-input'}
        value={typeof value === 'number' ? value.toFixed(decimals) : value}
        defaultValue={typeof defaultValue === 'number' ? defaultValue.toFixed(decimals) : defaultValue}
        onKeyDown={handleKeyDown}
        onPaste={handlePaste}
        onBlur={handleBlur}
      />
    );
  },
);

ZhDecimalInput.displayName = 'ZhDecimalInput';
