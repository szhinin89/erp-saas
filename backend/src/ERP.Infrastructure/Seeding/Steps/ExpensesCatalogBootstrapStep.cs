using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// EXPENSES-CATALOG-BOOTSTRAP-09: siembra el catálogo genérico de gastos por empresa usando como
/// fuente funcional la hoja "Plantilla" de plantilla_catalogo_gastos_erp_zh.xlsx. No lee el Excel
/// en runtime: la plantilla queda convertida a datos internos versionados. Depende de
/// <see cref="AccountingBootstrapStep"/> porque solo vincula subcategorías a cuentas contables de
/// gasto ya existentes, activas y postables; nunca crea cuentas desde este step.
/// </summary>
public sealed partial class ExpensesCatalogBootstrapStep : ICompanyBootstrapStep
{
    public const int TemplateItemCount = 59;

    public int Order => CompanyBootstrapStepOrder.ExpensesCatalog;

    private readonly ErpDbContext _db;
    private readonly ILogger<ExpensesCatalogBootstrapStep> _logger;

    public ExpensesCatalogBootstrapStep(
        ErpDbContext db,
        ILogger<ExpensesCatalogBootstrapStep> logger
    )
    {
        _db = db;
        _logger = logger;
    }

    private sealed record ExpenseCatalogItem(
        string TypeName,
        string CategoryName,
        string SubcategoryName,
        string AccountCode,
        bool IsDeductible,
        bool RequiresInvoice,
        bool IsActive,
        string Observation
    );

    private sealed record AccountLookup(
        Guid Id,
        bool IsActive,
        bool AllowsPosting,
        AccountType AccountType
    );

    private static readonly IReadOnlyList<ExpenseCatalogItem> Template =
    [
        new("Gastos administrativos", "Servicios basicos", "Energia electrica", "6.1.01.002", true, true, true, "Servicio basico deducible con comprobante autorizado"),
        new("Gastos administrativos", "Servicios basicos", "Agua potable", "6.1.01.002", true, true, true, "Servicio basico deducible con comprobante autorizado"),
        new("Gastos administrativos", "Servicios basicos", "Internet fijo", "6.1.01.002", true, true, true, "Servicio de conectividad para oficina o local"),
        new("Gastos administrativos", "Servicios basicos", "Telefonia fija", "6.1.01.002", true, true, true, "Servicio de comunicacion administrativa"),
        new("Gastos administrativos", "Servicios basicos", "Telefonia movil corporativa", "6.1.01.002", true, true, true, "Lineas moviles de uso empresarial"),
        new("Gastos administrativos", "Servicios basicos", "Servicios de seguridad y alarmas", "6.1.01.002", true, true, true, "Servicio recurrente de seguridad"),
        new("Gastos administrativos", "Arriendos", "Arriendo de oficina", "6.1.01.003", true, true, true, "Arriendo administrativo"),
        new("Gastos administrativos", "Arriendos", "Arriendo de local comercial", "6.1.01.003", true, true, true, "Arriendo de punto de venta"),
        new("Gastos administrativos", "Arriendos", "Arriendo de bodega", "6.1.01.003", true, true, true, "Bodega para operacion o inventario"),
        new("Gastos administrativos", "Arriendos", "Expensas y alicuotas", "6.1.01.003", true, true, true, "Cuotas de mantenimiento del inmueble"),
        new("Gastos administrativos", "Suministros de oficina", "Papeleria y utiles", "6.1.01.001", true, true, true, "Insumos administrativos de oficina"),
        new("Gastos administrativos", "Suministros de oficina", "Toner, tintas e insumos de impresion", "6.1.01.001", true, true, true, "Insumos para impresoras"),
        new("Gastos administrativos", "Suministros de oficina", "Material de limpieza", "6.1.01.001", true, true, true, "Suministros de limpieza del local u oficina"),
        new("Gastos administrativos", "Suministros de oficina", "Cafeteria y agua para personal", "6.1.01.001", true, true, true, "Consumo interno razonable"),
        new("Gastos administrativos", "Tecnologia y sistemas", "Licencias de software", "6.1.01.004", true, true, true, "Suscripciones o licencias administrativas"),
        new("Gastos administrativos", "Tecnologia y sistemas", "Hosting y dominio", "6.1.01.004", true, true, true, "Servicios web de la empresa"),
        new("Gastos administrativos", "Tecnologia y sistemas", "Soporte tecnico externo", "6.1.01.004", true, true, true, "Servicios profesionales de soporte"),
        new("Gastos administrativos", "Tecnologia y sistemas", "Servicios en la nube", "6.1.01.004", true, true, true, "Cloud, almacenamiento y herramientas SaaS"),
        new("Gastos administrativos", "Honorarios profesionales", "Servicios contables", "6.1.01.004", true, true, true, "Honorarios de contabilidad"),
        new("Gastos administrativos", "Honorarios profesionales", "Servicios legales", "6.1.01.004", true, true, true, "Asesoria legal externa"),
        new("Gastos administrativos", "Honorarios profesionales", "Consultoria administrativa", "6.1.01.004", true, true, true, "Consultoria de gestion"),
        new("Gastos administrativos", "Honorarios profesionales", "Servicios notariales", "6.1.01.004", true, true, true, "Tramites notariales relacionados al negocio"),
        new("Gastos administrativos", "Mantenimiento y reparaciones", "Mantenimiento de oficina", "6.1.01.005", true, true, true, "Mantenimiento menor de instalaciones"),
        new("Gastos administrativos", "Mantenimiento y reparaciones", "Mantenimiento de equipos de computacion", "6.1.01.005", true, true, true, "Mantenimiento preventivo o correctivo"),
        new("Gastos administrativos", "Mantenimiento y reparaciones", "Reparaciones menores", "6.1.01.005", true, true, true, "Reparaciones operativas que no se capitalizan"),
        new("Gastos administrativos", "Movilizacion y transporte", "Taxis y movilizacion local", "6.1.01.006", true, true, true, "Movilizacion administrativa sustentada"),
        new("Gastos administrativos", "Movilizacion y transporte", "Combustible administrativo", "6.1.01.006", true, true, true, "Combustible para gestiones administrativas"),
        new("Gastos administrativos", "Movilizacion y transporte", "Parqueaderos y peajes", "6.1.01.006", true, true, true, "Movilizacion relacionada con operaciones"),
        new("Gastos de venta", "Publicidad y marketing", "Publicidad digital", "6.2.01.001", true, true, true, "Campanas en redes, buscadores o medios digitales"),
        new("Gastos de venta", "Publicidad y marketing", "Material POP", "6.2.01.001", true, true, true, "Material promocional para puntos de venta"),
        new("Gastos de venta", "Publicidad y marketing", "Diseno grafico y contenido", "6.2.01.001", true, true, true, "Produccion de piezas comerciales"),
        new("Gastos de venta", "Publicidad y marketing", "Eventos comerciales", "6.2.01.001", true, true, true, "Activaciones, ferias o eventos de venta"),
        new("Gastos de venta", "Comisiones de venta", "Comisiones a vendedores", "6.2.01.002", true, true, true, "Comision comercial sustentada"),
        new("Gastos de venta", "Comisiones de venta", "Comisiones a marketplaces", "6.2.01.002", true, true, true, "Comisiones cobradas por plataformas"),
        new("Gastos de venta", "Comisiones de venta", "Comisiones a terceros comerciales", "6.2.01.002", true, true, true, "Referidos, agentes o intermediarios"),
        new("Gastos de venta", "Empaques y suministros de venta", "Fundas y empaques", "6.2.01.003", true, true, true, "Empaques entregados al cliente"),
        new("Gastos de venta", "Empaques y suministros de venta", "Etiquetas y adhesivos", "6.2.01.003", true, true, true, "Material de identificacion comercial"),
        new("Gastos de venta", "Empaques y suministros de venta", "Cajas y material de embalaje", "6.2.01.003", true, true, true, "Embalaje para venta o despacho"),
        new("Gastos de venta", "Transporte y entregas", "Envios a clientes", "6.2.01.004", true, true, true, "Courier o transporte de entregas"),
        new("Gastos de venta", "Transporte y entregas", "Fletes de distribucion", "6.2.01.004", true, true, true, "Traslado de mercaderia vendida"),
        new("Gastos de venta", "Transporte y entregas", "Mensajeria comercial", "6.2.01.004", true, true, true, "Mensajeria relacionada a ventas"),
        new("Gastos financieros", "Costos bancarios", "Comisiones bancarias", "6.3.01.001", true, true, true, "Cargos bancarios sustentados en estado o comprobante"),
        new("Gastos financieros", "Costos bancarios", "Mantenimiento de cuenta bancaria", "6.3.01.001", true, true, true, "Costo de mantenimiento de cuenta"),
        new("Gastos financieros", "Costos bancarios", "Transferencias bancarias", "6.3.01.001", true, true, true, "Costos por transacciones bancarias"),
        new("Gastos financieros", "Tarjetas y pasarelas de pago", "Comisiones de tarjetas de credito/debito", "6.3.01.002", true, true, true, "Comision por procesamiento de tarjetas"),
        new("Gastos financieros", "Tarjetas y pasarelas de pago", "Comisiones de pasarela de pago", "6.3.01.002", true, true, true, "Cargos de plataformas de cobro"),
        new("Gastos financieros", "Intereses financieros", "Intereses por prestamos", "6.3.01.003", true, true, true, "Intereses de obligaciones financieras"),
        new("Gastos financieros", "Intereses financieros", "Intereses por mora", "6.3.01.003", false, true, true, "Revisar deducibilidad segun normativa aplicable"),
        new("Impuestos y no deducibles", "Impuestos no recuperables", "Patentes y tasas municipales", "6.4.01.001", true, true, true, "Tasas o patentes relacionadas con la actividad"),
        new("Impuestos y no deducibles", "Impuestos no recuperables", "Impuestos asumidos no recuperables", "6.4.01.001", false, true, true, "Impuesto asumido que no genera credito tributario"),
        new("Impuestos y no deducibles", "Impuestos no recuperables", "IVA no recuperable", "6.4.01.001", false, true, true, "IVA que no puede tomarse como credito"),
        new("Impuestos y no deducibles", "Gastos no deducibles", "Multas e intereses tributarios", "6.4.01.002", false, true, true, "No deducible por defecto"),
        new("Impuestos y no deducibles", "Gastos no deducibles", "Gastos sin comprobante autorizado", "6.4.01.002", false, false, true, "Usar solo cuando no exista soporte tributario valido"),
        new("Impuestos y no deducibles", "Gastos no deducibles", "Donaciones no deducibles", "6.4.01.002", false, true, true, "Clasificar como no deducible salvo configuracion especifica"),
        new("Descuadres y perdidas operativas", "Descuadres de caja", "Faltante de caja", "6.5.01.001", false, false, true, "Diferencia negativa al cierre de caja"),
        new("Descuadres y perdidas operativas", "Descuadres de caja", "Ajuste menor de caja", "6.5.01.001", false, false, true, "Ajuste operativo por diferencia menor"),
        new("Descuadres y perdidas operativas", "Mermas retail", "Merma por dano o caducidad", "6.5.01.002", false, true, true, "Perdida de mercaderia no recuperable"),
        new("Descuadres y perdidas operativas", "Mermas retail", "Merma por robo o perdida", "6.5.01.002", false, true, true, "Perdida operativa sustentada con acta o soporte"),
        new("Descuadres y perdidas operativas", "Mermas retail", "Merma por ajuste fisico", "6.5.01.002", false, true, true, "Diferencia detectada en conteo fisico"),
    ];

    static ExpensesCatalogBootstrapStep()
    {
        if (Template.Count != TemplateItemCount)
            throw new InvalidOperationException(
                $"Expenses catalog template count mismatch. Expected {TemplateItemCount}, got {Template.Count}."
            );

        var duplicateSubcategories = Template
            .GroupBy(i => (
                Type: NormalizeName(i.TypeName),
                Category: NormalizeName(i.CategoryName),
                Subcategory: NormalizeName(i.SubcategoryName)
            ))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Type}/{g.Key.Category}/{g.Key.Subcategory}")
            .ToList();
        if (duplicateSubcategories.Count > 0)
            throw new InvalidOperationException(
                "Expenses catalog template contains duplicate subcategories: "
                    + string.Join(", ", duplicateSubcategories)
            );

        var invalidItem = Template.FirstOrDefault(i =>
            string.IsNullOrWhiteSpace(i.TypeName)
            || string.IsNullOrWhiteSpace(i.CategoryName)
            || string.IsNullOrWhiteSpace(i.SubcategoryName)
            || string.IsNullOrWhiteSpace(i.AccountCode)
        );
        if (invalidItem is not null)
            throw new InvalidOperationException(
                "Expenses catalog template contains blank required values."
            );
    }

    public async Task ExecuteAsync(
        CompanyBootstrapContext context,
        CancellationToken cancellationToken = default
    )
    {
        var (tenantId, companyId, actorId) = context;

        var accountByCode = await _db
            .Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.CompanyId == companyId)
            .Select(a => new
            {
                Code = a.Code.Value,
                a.Id,
                a.IsActive,
                a.AllowsPosting,
                a.AccountType,
            })
            .ToDictionaryAsync(
                a => a.Code,
                a => new AccountLookup(a.Id, a.IsActive, a.AllowsPosting, a.AccountType),
                StringComparer.Ordinal,
                cancellationToken
            );

        var nodes = await _db
            .ExpenseCategoryNodes.IgnoreQueryFilters()
            .Where(n => n.TenantId == tenantId && n.CompanyId == companyId)
            .ToListAsync(cancellationToken);
        var nodesByKey = BuildNodeLookup(nodes);
        var typeCodeByName = BuildTypeCodes();
        var categoryCodeByKey = BuildCategoryCodes();

        var createdCount = 0;
        var skippedSubcategoryCount = 0;

        for (var index = 0; index < Template.Count; index++)
        {
            var item = Template[index];
            if (!TryResolveUsableExpenseAccount(companyId, item, accountByCode, out var accountId))
            {
                skippedSubcategoryCount++;
                continue;
            }

            var type = GetOrCreateType(
                tenantId,
                companyId,
                actorId,
                item,
                typeCodeByName[NormalizeName(item.TypeName)],
                nodesByKey,
                ref createdCount
            );
            if (!type.IsActive)
            {
                LogExpenseCatalogSubcategorySkippedInactiveParent(
                    item.SubcategoryName,
                    item.CategoryName,
                    item.TypeName,
                    companyId
                );
                skippedSubcategoryCount++;
                continue;
            }

            var categoryKey = (NormalizeName(item.TypeName), NormalizeName(item.CategoryName));
            var category = GetOrCreateCategory(
                tenantId,
                companyId,
                actorId,
                item,
                type,
                categoryCodeByKey[categoryKey],
                nodesByKey,
                ref createdCount
            );
            if (!category.IsActive)
            {
                LogExpenseCatalogSubcategorySkippedInactiveParent(
                    item.SubcategoryName,
                    item.CategoryName,
                    item.TypeName,
                    companyId
                );
                skippedSubcategoryCount++;
                continue;
            }

            var subcategoryKey = NodeKey(
                category.Id,
                ExpenseCategoryNodeLevel.Subcategory,
                item.SubcategoryName
            );
            if (nodesByKey.ContainsKey(subcategoryKey))
                continue;

            var subcategory = ExpenseCategoryNode.CreateSubcategory(
                tenantId,
                companyId,
                category,
                $"GS-{index + 1:000}",
                item.SubcategoryName,
                accountId,
                actorId,
                item.Observation,
                item.IsDeductible,
                item.RequiresInvoice
            );
            if (!item.IsActive)
                subcategory.SetActive(false, actorId);

            _db.ExpenseCategoryNodes.Add(subcategory);
            nodesByKey[subcategoryKey] = subcategory;
            createdCount++;
        }

        if (createdCount > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            LogExpenseCatalogSeeded(createdCount, skippedSubcategoryCount, companyId);
        }
        else
        {
            LogExpenseCatalogSkipped(skippedSubcategoryCount, companyId);
        }
    }

    private bool TryResolveUsableExpenseAccount(
        Guid companyId,
        ExpenseCatalogItem item,
        IReadOnlyDictionary<string, AccountLookup> accountByCode,
        out Guid accountId
    )
    {
        accountId = Guid.Empty;
        if (!accountByCode.TryGetValue(item.AccountCode, out var account))
        {
            LogExpenseCatalogSubcategorySkippedMissingAccount(
                item.SubcategoryName,
                item.AccountCode,
                companyId
            );
            return false;
        }

        if (!account.IsActive || !account.AllowsPosting || account.AccountType != AccountType.Expense)
        {
            LogExpenseCatalogSubcategorySkippedInvalidAccount(
                item.SubcategoryName,
                item.AccountCode,
                account.IsActive,
                account.AllowsPosting,
                account.AccountType,
                companyId
            );
            return false;
        }

        accountId = account.Id;
        return true;
    }

    private ExpenseCategoryNode GetOrCreateType(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        ExpenseCatalogItem item,
        string code,
        Dictionary<(Guid? ParentId, ExpenseCategoryNodeLevel Level, string Name), ExpenseCategoryNode> nodesByKey,
        ref int createdCount
    )
    {
        var key = NodeKey(null, ExpenseCategoryNodeLevel.Type, item.TypeName);
        if (nodesByKey.TryGetValue(key, out var existing))
            return existing;

        var node = ExpenseCategoryNode.CreateType(
            tenantId,
            companyId,
            code,
            item.TypeName,
            actorId
        );
        _db.ExpenseCategoryNodes.Add(node);
        nodesByKey[key] = node;
        createdCount++;
        return node;
    }

    private ExpenseCategoryNode GetOrCreateCategory(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        ExpenseCatalogItem item,
        ExpenseCategoryNode parentType,
        string code,
        Dictionary<(Guid? ParentId, ExpenseCategoryNodeLevel Level, string Name), ExpenseCategoryNode> nodesByKey,
        ref int createdCount
    )
    {
        var key = NodeKey(parentType.Id, ExpenseCategoryNodeLevel.Category, item.CategoryName);
        if (nodesByKey.TryGetValue(key, out var existing))
            return existing;

        var node = ExpenseCategoryNode.CreateCategory(
            tenantId,
            companyId,
            parentType,
            code,
            item.CategoryName,
            actorId
        );
        _db.ExpenseCategoryNodes.Add(node);
        nodesByKey[key] = node;
        createdCount++;
        return node;
    }

    private static Dictionary<(Guid? ParentId, ExpenseCategoryNodeLevel Level, string Name), ExpenseCategoryNode> BuildNodeLookup(
        IEnumerable<ExpenseCategoryNode> nodes
    )
    {
        var lookup = new Dictionary<(Guid?, ExpenseCategoryNodeLevel, string), ExpenseCategoryNode>();
        foreach (var node in nodes)
        {
            var key = NodeKey(node.ParentId, node.Level, node.Name);
            lookup.TryAdd(key, node);
        }

        return lookup;
    }

    private static Dictionary<string, string> BuildTypeCodes()
    {
        var codes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in Template)
        {
            var name = NormalizeName(item.TypeName);
            if (!codes.ContainsKey(name))
                codes[name] = $"GT-{codes.Count + 1:000}";
        }

        return codes;
    }

    private static Dictionary<(string TypeName, string CategoryName), string> BuildCategoryCodes()
    {
        var codes = new Dictionary<(string, string), string>();
        foreach (var item in Template)
        {
            var key = (NormalizeName(item.TypeName), NormalizeName(item.CategoryName));
            if (!codes.ContainsKey(key))
                codes[key] = $"GC-{codes.Count + 1:000}";
        }

        return codes;
    }

    private static (Guid? ParentId, ExpenseCategoryNodeLevel Level, string Name) NodeKey(
        Guid? parentId,
        ExpenseCategoryNodeLevel level,
        string name
    ) => (parentId, level, NormalizeName(name));

    private static string NormalizeName(string value) =>
        string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeded {CreatedCount} expense catalog nodes for company {CompanyId}; skipped {SkippedSubcategoryCount} subcategories."
    )]
    private partial void LogExpenseCatalogSeeded(
        int createdCount,
        int skippedSubcategoryCount,
        Guid companyId
    );

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Expense catalog already complete for company {CompanyId}; skipped {SkippedSubcategoryCount} subcategories."
    )]
    private partial void LogExpenseCatalogSkipped(int skippedSubcategoryCount, Guid companyId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Skipped expense subcategory '{SubcategoryName}' for company {CompanyId}: account code {AccountCode} was not found."
    )]
    private partial void LogExpenseCatalogSubcategorySkippedMissingAccount(
        string subcategoryName,
        string accountCode,
        Guid companyId
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Skipped expense subcategory '{SubcategoryName}' for company {CompanyId}: account code {AccountCode} is not usable for expenses (IsActive={IsActive}, AllowsPosting={AllowsPosting}, AccountType={AccountType})."
    )]
    private partial void LogExpenseCatalogSubcategorySkippedInvalidAccount(
        string subcategoryName,
        string accountCode,
        bool isActive,
        bool allowsPosting,
        AccountType accountType,
        Guid companyId
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Skipped expense subcategory '{SubcategoryName}' under '{TypeName}/{CategoryName}' for company {CompanyId}: parent node is inactive."
    )]
    private partial void LogExpenseCatalogSubcategorySkippedInactiveParent(
        string subcategoryName,
        string categoryName,
        string typeName,
        Guid companyId
    );
}
