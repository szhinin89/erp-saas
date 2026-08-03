import { z } from "zod";
import type { ItemEditorMode } from "./types";

/**
 * Un único schema para ambos modos — el modo solo cambia qué es obligatorio, nunca introduce un
 * segundo formulario. Código de barras: obligatorio solo al crear (Update no gestiona códigos de
 * barras, son un recurso independiente del Item, ver `UpdateItemCommand`). El resto de los campos
 * son los mismos en ambos modos.
 */
export function buildItemEditorSchema(mode: ItemEditorMode) {
  const isCreate = mode === "create";
  return z
    .object({
      sku: z
        .string()
        .trim()
        .min(1, "El SKU es obligatorio.")
        .max(50, "El SKU no puede exceder 50 caracteres.")
        .regex(
          /^[A-Za-z0-9\-_.]+$/,
          "El SKU solo puede contener letras, números, guiones, puntos y guiones bajos.",
        ),
      shortName: z
        .string()
        .trim()
        .min(1, "El nombre corto es obligatorio.")
        .max(50, "El nombre corto no puede exceder 50 caracteres."),
      description: z
        .string()
        .trim()
        .min(1, "La descripción es obligatoria.")
        .max(254, "La descripción no puede exceder 254 caracteres."),
      itemTypeId: z.string().min(1, "El tipo de ítem es obligatorio."),
      categoryNodeId: z.string().min(1, "La categoría es obligatoria."),
      brandId: z.string().min(1, "La marca es obligatoria."),
      defaultUomCode: z.string().min(1, "La unidad de medida es obligatoria."),
      barcode:
        mode === "create"
          ? z
              .string()
              .trim()
              .min(1, "El código de barras es obligatorio.")
              .max(100, "El código de barras no puede exceder 100 caracteres.")
          : z.string().optional(),
      barcodeType:
        mode === "create"
          ? z.string().min(1, "El tipo de código de barras es obligatorio.")
          : z.string().optional(),
      observations: z
        .string()
        .trim()
        .max(500, "Las observaciones no pueden exceder 500 caracteres.")
        .optional(),
      // IVA de venta — siempre editable, en ambos modos. Obligatorio al crear (este editor solo
      // se usa desde Compras: el producto debe quedar listo para vender sin volver a pasar por el
      // módulo de Items); en Update sigue siendo opcional para no bloquear ediciones que no tocan
      // impuestos en ítems legados sin IVA configurado.
      saleVatCode: isCreate
        ? z.string().min(1, "Debe seleccionar el IVA de venta del Item.")
        : z.string().optional(),
      // IVA de compra — mismo criterio que saleVatCode. Antes no existía ningún campo editable en
      // este formulario (siempre viajaba `null` al crear) — causa raíz de que las líneas de
      // compra quedaran sin IVA y forzaran editar el Item desde el módulo completo.
      purchaseVatCode: isCreate
        ? z.string().min(1, "Debe seleccionar el IVA de compra del Item.")
        : z.string().optional(),
      // Precio de Venta — obligatorio al crear (el Item debe quedar vendible sin reproceso
      // manual); en Update sigue siendo opcional salvo que se active "Actualizar precio".
      salePrice: z
        .number()
        .nullable()
        .optional()
        .refine((v) => v === null || v === undefined || v >= 0, {
          message: "El precio no puede ser negativo.",
        }),
      updatePrice: z.boolean().default(false),
    })
    .refine(
      (data) => {
        if (isCreate) return data.salePrice != null && data.salePrice > 0;
        return !data.updatePrice || (data.salePrice != null && data.salePrice > 0);
      },
      {
        message: isCreate
          ? "Ingrese un precio de venta válido para crear el Item."
          : "Ingrese un precio de venta válido para actualizar el precio del Item.",
        path: ["salePrice"],
      },
    );
}

export type ItemEditorFormValues = z.infer<
  ReturnType<typeof buildItemEditorSchema>
>;
