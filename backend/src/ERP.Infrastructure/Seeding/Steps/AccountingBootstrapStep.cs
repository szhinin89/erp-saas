using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.InitialLoad.Constants;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// ACCOUNTING-INITIAL-CHART-SEED-11 / ACCOUNTING-BASE-CHART-TEMPLATE-13: sin este step, una
/// Company nueva nunca tenía Plan de Cuentas ni un AccountingPeriod abierto — el diagnóstico
/// previo (ACCOUNTING-DATA-SEED-AND-SMOKE-10G) confirmó que esto deja al Posting Engine sin
/// ningún <c>PostingRule</c> posible (no hay cuentas a las que apuntar) y, por lo tanto, sin
/// ningún <c>JournalEntry</c> real, aun con ventas/compras operando con normalidad.
///
/// ACCOUNTING-BASE-CHART-TEMPLATE-13 amplía el seed inicial de 13 cuentas hoja a una plantilla
/// retail Ecuador jerárquica: cuentas padre/resumen con <c>AllowsPosting=false</c> y cuentas
/// operativas finales con <c>AllowsPosting=true</c>. La plantilla usa como blueprint el plan real
/// de Sumak, pero no lo copia literalmente: evita cajas/locales/bancos/personas/proveedores
/// específicos y conserva los códigos operativos del seed mínimo previo para que las
/// <c>PostingRule</c> existentes y empresas ya configuradas no se rompan. Idempotente por Code
/// (cuentas) y por FiscalYear (período): solo crea lo faltante, nunca duplica ni toca cuentas/
/// períodos ya existentes.
///
/// ACCOUNTING-POSTING-RULES-SEED-11B: además del plan de cuentas, siembra las
/// <see cref="PostingRule"/>/<see cref="PostingRuleLine"/> mínimas para los hechos contables que
/// hoy tienen traductor real (ver <c>MinimalPostingRules</c>; incluye
/// SUPPLIER-PAYMENTS-POSTING-15D: <c>Payables/SupplierPaymentConfirmed</c>) — sin esto, el Posting Engine
/// resolvía "RULE_NOT_FOUND" (fail-closed, <c>PostingRuleResolver</c>) para toda venta/compra/
/// cobro/pago real, aun con Plan de Cuentas ya sembrado (diagnóstico ACCOUNTING-DATA-SEED-AND-
/// SMOKE-10G). <c>Purchases/PurchaseCreditNoteCancelled</c> NO tiene regla — ese hecho reversa el
/// <c>JournalEntry</c> original vía <c>ReverseJournalEntryCommand</c>
/// (<c>PurchaseCreditNoteCancelledPostingTranslator</c>), nunca resuelve una <c>PostingRule</c>
/// nueva. Idempotente por (SourceModule, FactType): solo crea las reglas que faltan, nunca
/// duplica ni toca reglas ya editadas por el admin (mismo criterio que el plan de cuentas). No
/// genera ningún asiento retroactivo — configuración únicamente.
///
/// RETENTIONS-POSTING-RULE-SEED-01H: agrega la <c>PostingRule</c> mínima que faltaba para
/// <c>Retentions/DocumentIssued</c> (usada por <c>RetentionDocumentIssuedPostingTranslator</c>,
/// confirmado leyendo su código real) — sin ella, todo gasto con retención fallaba fail-closed en
/// cualquier entorno (confirmado en RETENTIONS-EXPENSES-E2E-QA-01G, que hasta ahora sembraba esa
/// regla como fixture local del test). Reclasifica el monto retenido de "CxP proveedor" (Debe) a
/// "Retenciones IVA por pagar" (Haber), igual que el ejemplo conceptual de
/// docs/decisions/RETENTIONS-MODULE-DESIGN-01.md § "Impacto contable". 01H sembraba 2 líneas
/// (ambas <see cref="PostingAmountKind.Retention"/>, monto combinado IVA+Renta) contra la cuenta
/// genérica de IVA ("2.1.02.002"), dejando explícitamente como deuda conocida que una retención de
/// Renta quedaría mal clasificada hasta que el traductor separara los montos por tipo.
///
/// RETENTIONS-TAX-COMPONENT-POSTING-02C: cierra esa deuda. El traductor ya transporta
/// <c>PostingFact.RetainedVatAmount</c>/<c>RetainedIncomeAmount</c> por separado (ver
/// <c>RetentionDocumentIssuedPostingTranslator</c>), así que la regla pasa a 3 líneas: Debe CxP
/// proveedor (<see cref="PostingAmountKind.Retention"/>, total — sin cambios), Haber
/// "2.1.02.002 Retenciones IVA por pagar" (<see cref="PostingAmountKind.RetentionVat"/>), Haber
/// "2.1.02.003 Retenciones renta por pagar" (<see cref="PostingAmountKind.RetentionIncome"/>) —
/// ambas cuentas ya existían sembradas en <c>RetailChart</c> desde antes de 01H, "2.1.02.003" solo
/// estaba sin usar.
///
/// ERP-POSTING-RULES-EXPENSES-RETENTIONS-SEED-01: agrega la <c>PostingRule</c> mínima que faltaba
/// por completo para <c>Expenses/DocumentConfirmed</c> — a diferencia del gap de 01H (regla
/// incompleta), esta nunca tuvo entrada en <c>MinimalPostingRules</c>, detectado en preparación QA
/// (RETENTIONS-SRI-SANDBOX-SEED-04F-1) al confirmar el primer gasto de una empresa de prueba.
///
/// PURCHASE-RETURN-ACCOUNTING-NOT-GENERATED-01: mismo tipo de gap, ahora para
/// <c>Purchases/PurchaseReturn</c> — <c>PurchaseReturnAuthorizedPostingTranslator</c> (P0-02 Fase 6)
/// siempre existió y siempre publicó su <c>PostingFact</c>, pero <c>MinimalPostingRules</c> nunca
/// tuvo la clave correspondiente, así que toda autorización de devolución de compra, en toda
/// company, fallaba fail-closed ("RULE_NOT_FOUND") de forma silenciosa (el traductor solo loguea
/// un warning — <c>Authorize()</c> ya movió Kardex/CxP/SupplierCredit y no se revierte). Reportado
/// en producción: devolución autorizada con Kardex movido pero sin JournalEntry.
///
/// PURCHASE-SUPPLIER-CREDIT-APPLIED-POSTING-RULE-01: mismo tipo de gap, ahora para
/// <c>Purchases/SupplierCreditApplied</c> — <c>SupplierCreditAppliedPostingTranslator</c> (P0-02
/// Fase 7) siempre existió y siempre publicó su <c>PostingFact</c> (un único monto en
/// <c>GrandTotal</c>), pero <c>MinimalPostingRules</c> nunca tuvo la clave correspondiente: aplicar
/// un <c>SupplierCredit</c> contra una CxP destino (<c>ApplySupplierCreditUseCases</c>) persistía
/// con normalidad (el movimiento y la reducción de la CxP destino no dependen del posting) pero el
/// asiento fallaba fail-closed "RULE_NOT_FOUND" en silencio (mismo criterio de log-only warning que
/// el resto de traductores de este módulo — nunca revierte la aplicación ya persistida). Hecho
/// contable simple de 2 líneas (§19.1, doc comment del traductor): Debe CxP proveedores (reduce lo
/// exigible de la factura destino), Haber "1.1.03.004 Anticipos a proveedores" (reduce el crédito a
/// favor ya reconocido como activo — misma cuenta que <c>SupplierCredit</c> usa en
/// "Purchases/PurchaseReturn").
///
/// ACCOUNTING-CONTINUITY-04: las anulaciones de devolución de compra y las reversas de aplicación
/// y reembolso de saldo a favor espejan el asiento histórico (Accounting/Reversal); sus reglas
/// "PurchaseReturnCancelled", "SupplierCreditApplicationReversed" y "SupplierCreditRefundReversed"
/// ya no se siembran.
/// </summary>
public sealed partial class AccountingBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.Accounting;

    private readonly ErpDbContext _db;
    private readonly ERP.Application.Common.Services.ICompanyClock _companyClock;
    private readonly ILogger<AccountingBootstrapStep> _logger;

    public AccountingBootstrapStep(
        ErpDbContext db,
        ERP.Application.Common.Services.ICompanyClock companyClock,
        ILogger<AccountingBootstrapStep> logger
    )
    {
        _db = db;
        _companyClock = companyClock;
        _logger = logger;
    }

    private sealed record RetailAccount(
        string Code,
        string Name,
        string? ParentCode,
        AccountType Type,
        AccountNature Nature,
        bool AllowsPosting
    );

    // ACCOUNTING-CHART-CANONICAL-HIERARCHY-01: 92 -> 102. El código contable manda la jerarquía
    // (regla canónica: "1.1.01" implica padre "1.1", "1.1.01.001" implica padre "1.1.01"). La
    // auditoría ACCOUNTING-CHART-LOAD-HIERARCHY-AUDIT-01 encontró 10 hojas de 4 segmentos
    // parentadas directamente bajo un padre de 2 segmentos (saltándose el nivel de 3 segmentos
    // intermedio) — se agregan las 10 cuentas agrupadoras intermedias faltantes
    // (3.1.01/3.1.02/3.1.03/4.2.01/5.1.01/6.1.01/6.2.01/6.3.01/6.4.01/6.5.01), todas
    // AllowsPosting=false y ninguna referenciada por MinimalPostingRules.
    // IL-7A: 105 -> 107 — agrega 3.1.04/3.1.04.001 "Saldos de apertura".
    // ACCOUNTING-10: 107 -> 109 — agrega 4.1.03/4.1.03.001 "Devoluciones en ventas".
    public const int RetailChartAccountCount = 109;

    private static readonly IReadOnlyList<RetailAccount> RetailChart =
    [
        new("1", "Activo", null, AccountType.Asset, AccountNature.Debit, false),
        new("1.1", "Activo corriente", "1", AccountType.Asset, AccountNature.Debit, false),
        new(
            "1.1.01",
            "Efectivo y equivalentes",
            "1.1",
            AccountType.Asset,
            AccountNature.Debit,
            false
        ),
        new("1.1.01.001", "Caja general", "1.1.01", AccountType.Asset, AccountNature.Debit, true),
        new("1.1.01.002", "Caja chica", "1.1.01", AccountType.Asset, AccountNature.Debit, true),
        new(
            "1.1.01.003",
            "Fondos por depositar",
            "1.1.01",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new("1.1.02", "Bancos", "1.1", AccountType.Asset, AccountNature.Debit, false),
        new(
            "1.1.02.001",
            "Bancos cuenta corriente",
            "1.1.02",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new(
            "1.1.02.002",
            "Bancos cuenta ahorros",
            "1.1.02",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new(
            "1.1.03",
            "Cuentas y documentos por cobrar",
            "1.1",
            AccountType.Asset,
            AccountNature.Debit,
            false
        ),
        new(
            "1.1.03.001",
            "Cuentas por cobrar clientes",
            "1.1.03",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new(
            "1.1.03.002",
            "Cuentas por cobrar tarjetas credito/debito",
            "1.1.03",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new(
            "1.1.03.003",
            "Otras cuentas por cobrar",
            "1.1.03",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new(
            "1.1.03.004",
            "Anticipos a proveedores",
            "1.1.03",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new("1.1.04", "Inventarios", "1.1", AccountType.Asset, AccountNature.Debit, false),
        new(
            "1.1.04.001",
            "Inventario mercaderias",
            "1.1.04",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new(
            "1.1.04.002",
            "Inventario en transito",
            "1.1.04",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new(
            "1.1.04.003",
            "Inventario por ajustes",
            "1.1.04",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new("1.1.05", "Impuestos a favor", "1.1", AccountType.Asset, AccountNature.Debit, false),
        new(
            "1.1.05.001",
            "IVA credito tributario",
            "1.1.05",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new(
            "1.1.05.002",
            "IVA retenido por clientes",
            "1.1.05",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new(
            "1.1.05.003",
            "Retenciones renta a favor",
            "1.1.05",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new("1.1.06", "Pagos anticipados", "1.1", AccountType.Asset, AccountNature.Debit, false),
        new(
            "1.1.06.001",
            "Seguros pagados por anticipado",
            "1.1.06",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new("1.2", "Activo no corriente", "1", AccountType.Asset, AccountNature.Debit, false),
        new(
            "1.2.01",
            "Propiedad, planta y equipo",
            "1.2",
            AccountType.Asset,
            AccountNature.Debit,
            false
        ),
        new(
            "1.2.01.001",
            "Equipos de computacion",
            "1.2.01",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new(
            "1.2.01.002",
            "Muebles y enseres",
            "1.2.01",
            AccountType.Asset,
            AccountNature.Debit,
            true
        ),
        new("2", "Pasivo", null, AccountType.Liability, AccountNature.Credit, false),
        new("2.1", "Pasivo corriente", "2", AccountType.Liability, AccountNature.Credit, false),
        new(
            "2.1.01",
            "Cuentas y documentos por pagar",
            "2.1",
            AccountType.Liability,
            AccountNature.Credit,
            false
        ),
        new(
            "2.1.01.001",
            "Cuentas por pagar proveedores",
            "2.1.01",
            AccountType.Liability,
            AccountNature.Credit,
            true
        ),
        new(
            "2.1.01.002",
            "Anticipos de clientes",
            "2.1.01",
            AccountType.Liability,
            AccountNature.Credit,
            true
        ),
        new("2.1.02", "IVA por pagar", "2.1", AccountType.Liability, AccountNature.Credit, false),
        new(
            "2.1.02.001",
            "IVA cobrado en ventas",
            "2.1.02",
            AccountType.Liability,
            AccountNature.Credit,
            true
        ),
        new(
            "2.1.02.002",
            "Retenciones IVA por pagar",
            "2.1.02",
            AccountType.Liability,
            AccountNature.Credit,
            true
        ),
        new(
            "2.1.02.003",
            "Retenciones renta por pagar",
            "2.1.02",
            AccountType.Liability,
            AccountNature.Credit,
            true
        ),
        new(
            "2.1.03",
            "Impuestos especiales por pagar",
            "2.1",
            AccountType.Liability,
            AccountNature.Credit,
            false
        ),
        new(
            "2.1.03.001",
            "ICE por pagar",
            "2.1.03",
            AccountType.Liability,
            AccountNature.Credit,
            true
        ),
        new(
            "2.1.03.002",
            "IRBP por pagar",
            "2.1.03",
            AccountType.Liability,
            AccountNature.Credit,
            true
        ),
        new(
            "2.1.03.003",
            "Impuesto a la renta por pagar",
            "2.1.03",
            AccountType.Liability,
            AccountNature.Credit,
            true
        ),
        new(
            "2.1.04",
            "Nomina y beneficios por pagar",
            "2.1",
            AccountType.Liability,
            AccountNature.Credit,
            false
        ),
        new(
            "2.1.04.001",
            "Sueldos por pagar",
            "2.1.04",
            AccountType.Liability,
            AccountNature.Credit,
            true
        ),
        new("3", "Patrimonio", null, AccountType.Equity, AccountNature.Credit, false),
        new("3.1", "Capital y resultados", "3", AccountType.Equity, AccountNature.Credit, false),
        new("3.1.01", "Capital social", "3.1", AccountType.Equity, AccountNature.Credit, false),
        new("3.1.01.001", "Capital", "3.1.01", AccountType.Equity, AccountNature.Credit, true),
        new(
            "3.1.02",
            "Resultados de ejercicios anteriores",
            "3.1",
            AccountType.Equity,
            AccountNature.Credit,
            false
        ),
        new(
            "3.1.02.001",
            "Resultados acumulados",
            "3.1.02",
            AccountType.Equity,
            AccountNature.Credit,
            true
        ),
        new(
            "3.1.03",
            "Resultado del ejercicio actual",
            "3.1",
            AccountType.Equity,
            AccountNature.Credit,
            false
        ),
        new(
            "3.1.03.001",
            "Resultado del ejercicio",
            "3.1.03",
            AccountType.Equity,
            AccountNature.Credit,
            true
        ),
        // IL-7A — cuenta puente patrimonial de la apertura (contrapartida de inventario, CxC y CxP
        // iniciales y del futuro asiento manual de apertura de caja/bancos/otros). Específica:
        // nunca Capital ni Resultados acumulados. 3.1.04 estaba libre en el plan vigente; una
        // empresa con 3.1.04/3.1.04.001 ya usados con otro sentido no recibe ni la cuenta ni las
        // reglas InitialLoad/* (ver OpeningBalanceAccountConflict).
        new(
            OpeningBalanceGroupAccountCode,
            OpeningBalanceAccountName,
            "3.1",
            AccountType.Equity,
            AccountNature.Credit,
            false
        ),
        new(
            OpeningBalanceAccountCode,
            OpeningBalanceAccountName,
            OpeningBalanceGroupAccountCode,
            AccountType.Equity,
            AccountNature.Credit,
            true
        ),
        new("4", "Ingresos", null, AccountType.Income, AccountNature.Credit, false),
        new("4.1", "Ingresos operacionales", "4", AccountType.Income, AccountNature.Credit, false),
        new(
            "4.1.01",
            "Ventas de mercaderia",
            "4.1",
            AccountType.Income,
            AccountNature.Credit,
            false
        ),
        new(
            "4.1.01.001",
            "Ventas tarifa general",
            "4.1.01",
            AccountType.Income,
            AccountNature.Credit,
            true
        ),
        new(
            "4.1.01.002",
            "Ventas tarifa 0%",
            "4.1.01",
            AccountType.Income,
            AccountNature.Credit,
            true
        ),
        new(
            "4.1.01.003",
            "Ventas exentas de IVA",
            "4.1.01",
            AccountType.Income,
            AccountNature.Credit,
            true
        ),
        new(
            "4.1.01.004",
            "Ventas no objeto de IVA",
            "4.1.01",
            AccountType.Income,
            AccountNature.Credit,
            true
        ),
        new(
            "4.1.01.005",
            "Ventas de servicios retail",
            "4.1.01",
            AccountType.Income,
            AccountNature.Credit,
            true
        ),
        new(
            "4.1.02",
            "Descuentos sobre ventas",
            "4.1",
            AccountType.Income,
            AccountNature.Credit,
            false
        ),
        new(
            "4.1.02.001",
            "Descuentos concedidos en ventas",
            "4.1.02",
            AccountType.Income,
            AccountNature.Credit,
            true
        ),
        // ACCOUNTING-10 — contra-ingreso de Sales/SalesReturn (mismo patrón que 4.1.02).
        new(
            "4.1.03",
            "Devoluciones en ventas",
            "4.1",
            AccountType.Income,
            AccountNature.Credit,
            false
        ),
        new(
            "4.1.03.001",
            "Devoluciones en ventas",
            "4.1.03",
            AccountType.Income,
            AccountNature.Credit,
            true
        ),
        new("4.2", "Otros ingresos", "4", AccountType.Income, AccountNature.Credit, false),
        new(
            "4.2.01",
            "Ajustes y diferencias positivas",
            "4.2",
            AccountType.Income,
            AccountNature.Credit,
            false
        ),
        new(
            "4.2.01.001",
            "Ingresos por ajustes positivos de inventario",
            "4.2.01",
            AccountType.Income,
            AccountNature.Credit,
            true
        ),
        // PURCHASE-CREDIT-NOTE-DISCOUNT-POSTING-ACCOUNT-01: NC de compra tipo Descuento/Promoción
        // reduce CxP pero nunca mueve inventario/Kardex (a diferencia de NC tipo Devolución, que sí
        // afecta "1.1.04.001 Inventario mercaderias" vía PurchaseReturn) — necesitaba una cuenta
        // propia de contrapartida, nunca inventario. Mismo grupo económico que "4.2.01.001" (ajuste/
        // diferencia reconocida sin mover inventario), llena el hueco de numeración ya reservado.
        new(
            "4.2.01.002",
            "Descuentos obtenidos en compras",
            "4.2.01",
            AccountType.Income,
            AccountNature.Credit,
            true
        ),
        new(
            "4.2.01.003",
            "Diferencias positivas de caja",
            "4.2.01",
            AccountType.Income,
            AccountNature.Credit,
            true
        ),
        new("5", "Costos", null, AccountType.Cost, AccountNature.Debit, false),
        new("5.1", "Costo de ventas", "5", AccountType.Cost, AccountNature.Debit, false),
        new(
            "5.1.01",
            "Costo directo de ventas",
            "5.1",
            AccountType.Cost,
            AccountNature.Debit,
            false
        ),
        new(
            "5.1.01.001",
            "Costo de ventas mercaderia",
            "5.1.01",
            AccountType.Cost,
            AccountNature.Debit,
            true
        ),
        new(
            "5.1.01.002",
            "Costo de servicios retail",
            "5.1.01",
            AccountType.Cost,
            AccountNature.Debit,
            true
        ),
        new(
            "5.1.01.003",
            "Costo ICE/IRBP no recuperable",
            "5.1.01",
            AccountType.Cost,
            AccountNature.Debit,
            true
        ),
        // ACCOUNTING-CHART-CANONICAL-HIERARCHY-01: padre corregido de "5" a "5.1" — por código
        // ("5.1.02" son 3 segmentos) el padre canónico es el prefijo inmediato "5.1" (Costo de
        // ventas, ya existente), no la raíz "5". Bug de dato preexistente, sin tocar código/nombre.
        new("5.1.02", "Ajustes de inventario", "5.1", AccountType.Cost, AccountNature.Debit, false),
        new(
            "5.1.02.001",
            "Mermas y faltantes de inventario",
            "5.1.02",
            AccountType.Cost,
            AccountNature.Debit,
            true
        ),
        new(
            "5.1.02.002",
            "Descuadres negativos de inventario",
            "5.1.02",
            AccountType.Cost,
            AccountNature.Debit,
            true
        ),
        new("6", "Gastos", null, AccountType.Expense, AccountNature.Debit, false),
        new("6.1", "Gastos administrativos", "6", AccountType.Expense, AccountNature.Debit, false),
        new(
            "6.1.01",
            "Gastos administrativos operativos",
            "6.1",
            AccountType.Expense,
            AccountNature.Debit,
            false
        ),
        new(
            "6.1.01.001",
            "Gastos administrativos generales",
            "6.1.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new(
            "6.1.01.002",
            "Suministros de oficina",
            "6.1.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new(
            "6.1.01.003",
            "Servicios basicos",
            "6.1.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new("6.1.01.004", "Arriendos", "6.1.01", AccountType.Expense, AccountNature.Debit, true),
        new(
            "6.1.01.005",
            "Honorarios profesionales",
            "6.1.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new(
            "6.1.01.006",
            "Mantenimiento y reparaciones",
            "6.1.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        // EXPENSES-CATALOG-BOOTSTRAP-09-FIX — faltaban cuentas propias para 2 de las 7 categorías
        // que ExpensesCatalogBootstrapStep necesita bajo "Gastos administrativos" (el plan solo
        // tenía 6 cuentas hoja para 7 categorías del catálogo de gastos, forzando un mapeo
        // desplazado/incorrecto). Aditivo: no reemplaza ni renombra ninguna cuenta existente.
        new(
            "6.1.01.007",
            "Tecnologia y sistemas",
            "6.1.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new(
            "6.1.01.008",
            "Movilizacion y transporte",
            "6.1.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new("6.2", "Gastos de venta", "6", AccountType.Expense, AccountNature.Debit, false),
        new(
            "6.2.01",
            "Gastos comerciales directos",
            "6.2",
            AccountType.Expense,
            AccountNature.Debit,
            false
        ),
        new(
            "6.2.01.001",
            "Publicidad y marketing",
            "6.2.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new(
            "6.2.01.002",
            "Comisiones de venta",
            "6.2.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new(
            "6.2.01.003",
            "Empaques, fundas y suministros de venta",
            "6.2.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new(
            "6.2.01.004",
            "Transporte y entregas a clientes",
            "6.2.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new("6.3", "Gastos financieros", "6", AccountType.Expense, AccountNature.Debit, false),
        new(
            "6.3.01",
            "Gastos financieros directos",
            "6.3",
            AccountType.Expense,
            AccountNature.Debit,
            false
        ),
        new(
            "6.3.01.001",
            "Comisiones bancarias",
            "6.3.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new(
            "6.3.01.002",
            "Comisiones tarjetas credito/debito",
            "6.3.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new(
            "6.3.01.003",
            "Intereses financieros",
            "6.3.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new(
            "6.4",
            "Impuestos y no deducibles",
            "6",
            AccountType.Expense,
            AccountNature.Debit,
            false
        ),
        new(
            "6.4.01",
            "Impuestos, multas y no deducibles",
            "6.4",
            AccountType.Expense,
            AccountNature.Debit,
            false
        ),
        new(
            "6.4.01.001",
            "Impuestos no recuperables",
            "6.4.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new(
            "6.4.01.002",
            "Multas y gastos no deducibles",
            "6.4.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new(
            "6.5",
            "Descuadres y perdidas operativas",
            "6",
            AccountType.Expense,
            AccountNature.Debit,
            false
        ),
        new(
            "6.5.01",
            "Descuadres y mermas operativas",
            "6.5",
            AccountType.Expense,
            AccountNature.Debit,
            false
        ),
        new(
            "6.5.01.001",
            "Descuadres de caja",
            "6.5.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
        new(
            "6.5.01.002",
            "Mermas retail",
            "6.5.01",
            AccountType.Expense,
            AccountNature.Debit,
            true
        ),
    ];

    static AccountingBootstrapStep()
    {
        if (RetailChart.Count != RetailChartAccountCount)
            throw new InvalidOperationException(
                $"Retail chart template count mismatch. Expected {RetailChartAccountCount}, got {RetailChart.Count}."
            );
    }

    private sealed record MinimalPostingRuleLine(
        string AccountCode,
        AccountNature Nature,
        PostingAmountKind AmountKind
    );

    // ACCOUNTING-11: la clave sale siempre de PostingFactCatalog (autoridad única de hechos
    // soportados); este seed solo aporta las cuentas por defecto de RetailChart.
    private sealed record MinimalPostingRule(
        string SourceModule,
        string FactType,
        IReadOnlyList<MinimalPostingRuleLine> Lines
    )
    {
        public MinimalPostingRule(PostingFactDefinition fact, IReadOnlyList<MinimalPostingRuleLine> lines)
            : this(fact.SourceModule, fact.FactType, lines) { }
    }

    private sealed record AccountSeedLookup(Guid Id, bool IsActive, bool AllowsPosting);

    // ACCOUNTING-POSTING-RULES-SEED-11B: un (SourceModule, FactType) por cada traductor real
    // confirmado leyendo el código fuente de cada uno (ver doc comment de la clase) —
    // Purchases/PurchaseCreditNoteCancelled queda deliberadamente fuera (reversa el asiento
    // original, no resuelve regla nueva). Mapeos de cuentas genéricos usando exclusivamente las
    // cuentas de RetailChart — ninguna cuenta ni empresa hardcodeada. Líneas cuyo
    // PostingAmountKind resuelve en 0 (p. ej. ICE/IRBPNR ausentes) se omiten automáticamente en
    // JournalFactory, así que la misma regla sirve con y sin esos componentes.
    private static readonly IReadOnlyList<MinimalPostingRule> MinimalPostingRules =
    [
        // SALES-CASH-VS-RECEIVABLE-POSTING-SPLIT-AND-CANCEL-REVERSAL-01 Lote 3 — antes, la única
        // línea de Debe acreditaba el 100% de GrandTotal a CxC ("1.1.03.001"), sin importar cuánto
        // se cobró realmente en la venta (una venta 100% contado generaba una CxC ficticia). Se
        // reemplaza por dos líneas de Debe condicionales: "1.1.01.001 Caja general" por
        // CashApplied (dinero real cobrado — mismo criterio/cuenta que "Finance"/"CollectionApplied"
        // abajo) y "1.1.03.001 Cuentas por cobrar clientes" por PendingBalance (saldo pendiente).
        // JournalFactory ya omite automáticamente la línea cuyo monto resuelto es 0 (mecanismo
        // existente, no nuevo) — una venta 100% contado nunca genera línea de CxC y una venta 100%
        // crédito nunca genera línea de Caja.
        new(
            PostingFactCatalog.SalesInvoiceIssued,
            [
                new("1.1.01.001", AccountNature.Debit, PostingAmountKind.CashApplied),
                new("1.1.03.001", AccountNature.Debit, PostingAmountKind.PendingBalance),
                new("4.1.02.001", AccountNature.Debit, PostingAmountKind.Discount),
                new("4.1.01.001", AccountNature.Credit, PostingAmountKind.Subtotal),
                new("2.1.02.001", AccountNature.Credit, PostingAmountKind.TaxVat),
                new("2.1.03.001", AccountNature.Credit, PostingAmountKind.TaxIce),
                new("2.1.03.002", AccountNature.Credit, PostingAmountKind.TaxIrbpnr),
            ]
        ),
        // ACCOUNTING-10 — espejo de InvoiceIssued. Destino del reembolso desde RefundAllocations:
        // Caja (CashApplied; la cuenta la sustituye la caja de la sesión del SaleRefund) y crédito
        // CxC (PendingBalance); la línea en 0 se omite. El COGS revertido es otro hecho
        // (CostOfGoodsSoldReversed).
        new(
            PostingFactCatalog.SalesReturn,
            [
                new("4.1.03.001", AccountNature.Debit, PostingAmountKind.Subtotal),
                new("2.1.02.001", AccountNature.Debit, PostingAmountKind.TaxVat),
                new("2.1.03.001", AccountNature.Debit, PostingAmountKind.TaxIce),
                new("2.1.03.002", AccountNature.Debit, PostingAmountKind.TaxIrbpnr),
                new("4.1.02.001", AccountNature.Credit, PostingAmountKind.Discount),
                new("1.1.01.001", AccountNature.Credit, PostingAmountKind.CashApplied),
                new("1.1.03.001", AccountNature.Credit, PostingAmountKind.PendingBalance),
            ]
        ),
        new(
            PostingFactCatalog.CostOfGoodsSold,
            [
                new("5.1.01.001", AccountNature.Debit, PostingAmountKind.HistoricalCost),
                new("1.1.04.001", AccountNature.Credit, PostingAmountKind.HistoricalCost),
            ]
        ),
        new(
            PostingFactCatalog.CostOfGoodsSoldReversed,
            [
                new("1.1.04.001", AccountNature.Debit, PostingAmountKind.HistoricalCost),
                new("5.1.01.001", AccountNature.Credit, PostingAmountKind.HistoricalCost),
            ]
        ),
        new(
            PostingFactCatalog.PurchaseInvoiceReceived,
            [
                new("1.1.04.001", AccountNature.Debit, PostingAmountKind.Subtotal),
                new("1.1.05.001", AccountNature.Debit, PostingAmountKind.TaxVat),
                new("1.1.04.001", AccountNature.Debit, PostingAmountKind.TaxIce),
                new("1.1.04.001", AccountNature.Debit, PostingAmountKind.TaxIrbpnr),
                new("2.1.01.001", AccountNature.Credit, PostingAmountKind.GrandTotal),
            ]
        ),
        // PURCHASE-CREDIT-NOTE-DISCOUNT-POSTING-ACCOUNT-01: Subtotal/TaxIce acreditaban
        // "1.1.04.001 Inventario mercaderias" — incorrecto para este FactType. Esta regla es
        // EXCLUSIVA de PurchaseCreditNoteApplicationType.Discount (ver remarks del traductor:
        // Return nunca se autoriza aquí, su efecto contable es "Purchases"/"PurchaseReturn" arriba,
        // que sí mueve inventario porque PurchaseReturn sí mueve Kardex); un descuento/promoción
        // nunca mueve inventario ni Kardex, así que su contrapartida no puede ser una cuenta de
        // inventario. Ambas líneas pasan a "4.2.01.002 Descuentos obtenidos en compras" (mismo
        // AmountKind/Nature, solo cambia la cuenta) — TaxVat se mantiene en "1.1.05.001 IVA credito
        // tributario" (correcto: sí es una reversión de crédito tributario, no de inventario).
        new(
            PostingFactCatalog.PurchaseCreditNoteAuthorized,
            [
                new("2.1.01.001", AccountNature.Debit, PostingAmountKind.AppliedToPayable),
                new("4.2.01.002", AccountNature.Credit, PostingAmountKind.Subtotal),
                new("4.2.01.002", AccountNature.Credit, PostingAmountKind.TaxIce),
                new("1.1.05.001", AccountNature.Credit, PostingAmountKind.TaxVat),
            ]
        ),
        // PURCHASE-RETURN-ACCOUNTING-NOT-GENERATED-01 — "Purchases"/"PurchaseReturn" nunca tuvo
        // entrada en MinimalPostingRules, igual clase de gap que Expenses/DocumentConfirmed:
        // PurchaseReturnAuthorizedPostingTranslator existe y publica PostingFact desde P0-02 Fase 6,
        // pero PostingRuleResolver fail-closed ("RULE_NOT_FOUND") en toda company — Authorize()
        // persistía igual (Kardex/CxP/SupplierCredit ya aplicados por el propio agregado, el
        // posting fallido solo se loguea como warning, nunca revierte la autorización, ver
        // PurchaseReturnAuthorizedPostingTranslator), así que la devolución quedaba Authorized con
        // Kardex movido pero sin JournalEntry — nunca un error visible para el usuario. Espejo de
        // InvoiceReceived/PurchaseCreditNoteAuthorized + los 5 campos agregados a PostingFact en la
        // Remediación 01 de P0-02 Fase 6 (§19.1bis): AppliedToPayable/SupplierCredit reducen lo
        // exigible del proveedor (Debe), CostVarianceDebit/CostVarianceCredit son mutuamente
        // excluyentes (JournalFactory omite la que resuelve a 0 — nunca las dos a la vez),
        // HistoricalCost/TaxVat/TaxIce/TaxIrbpnr reversan exactamente lo que InvoiceReceived debitó.
        // Cuentas reutilizadas del RetailChart existente, ninguna inventada: "1.1.03.004 Anticipos a
        // proveedores" para SupplierCredit (mismo rol económico: saldo a favor frente al proveedor)
        // y "5.1.02.001 Mermas y faltantes de inventario"/"4.2.01.001 Ingresos por ajustes positivos
        // de inventario" para la variación de costo, mismo criterio ya usado por StockAdjustment.
        new(
            PostingFactCatalog.PurchaseReturn,
            [
                new("2.1.01.001", AccountNature.Debit, PostingAmountKind.AppliedToPayable),
                new("1.1.03.004", AccountNature.Debit, PostingAmountKind.SupplierCredit),
                new("5.1.02.001", AccountNature.Debit, PostingAmountKind.CostVarianceDebit),
                new("1.1.04.001", AccountNature.Credit, PostingAmountKind.HistoricalCost),
                new("1.1.05.001", AccountNature.Credit, PostingAmountKind.TaxVat),
                new("1.1.04.001", AccountNature.Credit, PostingAmountKind.TaxIce),
                new("1.1.04.001", AccountNature.Credit, PostingAmountKind.TaxIrbpnr),
                new("4.2.01.001", AccountNature.Credit, PostingAmountKind.CostVarianceCredit),
            ]
        ),
        // PURCHASE-SUPPLIER-CREDIT-APPLIED-POSTING-RULE-01 — "Purchases"/"SupplierCreditApplied"
        // nunca tuvo entrada en MinimalPostingRules, aunque SupplierCreditAppliedPostingTranslator
        // existe desde P0-02 Fase 7: aplicar un SupplierCredit contra una CxP destino persistía con
        // normalidad (ApplySupplierCreditUseCases mueve el movimiento y reduce la CxP destino sin
        // depender del posting) pero el asiento fallaba fail-closed "RULE_NOT_FOUND" en silencio.
        // Hecho de una sola línea por lado (GrandTotal, sin IVA/ICE): Debe CxP proveedores (se
        // reduce lo exigible de la factura destino), Haber "1.1.03.004 Anticipos a proveedores"
        // (se reduce el crédito a favor ya reconocido como activo — misma cuenta que
        // "Purchases"/"PurchaseReturn" usa para SupplierCredit).
        new(
            PostingFactCatalog.SupplierCreditApplied,
            [
                new("2.1.01.001", AccountNature.Debit, PostingAmountKind.GrandTotal),
                new("1.1.03.004", AccountNature.Credit, PostingAmountKind.GrandTotal),
            ]
        ),
        // ZH-SUPPLIER-CREDIT-REFUND-POSTING-02D-B — "Purchases"/"SupplierCreditRefunded": el
        // proveedor devuelve el saldo a favor. Antes el traductor usaba un FactType por destino
        // ("SupplierCreditRefunded:{código}") que nunca tuvo regla → reembolsos sin asiento. Única
        // línea fija: Haber "1.1.03.004 Anticipos a proveedores" (GrandTotal); el Debe Caja/Banco es
        // dinámico vía PostingFact.Allocations (cuenta congelada de la transacción de reembolso,
        // tomada de CashRegister/CompanyBankAccount.AccountingAccountId) — nunca una regla por
        // caja/banco. Mismo patrón que "Payables"/"SupplierPaymentConfirmed".
        new(
            PostingFactCatalog.SupplierCreditRefunded,
            [new("1.1.03.004", AccountNature.Credit, PostingAmountKind.GrandTotal)]
        ),
        // ACCOUNTING-CONTINUITY-04 — "PurchaseReturnCancelled", "SupplierCreditApplicationReversed" y
        // "SupplierCreditRefundReversed" ya no se siembran: sus anulaciones espejan el asiento
        // histórico (Accounting/Reversal) sin PostingRule. Las reglas ya existentes no se borran.
        new(
            PostingFactCatalog.CollectionApplied,
            [
                new("1.1.01.001", AccountNature.Debit, PostingAmountKind.GrandTotal),
                new("1.1.03.001", AccountNature.Credit, PostingAmountKind.GrandTotal),
            ]
        ),
        // PAYABLES-PAYMENTS-LEGACY-CLEANUP-14 — "Finance"/"SupplierPaymentApplied" (pago a
        // proveedor) se eliminó junto con RegisterPaymentCommand/SupplierPaymentAppliedPostingTranslator:
        // sin nada que dispare ese FactType, sembrar la PostingRule por empresa nueva sería
        // configuración muerta desde el día uno.
        //
        // SUPPLIER-PAYMENTS-POSTING-15D — "Payables"/"SupplierPaymentConfirmed" reemplaza esa
        // configuración muerta: única línea fija (Debe CxP por el total del pago); el Haber por
        // cada medio de pago (1..N líneas, caja/banco) es completamente dinámico vía
        // PostingFact.Allocations en SupplierPaymentConfirmedPostingTranslator, no representable
        // como PostingRuleLine fija (cardinalidad variable, mismo criterio que
        // "Expenses"/"DocumentConfirmed").
        //
        // ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C (ADR-035) — el Debe fijo se separa en dos
        // líneas: CxP por lo aplicado (AppliedToPayable) y "1.1.03.004 Anticipos a proveedores"
        // por el remanente no aplicado (SupplierCredit — misma cuenta/AmountKind que
        // "Purchases"/"PurchaseReturn" usa para el saldo a favor). JournalFactory omite la línea
        // cuyo monto es 0 (pago sin remanente / pago sin CxP).
        new(
            PostingFactCatalog.SupplierPaymentConfirmed,
            [
                new("2.1.01.001", AccountNature.Debit, PostingAmountKind.AppliedToPayable),
                new("1.1.03.004", AccountNature.Debit, PostingAmountKind.SupplierCredit),
            ]
        ),
        // ACCOUNTING-FINAL-FIX — "SupplierPaymentReversed" ya no se siembra: la reversa del pago espeja el
        // asiento histórico (Accounting/Reversal, §18) sin PostingRule. Las reglas existentes no se borran.
        // RETENTIONS-POSTING-RULE-SEED-01H / RETENTIONS-TAX-COMPONENT-POSTING-02C —
        // "Retentions"/"DocumentIssued" reclasifica el monto retenido: Debe CxP proveedor (se
        // reduce lo exigible por el total retenido), Haber Retenciones IVA por pagar (solo el
        // componente IVA) + Haber Retenciones Renta por pagar (solo el componente Renta). 02C
        // separa lo que 01H sembraba como una sola línea combinada de IVA (deuda documentada en su
        // momento: "una retención de Renta futura quedaría mal clasificada... hasta que el
        // traductor separe los montos por tipo") — ahora el traductor sí transporta ambos montos
        // por separado (PostingFact.RetainedVatAmount/RetainedIncomeAmount) y esta regla tiene una
        // línea propia por cada uno. Una línea cuyo componente es 0 (retención solo-IVA o
        // solo-Renta) se omite automáticamente en JournalFactory — nunca se contabiliza en cero.
        // RetentionDocumentCancelledPostingTranslator no tiene entrada propia aquí — reversa el
        // mismo asiento vía ReverseJournalEntryCommand, no resuelve una PostingRule nueva (mismo
        // criterio que Purchases/PurchaseCreditNoteCancelled arriba).
        new(
            PostingFactCatalog.RetentionDocumentIssued,
            [
                new("2.1.01.001", AccountNature.Debit, PostingAmountKind.Retention),
                new("2.1.02.002", AccountNature.Credit, PostingAmountKind.RetentionVat),
                new("2.1.02.003", AccountNature.Credit, PostingAmountKind.RetentionIncome),
            ]
        ),
        // ERP-POSTING-RULES-EXPENSES-RETENTIONS-SEED-01 — "Expenses"/"DocumentConfirmed" faltaba
        // por completo en este seed (confirmado leyendo ExpenseDocumentConfirmedPostingTranslator:
        // el traductor existe y publica el hecho contable desde EXPENSES-CONFIRM-07, pero ninguna
        // fase previa sembró la PostingRule correspondiente) — toda empresa nueva o existente
        // llegaba fail-closed ("RULE_NOT_FOUND") al confirmar el primer gasto, hasta que se creaba
        // manualmente vía CreatePostingRuleHandler (hallazgo real en preparación QA
        // RETENTIONS-SRI-SANDBOX-SEED-04F-1). Mismo criterio que "Payables"/"SupplierPaymentConfirmed":
        // la línea de Debe por cada categoría/subcategoría de gasto es dinámica vía
        // PostingFact.Allocations en el traductor (cardinalidad variable, no representable como
        // PostingRuleLine fija) — aquí solo se fijan las 2 líneas que sí son cuentas fijas de la
        // empresa: Debe IVA crédito tributario (si el gasto tiene IVA; se omite en cero vía
        // JournalFactory) y Haber CxP proveedores por el total.
        new(
            PostingFactCatalog.ExpenseDocumentConfirmed,
            [
                new("1.1.05.001", AccountNature.Debit, PostingAmountKind.TaxVat),
                new("2.1.01.001", AccountNature.Credit, PostingAmountKind.GrandTotal),
            ]
        ),
        // ACCOUNTING-12A — ajustes de inventario: Inventario por el TotalCost del Kardex; la
        // contrapartida (Haber en Ingreso, Debe en Egreso) es la cuenta del motivo, dinámica.
        new(
            PostingFactCatalog.StockAdjustmentIn,
            [new("1.1.04.001", AccountNature.Debit, PostingAmountKind.GrandTotal)]
        ),
        new(
            PostingFactCatalog.StockAdjustmentOut,
            [new("1.1.04.001", AccountNature.Credit, PostingAmountKind.GrandTotal)]
        ),
        // ACCOUNTING-12C — Caja: movimientos manuales y depósito bancario son reglas habilitadoras
        // (la caja real de la sesión, el motivo y el banco destino llegan como destinos dinámicos);
        // la diferencia de arqueo fija la cuenta de diferencias y la caja es dinámica.
        new(PostingFactCatalog.CashManualIncome, []),
        new(PostingFactCatalog.CashManualExpense, []),
        new(PostingFactCatalog.CashWithdrawal, []),
        new(PostingFactCatalog.CashBankDeposit, []),
        new(
            PostingFactCatalog.CashCountSurplus,
            [new("4.2.01.003", AccountNature.Credit, PostingAmountKind.GrandTotal)]
        ),
        new(
            PostingFactCatalog.CashCountShortage,
            [new("6.5.01.001", AccountNature.Debit, PostingAmountKind.GrandTotal)]
        ),
        // IL-7A — apertura de la Carga Inicial: un asiento por ImportBatch confirmado
        // (SourceEventId = lote, EntryDate = Company.OpeningBalanceDate), GrandTotal = monto
        // confirmado del dominio ya redondeado. Contrapartida única: la cuenta puente
        // "Saldos de apertura". El traductor que publica estos hechos llega en IL-7B.
        new(
            PostingFactCatalog.OpeningInventory,
            [
                new("1.1.04.001", AccountNature.Debit, PostingAmountKind.GrandTotal),
                new(OpeningBalanceAccountCode, AccountNature.Credit, PostingAmountKind.GrandTotal),
            ]
        ),
        new(
            PostingFactCatalog.OpeningReceivables,
            [
                new("1.1.03.001", AccountNature.Debit, PostingAmountKind.GrandTotal),
                new(OpeningBalanceAccountCode, AccountNature.Credit, PostingAmountKind.GrandTotal),
            ]
        ),
        new(
            PostingFactCatalog.OpeningPayables,
            [
                new(OpeningBalanceAccountCode, AccountNature.Debit, PostingAmountKind.GrandTotal),
                new("2.1.01.001", AccountNature.Credit, PostingAmountKind.GrandTotal),
            ]
        ),
        // IL-8A — ASI de apertura: regla HABILITADORA sin líneas fijas. Todas las líneas del asiento
        // (Debe/Haber dinámicos) viajan como PostingAllocation; el comando de publicación exige que
        // esta regla siga sin líneas fijas. Requiere la cuenta puente (no se siembra si hay conflicto).
        new(
            PostingFactCatalog.OpeningJournalEntry,
            []
        ),
    ];

    public async Task ExecuteAsync(
        CompanyBootstrapContext context,
        CancellationToken cancellationToken = default
    )
    {
        var (tenantId, companyId, actorId) = context;

        var existingAccounts = await _db
            .Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .Select(a => new
            {
                a.Id,
                Code = a.Code.Value,
                a.Name,
                a.AccountType,
                a.Nature,
            })
            .ToListAsync(cancellationToken);
        var openingAccountConflict = OpeningBalanceAccountConflict(
            existingAccounts.Select(a => (a.Code, a.Name, a.AccountType, a.Nature))
        );
        if (openingAccountConflict is not null)
            LogOpeningBalanceAccountConflict(openingAccountConflict, companyId);
        var accountIdByCode = existingAccounts.ToDictionary(
            a => a.Code,
            a => a.Id,
            StringComparer.Ordinal
        );

        var seededCount = 0;
        foreach (var m in RetailChart)
        {
            if (accountIdByCode.ContainsKey(m.Code))
                continue;
            if (openingAccountConflict is not null && IsOpeningBalanceAccountCode(m.Code))
                continue;

            Guid? parentAccountId = null;
            if (m.ParentCode is not null)
            {
                if (!accountIdByCode.TryGetValue(m.ParentCode, out var parentId))
                    throw new InvalidOperationException(
                        $"RetailChart inválido: la cuenta '{m.Code}' declara ParentCode '{m.ParentCode}' "
                            + "pero esa cuenta padre no existe ni fue declarada antes en la lista. "
                            + "El código contable manda la jerarquía (ACCOUNTING-CHART-CANONICAL-HIERARCHY-01) "
                            + "— agregue la cuenta agrupadora intermedia antes de sus hijas."
                    );
                parentAccountId = parentId;
            }
            var account = Account.Create(
                tenantId,
                companyId,
                AccountCode.Create(m.Code),
                m.Name,
                parentAccountId,
                m.Type,
                m.Nature,
                m.AllowsPosting,
                actorId
            );
            _db.Accounts.Add(account);
            accountIdByCode[m.Code] = account.Id;
            seededCount++;
        }

        if (seededCount > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            LogAccountsSeeded(seededCount, companyId);
        }
        else
        {
            LogAccountsSkipped(companyId);
        }

        // SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01 — Efectivo resuelve su cuenta contable
        // exclusivamente desde CashRegister.AccountingAccountId (nunca desde PaymentMethodAccount,
        // que ahora es exclusivo de Tarjeta/Cheque). Vincula la Caja Principal sembrada por
        // CajaBootstrapStep (Order 45, ANTES de este step) a "1.1.01.001 Caja general" — mismo
        // criterio de idempotencia que el resto de este step: solo si aún no tiene cuenta
        // configurada, nunca pisa una configuración manual posterior.
        if (accountIdByCode.TryGetValue("1.1.01.001", out var cajaGeneralAccountId))
        {
            var cashRegistersWithoutAccount = await _db
                .CashRegisters.IgnoreQueryFilters()
                .Where(cr =>
                    cr.TenantId == tenantId
                    && cr.CompanyId == companyId
                    && cr.AccountingAccountId == null
                )
                .ToListAsync(cancellationToken);
            if (cashRegistersWithoutAccount.Count > 0)
            {
                foreach (var cashRegister in cashRegistersWithoutAccount)
                    cashRegister.SetAccountingAccount(cajaGeneralAccountId, actorId);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        // DATETIME-COMPANY-CLOCK-GLOBAL-FIX-01: año fiscal a sembrar según el día operativo de la
        // empresa (ICompanyClock), nunca DateTime.UtcNow.Year — evita crear el período del año
        // equivocado si el bootstrap corre en la ventana horaria en que UTC ya cruzó el 1 de enero
        // pero en Ecuador todavía es 31 de diciembre (o viceversa).
        var currentYear = (
            await _companyClock.TodayAsync(companyId, tenantId, cancellationToken)
        ).Year;
        var hasPeriodForYear = await _db
            .AccountingPeriods.IgnoreQueryFilters()
            .AnyAsync(
                p =>
                    p.TenantId == tenantId
                    && p.CompanyId == companyId
                    && p.FiscalYear == currentYear,
                cancellationToken
            );

        if (!hasPeriodForYear)
        {
            // ACCOUNTING-07: desde el ejercicio AccountingPeriodStructure.MonthlyFromFiscalYear (2027) se
            // siembran los 12 meses naturales (mismo layout que GenerateAccountingFiscalYear); antes, el
            // período anual histórico — 2026 queda anual y nunca se divide retroactivamente.
            IReadOnlyList<AccountingPeriodStructure.Slot> slots =
                currentYear >= AccountingPeriodStructure.MonthlyFromFiscalYear
                ? AccountingPeriodStructure.MonthlyLayout(currentYear)
                : [new AccountingPeriodStructure.Slot(
                    currentYear,
                    1,
                    new DateOnly(currentYear, 1, 1),
                    new DateOnly(currentYear, 12, 31)
                )];
            foreach (var slot in slots)
                _db.AccountingPeriods.Add(
                    AccountingPeriod.Create(
                        tenantId,
                        companyId,
                        slot.FiscalYear,
                        slot.PeriodNumber,
                        slot.StartDate,
                        slot.EndDate,
                        createdBy: actorId
                    )
                );
            await _db.SaveChangesAsync(cancellationToken);
            LogPeriodSeeded(currentYear, companyId);
        }
        else
        {
            LogPeriodSkipped(currentYear, companyId);
        }

        await SeedPostingRulesAsync(
            tenantId,
            companyId,
            actorId,
            openingAccountConflict is not null,
            cancellationToken
        );
    }

    private async Task SeedPostingRulesAsync(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        bool openingAccountConflict,
        CancellationToken cancellationToken
    )
    {
        var existingRuleKeys = await _db
            .PostingRules.IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.CompanyId == companyId)
            .Select(r => new { r.SourceModule, r.FactType })
            .ToListAsync(cancellationToken);
        var existingRuleKeySet = existingRuleKeys
            .Select(r => (r.SourceModule, r.FactType))
            .ToHashSet();

        var missingRules = MinimalPostingRules
            .Where(r => !existingRuleKeySet.Contains((r.SourceModule, r.FactType)))
            // IL-7A — sin cuenta puente propia (código ocupado con otro sentido) no se siembra
            // ninguna regla de apertura: nunca se contabiliza contra una cuenta ajena.
            .Where(r => !(openingAccountConflict && IsOpeningBalanceRule(r)))
            .ToList();

        if (missingRules.Count == 0)
        {
            LogPostingRulesSkipped(companyId);
            return;
        }

        var accountByCode = await _db
            .Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .Select(a => new
            {
                a.Id,
                Code = a.Code.Value,
                a.IsActive,
                a.AllowsPosting,
            })
            .ToDictionaryAsync(
                a => a.Code,
                a => new AccountSeedLookup(a.Id, a.IsActive, a.AllowsPosting),
                StringComparer.Ordinal,
                cancellationToken
            );

        var seededRulesCount = 0;
        foreach (var m in missingRules)
        {
            var missingAccountCode = m
                .Lines.Select(l => l.AccountCode)
                .FirstOrDefault(code => !accountByCode.ContainsKey(code));
            if (missingAccountCode is not null)
            {
                LogPostingRuleSkippedMissingAccount(
                    m.SourceModule,
                    m.FactType,
                    missingAccountCode,
                    companyId
                );
                continue;
            }

            var invalidAccountCode = m
                .Lines.Select(l => l.AccountCode)
                .FirstOrDefault(code =>
                    !accountByCode[code].IsActive || !accountByCode[code].AllowsPosting
                );
            if (invalidAccountCode is not null)
            {
                LogPostingRuleSkippedInvalidAccount(
                    m.SourceModule,
                    m.FactType,
                    invalidAccountCode,
                    companyId
                );
                continue;
            }

            // ACCOUNTING-11 — el seed respeta la misma forma que exige la administración desde UI.
            var fact = PostingFactCatalog.Find(m.SourceModule, m.FactType);
            var shapeError = fact is null
                ? "fact not in PostingFactCatalog"
                : PostingFactCatalog.ValidateLineShape(
                    fact,
                    m.Lines.Select(l => (l.Nature, l.AmountKind)).ToList()
                );
            if (shapeError is not null)
            {
                LogPostingRuleSkippedInvalidShape(m.SourceModule, m.FactType, shapeError, companyId);
                continue;
            }

            var rule = PostingRule.Create(
                tenantId,
                companyId,
                m.SourceModule,
                m.FactType,
                createdBy: actorId
            );

            foreach (var line in m.Lines)
                rule.AddLine(accountByCode[line.AccountCode].Id, line.Nature, line.AmountKind);

            _db.PostingRules.Add(rule);
            seededRulesCount++;
        }

        if (seededRulesCount == 0)
        {
            LogPostingRulesSkipped(companyId);
            return;
        }

        await _db.SaveChangesAsync(cancellationToken);
        LogPostingRulesSeeded(seededRulesCount, companyId);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeded {Count} retail chart-of-accounts entries for company {CompanyId}."
    )]
    private partial void LogAccountsSeeded(int count, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Retail chart of accounts already complete for company {CompanyId}. Skipping."
    )]
    private partial void LogAccountsSkipped(Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeded AccountingPeriod {FiscalYear} for company {CompanyId}."
    )]
    private partial void LogPeriodSeeded(int fiscalYear, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "AccountingPeriod {FiscalYear} already exists for company {CompanyId}. Skipping."
    )]
    private partial void LogPeriodSkipped(int fiscalYear, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeded {Count} retail posting rules for company {CompanyId}."
    )]
    private partial void LogPostingRulesSeeded(int count, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Retail posting rules already complete for company {CompanyId}. Skipping."
    )]
    private partial void LogPostingRulesSkipped(Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Skipped seeding posting rule {SourceModule}/{FactType} for company {CompanyId} — "
            + "referenced account code {AccountCode} not found in the chart of accounts."
    )]
    private partial void LogPostingRuleSkippedMissingAccount(
        string sourceModule,
        string factType,
        string accountCode,
        Guid companyId
    );

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Skipped seeding posting rule {SourceModule}/{FactType} for company {CompanyId} — "
            + "seed lines do not match PostingFactCatalog: {Reason}."
    )]
    private partial void LogPostingRuleSkippedInvalidShape(
        string sourceModule,
        string factType,
        string reason,
        Guid companyId
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Skipped seeding posting rule {SourceModule}/{FactType} for company {CompanyId} — "
            + "referenced account code {AccountCode} is inactive or does not allow posting."
    )]
    private partial void LogPostingRuleSkippedInvalidAccount(
        string sourceModule,
        string factType,
        string accountCode,
        Guid companyId
    );
}
