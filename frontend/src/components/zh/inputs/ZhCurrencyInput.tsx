import React from "react";
import { handleDecimalKeyDown } from "../../../lib/validators/numericValidators";
import { sanitizeDecimal } from "../../../lib/sanitizers";
import { setProgrammaticInputValue } from "../../../lib/inputUtils";
import type { PrecisionKind } from "../../../lib/config/precisionPolicy.config";
import { SemanticDecimals } from "../SemanticDecimals";
import "./ZhInputs.css";

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, "type"> & {
  /** Override CONTRACTUAL explícito (constante `*_DECIMALS` documentada): gana sobre `precision`.
   * Sin `decimals` ni `precision` = default legacy 2, DEPRECATED (F-PREC-implicit-value). */
  decimals?: number;
  currency?: string;
};

/**
 * Input de moneda con símbolo de divisa y limitación de decimales.
 * Compatible con RHF register() via forwardRef.
 *
 * Fallback fijo de 2 decimales (COMPANY-PRECISION-POLICY-FRONTEND-CONSUMERS-MIGRATION-07):
 * los callers que necesiten la precisión de la empresa declaran `precision` (03B, vía
 * `SemanticDecimals`) o pasan `decimals` explícito — sin ninguno de los dos no lee config.
 *
 * @example
 * <ZhCurrencyInput {...register('price')} precision="salesUnitPrice" />
 * <ZhCurrencyInput {...register('cost')} precision="purchaseUnitPrice" currency="USD" />
 */
const ZhCurrencyInputCore = React.forwardRef<HTMLInputElement, Props>(
  (
    {
      decimals = 2,
      currency = "USD",
      onKeyDown,
      onPaste,
      disabled,
      className,
      ...props
    },
    ref,
  ) => {
    const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
      handleDecimalKeyDown(e, decimals, true);
      onKeyDown?.(e);
    };

    const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
      e.preventDefault();
      const raw = e.clipboardData.getData("text");
      const clean = sanitizeDecimal(raw, decimals, true);
      if (clean !== "") setProgrammaticInputValue(e.currentTarget, clean);
      onPaste?.(e);
    };

    return (
      <div
        className={`zh-prefixed-input${disabled ? " zh-prefixed-input--disabled" : ""}`}
      >
        <span className="zh-input-prefix" aria-hidden="true">
          {currency}
        </span>
        <input
          {...props}
          ref={ref}
          type="text"
          inputMode="decimal"
          disabled={disabled}
          className={
            className ? `zh-numeric-input ${className}` : "zh-numeric-input"
          }
          onKeyDown={handleKeyDown}
          onPaste={handlePaste}
        />
      </div>
    );
  },
);

ZhCurrencyInputCore.displayName = "ZhCurrencyInputCore";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-03B — API pública. Prioridad de decimales (igual que `ZHMoneyValue`):
 * `decimals` explícito > `precision` (PrecisionPolicy vía `SemanticDecimals`) > default legacy del
 * core. Solo resuelve CUÁNTOS decimales: el comportamiento del input es el del core, sin cambios.
 * Sin `precision` no depende de la PrecisionPolicy.
 *
 * LEGACY (ZH-DESIGN-SYSTEM-PRECISION-05B): sin `decimals` ni `precision` usa el default fijo 2 —
 * DEPRECATED para código nuevo y bloqueado por el guard F-PREC-implicit-value (compatibilidad
 * temporal, sin consumidores productivos hoy). No se expresa como sobrecarga @deprecated: el
 * componente es un `forwardRef` (una sola firma de llamada) y separarla exigiría un cast del tipo
 * exportado. Patrón normal: `precision`; `decimals` solo como constante contractual `*_DECIMALS`.
 */
export const ZhCurrencyInput = React.forwardRef<HTMLInputElement, Props & { precision?: PrecisionKind }>(
  ({ precision, decimals, ...props }, ref) => {
    if (decimals == null && precision !== undefined) {
      return (
        <SemanticDecimals kind={precision}>
          {(resolved) => <ZhCurrencyInputCore {...props} decimals={resolved} ref={ref} />}
        </SemanticDecimals>
      );
    }
    return <ZhCurrencyInputCore {...props} decimals={decimals} ref={ref} />;
  },
);

ZhCurrencyInput.displayName = "ZhCurrencyInput";
