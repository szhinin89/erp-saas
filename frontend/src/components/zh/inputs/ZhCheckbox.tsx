import React from "react";
import { useZHFieldA11y } from "../zhFieldContext";

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, "type" | "children"> & {
  /**
   * Texto de la opción. Si se omite, se renderiza SOLO el `<input type="checkbox">` (celdas de
   * tabla, "seleccionar todo", o cuando el consumidor ya envuelve el control en su propio
   * `<label>`). Con `label` se envuelve en `<label class="zh-checkbox-label">` — el patrón visual
   * que ya usaban los formularios (`.zh-checkbox-label` en zh-ui.css).
   */
  label?: React.ReactNode;
  /** Clase extra del `<label>` (solo si hay `label`), p. ej. un modificador de layout del módulo. */
  labelClassName?: string;
};

/**
 * Checkbox oficial del Design System (Paquete C2). Input nativo con `forwardRef`, todos los
 * eventos nativos (`onChange`, `onBlur`, `checked`/`defaultChecked`, `disabled`, `name`) y
 * compatible con React Hook Form: `<ZhCheckbox {...register("isActive")} label="Activo" />`.
 * Dentro de un `<ZHField>` con error recibe `aria-invalid`/`aria-describedby` automáticamente.
 *
 * Es distinto de `<ZHToggle>`: el toggle es un interruptor con descripción para configuración
 * booleana; el checkbox es para selección de filas, filtros y opciones en línea.
 */
export const ZhCheckbox = React.forwardRef<HTMLInputElement, Props>(
  ({ label, labelClassName, ...props }, ref) => {
    const a11y = useZHFieldA11y(props);
    const input = <input {...props} {...a11y} ref={ref} type="checkbox" />;
    if (label === undefined) return input;
    return (
      <label className={["zh-checkbox-label", labelClassName].filter(Boolean).join(" ")}>
        {input}
        <span>{label}</span>
      </label>
    );
  },
);

ZhCheckbox.displayName = "ZhCheckbox";
