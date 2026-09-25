import React from "react";
import { handleDecimalKeyDown } from "../../../lib/validators/numericValidators";
import { formatDecimalDisplay, sanitizeDecimal } from "../../../lib/sanitizers";
import { setProgrammaticInputValue } from "../../../lib/inputUtils";
import type { PrecisionKind } from "../../../lib/config/precisionPolicy.config";
import { SemanticDecimals } from "../SemanticDecimals";
import type { ZhInputDensity } from "./ZhTextInput";

type Props = Omit<
  React.InputHTMLAttributes<HTMLInputElement>,
  "type" | "value" | "defaultValue"
> & {
  decimals?: number;
  positiveOnly?: boolean;
  value?: string | number;
  defaultValue?: string | number;
  /** `compact` = misma densidad que `<ZHField density="compact">`, para uso suelto
   * dentro de celdas de tabla (`.zh-input--compact`). */
  density?: ZhInputDensity;
};

/**
 * Input decimal. Compatible con RHF register() via forwardRef.
 * Bloquea teclas inválidas, limita decimales y sanitiza paste.
 * Si value/defaultValue llega como number, se formatea con `decimals` — evita que cada
 * pantalla repita formatMoney(x, decimals) para mostrar el valor inicial correctamente.
 *
 * @example
 * <ZhDecimalInput {...register('price')} decimals={decimalConfig.sales} positiveOnly />
 * <ZhDecimalInput {...register('amount')} density="compact" />
 */
const ZhDecimalInputCore = React.forwardRef<HTMLInputElement, Props>(
  (
    {
      decimals = 2,
      positiveOnly = false,
      onKeyDown,
      onPaste,
      onBlur,
      value,
      defaultValue,
      className,
      density = "default",
      ...props
    },
    ref,
  ) => {
    const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
      handleDecimalKeyDown(e, decimals, positiveOnly);
      onKeyDown?.(e);
    };

    const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
      e.preventDefault();
      const raw = e.clipboardData.getData("text");
      const clean = sanitizeDecimal(raw, decimals, positiveOnly);
      if (clean !== "") setProgrammaticInputValue(e.currentTarget, clean);
      onPaste?.(e);
    };

    // Re-formatea al perder el foco (ej. "5" → "5.0000", "12.2" → "12.200000") — el input es
    // no controlado mientras se edita, así que sin esto el DOM se queda con lo que el usuario
    // tecleó literalmente en vez de respetar `decimals` configurado por empresa.
    const handleBlur = (e: React.FocusEvent<HTMLInputElement>) => {
      const raw = e.currentTarget.value.trim();
      if (raw !== "") {
        const num = parseFloat(raw);
        if (!Number.isNaN(num)) {
          const formatted = formatDecimalDisplay(positiveOnly ? Math.max(0, num) : num, decimals);
          if (formatted !== raw)
            setProgrammaticInputValue(e.currentTarget, formatted);
        }
      }
      onBlur?.(e);
    };

    const cls = [
      "zh-numeric-input",
      density === "compact" ? "zh-input--compact" : "",
      className,
    ]
      .filter(Boolean)
      .join(" ");

    return (
      <input
        {...props}
        ref={ref}
        type="text"
        inputMode="decimal"
        className={cls}
        value={typeof value === "number" ? formatDecimalDisplay(value, decimals) : value}
        defaultValue={
          typeof defaultValue === "number"
            ? formatDecimalDisplay(defaultValue, decimals)
            : defaultValue
        }
        onKeyDown={handleKeyDown}
        onPaste={handlePaste}
        onBlur={handleBlur}
      />
    );
  },
);

ZhDecimalInputCore.displayName = "ZhDecimalInputCore";

/**
 * ZH-DESIGN-SYSTEM-PRECISION-03B — API pública. Prioridad de decimales (igual que `ZHMoneyValue`):
 * `decimals` explícito > `precision` (PrecisionPolicy vía `SemanticDecimals`) > default legacy del
 * core. Solo resuelve CUÁNTOS decimales: el comportamiento del input es el del core, sin cambios.
 * Sin `precision` no depende de la PrecisionPolicy.
 */
export const ZhDecimalInput = React.forwardRef<HTMLInputElement, Props & { precision?: PrecisionKind }>(
  ({ precision, decimals, ...props }, ref) => {
    if (decimals == null && precision !== undefined) {
      return (
        <SemanticDecimals kind={precision}>
          {(resolved) => <ZhDecimalInputCore {...props} decimals={resolved} ref={ref} />}
        </SemanticDecimals>
      );
    }
    return <ZhDecimalInputCore {...props} decimals={decimals} ref={ref} />;
  },
);

ZhDecimalInput.displayName = "ZhDecimalInput";
