import { HELP_KEYS, type HelpKeyId } from "./helpKeys";
import type { HelpContent } from "./helpTypes";

/** Único punto de datos de ayuda contextual (equivalente a messageCatalog.ts). 1 helpKey = 1 entrada. */
export const HELP_REGISTRY: Record<HelpKeyId, HelpContent> = {
  [HELP_KEYS.COMPANY_FISCAL_TAX_REGIME]: {
    i18nPrefix: "help.companyFiscal.taxRegime",
    title: "Régimen tributario",
    short: "Clasificación tributaria de la empresa. Seleccione el régimen que consta en su información fiscal vigente.",
    long: "El ERP lo guarda en el perfil fiscal y lo utiliza al resolver la política de Consumidor Final."
  },
  [HELP_KEYS.COMPANY_FISCAL_SPECIAL_TAXPAYER]: {
    i18nPrefix: "help.companyFiscal.specialTaxpayer",
    title: "Contribuyente especial",
    short: "Número de resolución que acredita la designación de contribuyente especial. Complételo solo si la empresa tiene esa designación.",
    long: "El ERP conserva este dato fiscal para los comprobantes; ingresarlo no otorga la designación del SRI."
  },
  [HELP_KEYS.COMPANY_FISCAL_RETENTION_AGENT_RESOLUTION]: {
    i18nPrefix: "help.companyFiscal.retentionAgentResolution",
    title: "Agente de retención: resolución",
    short: "Resolución de designación como agente de retención. Complete su número y vigencia cuando exista una designación aplicable a la empresa.",
    long: "El ERP registra la designación del emisor con su vigencia para los comprobantes. Es distinta de las casillas operativas de retención de renta e IVA."
  },
  [HELP_KEYS.COMPANY_FISCAL_RETENTION_AGENT_EFFECTIVE_FROM]: {
    i18nPrefix: "help.companyFiscal.retentionAgentEffectiveFrom",
    title: "Agente de retención: vigencia",
    short: "Fecha desde la que rige la resolución de agente de retención. Complétela junto con el número de resolución, según la designación recibida.",
    long: "El ERP conserva la fecha para determinar la vigencia de la designación del emisor en los comprobantes."
  },
  [HELP_KEYS.COMPANY_FISCAL_LARGE_TAXPAYER_RESOLUTION]: {
    i18nPrefix: "help.companyFiscal.largeTaxpayerResolution",
    title: "Gran contribuyente: resolución",
    short: "Resolución de designación como gran contribuyente. Complétela con su vigencia solo cuando la empresa tenga esa designación.",
    long: "El ERP registra esta condición fiscal del emisor para los comprobantes; el registro no concede una calificación del SRI."
  },
  [HELP_KEYS.COMPANY_FISCAL_LARGE_TAXPAYER_EFFECTIVE_FROM]: {
    i18nPrefix: "help.companyFiscal.largeTaxpayerEffectiveFrom",
    title: "Gran contribuyente: vigencia",
    short: "Fecha desde la que rige la resolución de gran contribuyente. Complétela junto con la resolución según la designación recibida.",
    long: "El ERP conserva la fecha para determinar la vigencia de esta condición del emisor en los comprobantes."
  },
  [HELP_KEYS.COMPANY_FISCAL_ACCOUNTING_REQUIRED]: {
    i18nPrefix: "help.companyFiscal.accountingRequired",
    title: "Obligación de llevar contabilidad",
    short: "Indica si la empresa está obligada a llevar contabilidad. Actívela cuando esa obligación conste en su información fiscal vigente.",
    long: "El ERP registra la obligación fiscal para los comprobantes. El modo de operación contable se configura por separado."
  },
  [HELP_KEYS.COMPANY_FISCAL_FOREIGN_TRADE]: {
    i18nPrefix: "help.companyFiscal.foreignTrade",
    title: "Comercio exterior",
    short: "Indica que la empresa realiza operaciones de comercio exterior. Actívela si corresponde a su actividad.",
    long: "El ERP guarda este indicador en el perfil fiscal. No activa, desactiva ni oculta las retenciones de renta o IVA."
  },
  [HELP_KEYS.COMPANY_FISCAL_WITHHOLDS_RENTA]: {
    i18nPrefix: "help.companyFiscal.withholdsRenta",
    title: "Retención de renta",
    short: "Indicador operativo de que la empresa practica retenciones de impuesto a la renta. Actívelo cuando corresponda a sus obligaciones y operaciones.",
    long: "El ERP lo consulta para evaluar si procede retener renta en compras, junto con las demás reglas aplicables. Es independiente de comercio exterior y de IVA; no acredita una designación oficial del SRI como agente de retención."
  },
  [HELP_KEYS.COMPANY_FISCAL_WITHHOLDS_VAT]: {
    i18nPrefix: "help.companyFiscal.withholdsVat",
    title: "Retención de IVA",
    short: "Indicador operativo de que la empresa practica retenciones de IVA. Actívelo cuando corresponda a sus obligaciones y operaciones.",
    long: "El ERP lo consulta para evaluar si procede retener IVA en compras, junto con las demás reglas aplicables. Es independiente de comercio exterior y de renta; no acredita una designación oficial del SRI como agente de retención."
  },

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
  [HELP_KEYS.SALES_SRI_PAYMENT_METHOD_DEFAULT]: {
    title: "Forma Pago SRI por defecto",
    short: "Se usa solo si una forma de cobro no tiene mapeo SRI configurado.",
    long: "Cada forma de cobro (Efectivo, Tarjeta, Transferencia, etc.) puede tener su propio código SRI configurado en Métodos de Pago. Este valor de cabecera es el respaldo que se aplica únicamente cuando esa forma de cobro no tiene su propio mapeo.",
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
    title: "Efectivo recibido",
    short: "Dinero en efectivo que el cliente entrega en caja.",
    long: "Se usa solo para calcular Falta / Pago exacto / Vuelto; no cambia el monto aplicado a la factura.",
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
    short:
      "Retenciones de IVA y Renta según el proveedor y la empresa. Se definen en el borrador y se emiten al confirmar la compra; el registro ante el SRI se hace después.",
  },
  [HELP_KEYS.PURCHASES_PAYMENT_SCHEDULE]: {
    title: "Plazos de pago",
    short: "Cuotas y fechas de vencimiento de esta compra a crédito.",
  },
  [HELP_KEYS.PURCHASES_CREDIT_NOTE]: {
    title: "Nota de crédito",
    short: "Tipo de nota de crédito según si el origen es una compra registrada o un XML recibido.",
  },
  [HELP_KEYS.PURCHASES_RECEPTION_OVERVIEW]: {
    title: "¿Qué puede hacer aquí?",
    short:
      "Aquí puede consultar el XML, validar el proveedor y procesar cada comprobante como Compra, Gasto o Nota de crédito de compra.",
  },
  [HELP_KEYS.ACCOUNTING_REPORTS_GENERAL_JOURNAL]: {
    i18nPrefix: "help.accountingReports.generalJournal",
    title: "Libro Diario: qué es y cómo se lee",
    short: "¿Qué es? El registro cronológico de todos los asientos contabilizados: cada operación del ERP con las cuentas que afecta.",
    long: "¿Cómo se lee? Cada bloque es un asiento: su encabezado indica número, fecha, operación y documento de origen; debajo, el código y la cuenta de cada línea con su importe en Debe o en Haber, y el total del asiento, donde Debe y Haber son iguales. El pie suma los asientos de la página indicada.",
  },
  [HELP_KEYS.ACCOUNTING_REPORTS_GENERAL_LEDGER]: {
    i18nPrefix: "help.accountingReports.generalLedger",
    title: "Libro Mayor: qué es y cómo se lee",
    short: "¿Qué es? El historial de cada cuenta: su saldo inicial, los movimientos del período y el saldo con el que termina.",
    long: "¿Cómo se lee? Elija una cuenta del índice para ver su mayor: parte del saldo inicial, cada movimiento muestra fecha, asiento, referencia, Debe, Haber y el saldo acumulado después de él, y termina en el saldo final. Los saldos siguen la naturaleza de la cuenta: un valor negativo indica saldo contrario a su naturaleza.",
  },
  [HELP_KEYS.ACCOUNTING_REPORTS_TRIAL_BALANCE]: {
    i18nPrefix: "help.accountingReports.trialBalance",
    title: "Balance de Comprobación: qué es y cómo se lee",
    short: "¿Qué es? La matriz de todas las cuentas con sus saldos y movimientos, que comprueba que lo registrado en el Debe es igual a lo registrado en el Haber.",
    long: "¿Cómo se lee? Por cuenta, agrupada por tipo: saldo inicial, movimientos del período y saldo final, en columnas deudoras y acreedoras. La fila Comprobación indica si los movimientos cuadran (Debe = Haber) y si los totales de saldo inicial y de saldo final son iguales entre Deudor y Acreedor.",
  },
  [HELP_KEYS.ACCOUNTING_REPORTS_INCOME_STATEMENT]: {
    i18nPrefix: "help.accountingReports.incomeStatement",
    title: "Estado de Resultados: qué es y cómo se lee",
    short: "¿Qué es? El resultado económico del período: cuánto se ganó o se perdió con las operaciones contabilizadas.",
    long: "¿Cómo se lee? Se lee de arriba hacia abajo: Ingresos (−) Costos (=) Utilidad bruta (−) Gastos (=) Utilidad o pérdida neta. La columna Detalle muestra cada cuenta y la columna Total, el total de cada concepto. Solo incluye lo contabilizado en el período, sin saldo inicial; un resultado negativo se presenta como pérdida.",
  },
  [HELP_KEYS.ACCOUNTING_REPORTS_BALANCE_SHEET]: {
    i18nPrefix: "help.accountingReports.balanceSheet",
    title: "Balance General: qué es y cómo se lee",
    short: "¿Qué es? La situación financiera de la empresa a la fecha de corte: lo que tiene (Activo), lo que debe (Pasivo) y lo que pertenece a sus propietarios (Patrimonio).",
    long: "¿Cómo se lee? Saldos acumulados hasta la fecha de corte. Las cuentas aparecen bajo su grupo del Plan de Cuentas, con su importe en Detalle y el total de cada sección en Total. Debe cumplirse Activo = Pasivo + Patrimonio: la fila Comprobación muestra el resultado y la diferencia. Patrimonio incluye los resultados aún no cerrados, calculados por el sistema contable.",
  },
  [HELP_KEYS.ACCOUNTING_POSTING_RULES_OVERVIEW]: {
    i18nPrefix: "help.accountingPostingRules.overview",
    title: "Reglas contables: qué son",
    short: "Cada regla indica qué cuentas usa el ERP para contabilizar automáticamente una operación de un módulo, por ejemplo una venta o un depósito bancario.",
    long: "Hay una regla por operación. Fija: todas sus cuentas se configuran aquí. Mixta: aquí se configura una parte y el módulo origen aporta el resto. Habilitadora: el módulo origen aporta todas las cuentas y la regla solo permite contabilizar. Una operación sin regla, o con su regla inactiva, no se contabiliza.",
  },
  [HELP_KEYS.ACCOUNTING_POSTING_RULES_ENTRY]: {
    i18nPrefix: "help.accountingPostingRules.entry",
    title: "Asiento que genera: cómo se lee",
    short: "Cada fila es una línea del asiento, en el Debe o en el Haber. «Cuenta fija» es la cuenta configurada en esta regla; «Aporta el origen» es una cuenta que el módulo origen determina en cada operación.",
    long: "La base del importe indica qué valor del documento lleva cada línea fija, por ejemplo el subtotal o el IVA. Las cuentas del origen salen de la configuración de ese módulo (caja, banco, motivo, medio de pago o subcategoría), no de esta pantalla. «Editar líneas» cambia solo las líneas fijas y afecta únicamente contabilizaciones futuras.",
  },
  [HELP_KEYS.ACCOUNTING_POSTING_RULES_STATUS]: {
    i18nPrefix: "help.accountingPostingRules.status",
    title: "Activar o desactivar una regla",
    short: "Una regla activa se usa en cada nueva operación de su tipo. Al desactivarla, esas operaciones no podrán contabilizarse hasta reactivar una regla válida.",
    long: "Los asientos ya publicados no cambian. Para reactivarla, la regla debe volver a cumplir la validación: cuentas activas que admiten movimientos y las líneas de Debe y Haber que exige la operación.",
  },
};
