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
        .min(1, "El nombre comercial es obligatorio.")
        .max(50, "El nombre comercial no puede superar 50 caracteres."),
      description: z
        .string()
        .trim()
        .min(1, "La descripción completa es obligatoria.")
        .max(254, "La descripción completa no puede superar 254 caracteres."),
      nature: z.enum(["Product", "Service"]).default("Product"),
      itemTypeId: z.string().min(1, "El tipo de ítem es obligatorio."),
      categoryNodeId: isCreate ? z.string().min(1, "La categoría es obligatoria.") : z.string(),
      brandId: isCreate ? z.string().min(1, "La marca es obligatoria.") : z.string(),
      defaultUomCode: z.string().min(1, "La unidad base de inventario es obligatoria."),
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
      // Ambos IVA requieren selección explícita e independiente, también al actualizar.
      saleVatCode: z.string().trim().min(1, "Debe seleccionar un código de IVA para venta."),
      purchaseVatCode: z.string().trim().min(1, "Debe seleccionar un código de IVA para compra."),
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
