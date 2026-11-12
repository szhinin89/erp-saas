import React from "react";
import type { ZhInputDensity } from "./ZhTextInput";
import { useZHFieldA11y } from "../zhFieldContext";

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, "type"> & {
  /** `compact` = misma densidad que `<ZHField density="compact">`, para uso suelto
   * dentro de celdas de tabla (`.zh-input--compact`). */
  density?: ZhInputDensity;
};

/**
 * Date input normalizado. Siempre muestra DD/MM/YYYY gracias al atributo
 * lang="es-EC". Compatible con RHF register() via forwardRef.
 *
 * @example
 * <ZhDateInput className="zh-input" {...register('issueDate')} />
 * <ZhDateInput density="compact" {...register('dueDate')} />
 */
export const ZhDateInput = React.forwardRef<HTMLInputElement, Props>(
  ({ className = "zh-input", density = "default", ...props }, ref) => {
    const a11y = useZHFieldA11y(props);
    const cls = [className, density === "compact" ? "zh-input--compact" : ""]
      .filter(Boolean)
      .join(" ");
    return (
      <input
        {...props}
        {...a11y}
        ref={ref}
        type="date"
        lang="es-EC"
        className={cls}
      />
    );
  },
);

ZhDateInput.displayName = "ZhDateInput";
