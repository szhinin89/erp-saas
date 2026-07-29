import { z } from "zod";
import type { ItemEditorMode } from "./types";

/**
 * Un único schema para ambos modos — el modo solo cambia qué es obligatorio, nunca introduce un
 * segundo formulario. Código de barras: obligatorio solo al crear (Update no gestiona códigos de
 * barras, son un recurso independiente del Item, ver `UpdateItemCommand`). El resto de los campos
 * son los mismos en ambos modos.
 */
export function buildItemEditorSchema(mode: ItemEditorMode) {
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
      // IVA del Item — siempre editable, en ambos modos (antes no existía ningún campo de IVA en
      // este formulario; se enviaba `null` fijo). Opcional: un ítem puede no tener IVA de venta
      // configurado todavía, igual que hoy en el formulario completo de Items.
      saleVatCode: z.string().optional(),
      // Precio de Venta — opcional; solo relevante cuando initialData.purchaseContext habilita la
      // sección de simulación. `null` = campo vacío (nunca se infiere un valor). No impone reglas
      // comerciales de margen — solo valida que, si se ingresa, no sea negativo.
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
      (data) =>
        !data.updatePrice || (data.salePrice != null && data.salePrice > 0),
      {
        message:
          "Ingrese un precio de venta válido para actualizar el precio del Item.",
        path: ["salePrice"],
      },
    );
}

export type ItemEditorFormValues = z.infer<
  ReturnType<typeof buildItemEditorSchema>
>;
