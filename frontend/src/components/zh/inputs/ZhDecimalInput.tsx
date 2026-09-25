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
      onFocus,
      onChange,
      value,
      defaultValue,
      className,
      density = "default",
      ...props
    },
    ref,
  ) => {
    // ZH-DESIGN-SYSTEM-PRECISION-03D — "entrar y salir sin modificar no es una edición". Metadata
    // efímera (no una copia del valor): se activa con el onChange de React, que solo ocurre por
    // entrada real (teclado, borrado, paste y coma normalizada, que se inyectan como input); no
    // con el montaje ni con un `value` controlado actualizado por el padre. Se reinicia al enfocar.
    const editedSinceFocus = React.useRef(false);

    const handleFocus = (e: React.FocusEvent<HTMLInputElement>) => {
      editedSinceFocus.current = false;
      onFocus?.(e);
    };

    const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
      editedSinceFocus.current = true;
      onChange?.(e);
    };

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

    // Re-formatea al perder el foco (ej. "5" → "5.0000", "12.2" → "12.200000") SOLO si el usuario
    // editó desde el último focus: sin edición el valor (p. ej. un dato persistido con más escala)
    // no se reescribe ni se emite onChange sintético. El onBlur externo se propaga siempre.
    const handleBlur = (e: React.FocusEvent<HTMLInputElement>) => {
      const raw = e.currentTarget.value.trim();
      if (editedSinceFocus.current && raw !== "") {
        const num = parseFloat(raw);
        if (!Number.isNaN(num)) {
          const formatted = formatDecimalDisplay(positiveOnly ? Math.max(0, num) : num, decimals);
          if (formatted !== raw)
            setProgrammaticInputValue(e.currentTarget, formatted);
        }
      }
      editedSinceFocus.current = false;
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
        onFocus={handleFocus}
        onChange={handleChange}
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
