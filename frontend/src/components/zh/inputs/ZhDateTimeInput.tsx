import React from "react";
import type { ZhInputDensity } from "./ZhTextInput";

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, "type"> & {
  /** `compact` = misma densidad que `<ZHField density="compact">`, para uso suelto
   * dentro de celdas de tabla (`.zh-input--compact`). */
  density?: ZhInputDensity;
};

/**
 * Input de fecha + hora normalizado (`type="datetime-local"`). Compatible con
 * RHF register() via forwardRef. El valor debe llegar ya normalizado al
 * formato `yyyy-MM-ddTHH:mm` — usar `toDateTimeLocalInputValue()` de
 * `lib/formatters/dateFormatters` al poblar el formulario (p. ej. en `reset()`),
 * nunca pasar el ISO/SRI crudo del backend directamente.
 *
 * Errores/ayuda/label/required/readOnly se manejan con el `<ZHField>` que lo
 * envuelve (mismo patrón que ZhTextInput/ZhDateInput), no dentro de este input.
 *
 * @example
 * <ZHField label="Fecha y hora autorización" density="compact">
 *   <ZhDateTimeInput {...register('authorizationDate')} />
 * </ZHField>
 */
export const ZhDateTimeInput = React.forwardRef<HTMLInputElement, Props>(
  ({ className, density = "default", step = 1, ...props }, ref) => {
    const cls = [className, density === "compact" ? "zh-input--compact" : ""]
      .filter(Boolean)
      .join(" ");
    return (
      <input
        {...props}
        ref={ref}
        type="datetime-local"
        // ZH-TEMPORAL-DATETIME-SECONDS-02I: los instantes del ERP (p. ej. fechaAutorizacion SRI)
        // tienen segundos reales; step=1 permite que el valor los conserve sin marcarse inválido.
        step={step}
        className={cls || undefined}
      />
    );
  },
);

ZhDateTimeInput.displayName = "ZhDateTimeInput";
