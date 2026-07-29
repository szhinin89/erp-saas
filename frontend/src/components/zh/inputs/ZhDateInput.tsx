import React from "react";

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, "type">;

/**
 * Date input normalizado. Siempre muestra DD/MM/YYYY gracias al atributo
 * lang="es-EC". Compatible con RHF register() via forwardRef.
 *
 * @example
 * <ZhDateInput className="zh-input" {...register('issueDate')} />
 */
export const ZhDateInput = React.forwardRef<HTMLInputElement, Props>(
  ({ className = "zh-input", ...props }, ref) => (
    <input
      {...props}
      ref={ref}
      type="date"
      lang="es-EC"
      className={className}
    />
  ),
);

ZhDateInput.displayName = "ZhDateInput";
