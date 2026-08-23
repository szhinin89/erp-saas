import { HELP_KEYS, type HelpKeyId } from "./helpKeys";
import type { HelpContent } from "./helpTypes";

/** Único punto de datos de ayuda contextual (equivalente a messageCatalog.ts). 1 helpKey = 1 entrada. */
export const HELP_REGISTRY: Record<HelpKeyId, HelpContent> = {
  [HELP_KEYS.SALES_CUSTOMER_CONSUMER_FINAL]: {
    title: "Consumidor Final",
    short: "Solo para ventas a contado hasta {maxConsumerFinalAmount}.",
    long: "Para superar este valor, seleccione un cliente identificado.",
  },
  [HELP_KEYS.SALES_EMISSION_SECTION]: {
    title: "Datos de Emisión",
    short: "Sucursal, caja, punto y tipo de documento se resuelven automáticamente según su sesión.",
    long: "Los define el servidor a partir de la caja/sucursal abierta; no se editan aquí.",
  },
  [HELP_KEYS.SALES_EMISSION_TYPE]: {
    title: "Tipo de Emisión",
    short: "Física o Electrónica según la configuración SRI del punto de emisión.",
    long: "Lo determina la configuración del punto de emisión, no se puede cambiar desde esta pantalla.",
  },
  [HELP_KEYS.SALES_CASH_SESSION]: {
    title: "Caja",
    short: "Caja asociada a su sesión activa; se abre desde el módulo de Caja antes de facturar.",
  },
  [HELP_KEYS.SALES_PAYMENTS_SECTION]: {
    title: "Formas de Cobro",
    short: "Registre uno o varios medios de pago hasta cubrir el total a cobrar.",
    long: "Puede combinar efectivo, tarjeta, transferencia, cheque o crédito (si el cliente lo permite).",
  },
  [HELP_KEYS.SALES_PAYMENTS_CASH_RECEIVED]: {
    title: "Monto recibido",
    short: "Dinero en efectivo que el cliente entrega en caja.",
    long: "Se usa solo para calcular el vuelto; no afecta el total de la factura.",
  },
  [HELP_KEYS.SALES_PAYMENTS_CHANGE]: {
    title: "Vuelto",
    short: "Diferencia entre el monto recibido en efectivo y el total a cobrar.",
  },
  [HELP_KEYS.SALES_CHECKLIST]: {
    title: "Checklist de emisión",
    short: "Resume qué falta completar antes de poder emitir la factura.",
    long: "Se actualiza automáticamente según cliente, líneas, caja y formas de pago registradas.",
  },
  [HELP_KEYS.SALES_PRODUCT_SEARCH]: {
    title: "Buscar productos",
    short: "Escriba nombre, código o escanee con lector de código de barras (F2).",
  },
  [HELP_KEYS.SALES_STOCK]: {
    title: "Stock disponible",
    short: "Cantidad disponible en la bodega seleccionada para esta línea.",
    long: "Se actualiza en tiempo real; una venta puede quedar bloqueada si supera el stock disponible.",
  },
  [HELP_KEYS.SALES_PRICING]: {
    title: "Precios",
    short: "Precio sin IVA, IVA calculado y precio final por unidad.",
    long: "El precio final es el que paga el cliente; el costo nunca se muestra en ventas.",
  },
  [HELP_KEYS.PURCHASES_SUPPLIER]: {
    title: "Proveedor",
    short: "Selecciónelo por RUC o nombre; sus datos fiscales se completan automáticamente.",
  },
  [HELP_KEYS.PURCHASES_SRI_AUTHORIZATION]: {
    title: "Autorización SRI",
    short: "Número de autorización emitido por el SRI para este comprobante electrónico.",
  },
  [HELP_KEYS.PURCHASES_SRI_AUTHORIZATION_DATE]: {
    title: "Fecha y hora de autorización",
    short: "Fecha y hora en que el SRI autorizó este comprobante.",
  },
  [HELP_KEYS.PURCHASES_XML_RECEIVED]: {
    title: "XML recibido",
    short: "Indica si esta línea proviene del XML/TXT enviado por el proveedor.",
    long: "Si la compra fue manual, no hay XML de origen.",
  },
  [HELP_KEYS.PURCHASES_ITEM_MATCHING]: {
    title: "Matching de ítems",
    short: "Compara el producto de la línea con el ítem ya registrado en el catálogo interno.",
  },
  [HELP_KEYS.PURCHASES_WAREHOUSE]: {
    title: "Bodega",
    short: "Bodega destino donde ingresará el stock de esta compra.",
    long: "Puede sobrescribirse por línea si un producto va a otra bodega.",
  },
  [HELP_KEYS.PURCHASES_VAT]: {
    title: "IVA",
    short: "Impuesto al valor agregado calculado sobre las líneas gravadas de la factura.",
  },
  [HELP_KEYS.PURCHASES_RETENTIONS]: {
    title: "Retenciones",
    short: "Retenciones de IVA y Renta aplicadas según el proveedor y el tipo de ítem.",
  },
  [HELP_KEYS.PURCHASES_PAYMENT_SCHEDULE]: {
    title: "Plazos de pago",
    short: "Cuotas y fechas de vencimiento de esta compra a crédito.",
  },
  [HELP_KEYS.PURCHASES_CREDIT_NOTE]: {
    title: "Nota de crédito",
    short: "Tipo de nota de crédito según si el origen es una compra registrada o un XML recibido.",
  },
};
