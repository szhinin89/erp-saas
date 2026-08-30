using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.DocTypes.Entities;
using ERP.Infrastructure.Persistence.Configurations.DocTypes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// DOC-TYPE-SSOT-01: <c>DocTypeCodes</c> es un alias técnico para literales usados en lógica
/// interna (p. ej. reglas por tipo en <c>DocumentFlowPolicyBootstrapStep</c>) — nunca debe
/// convertirse en un catálogo paralelo que pueda divergir del SSOT real, <see cref="DocType"/>
/// (seed EF en <see cref="DocTypeConfiguration"/>). Lee el seed directamente desde
/// <c>DocTypeConfiguration.Configure</c> aplicado a un <see cref="ModelBuilder"/> aislado (sin
/// conexión real a base de datos) y falla si alguno de los códigos nombrados deja de existir
/// activo en el catálogo persistido, o si algún mapeo SRI esperado se pierde del seed de
/// <see cref="DocTypeSriMap"/>.
/// </summary>
public sealed class DocTypeSeedAlignmentTests
{
    [Theory]
    [InlineData(nameof(DocTypeCodes.SalesInvoice), DocTypeCodes.SalesInvoice)]
    [InlineData(nameof(DocTypeCodes.SalesCreditNote), DocTypeCodes.SalesCreditNote)]
    [InlineData(nameof(DocTypeCodes.PurchaseInvoice), DocTypeCodes.PurchaseInvoice)]
    [InlineData(nameof(DocTypeCodes.PurchaseCreditNote), DocTypeCodes.PurchaseCreditNote)]
    [InlineData(nameof(DocTypeCodes.ExpenseDocument), DocTypeCodes.ExpenseDocument)]
    [InlineData(nameof(DocTypeCodes.ExpenseWithholding), DocTypeCodes.ExpenseWithholding)]
    [InlineData(nameof(DocTypeCodes.SupplierPayment), DocTypeCodes.SupplierPayment)]
    [InlineData(nameof(DocTypeCodes.CustomerCollection), DocTypeCodes.CustomerCollection)]
    [InlineData(nameof(DocTypeCodes.ManualJournalEntry), DocTypeCodes.ManualJournalEntry)]
    [InlineData(nameof(DocTypeCodes.InventoryAdjustment), DocTypeCodes.InventoryAdjustment)]
    public void Every_DocTypeCodes_constant_exists_active_in_the_DocType_seed(
        string constantName,
        string code
    )
    {
        _ = constantName;
        var seededCodes = GetSeededActiveDocTypeCodes();

        seededCodes
            .Should()
            .Contain(code, $"DocTypeCodes.{constantName} debe seguir existiendo activo en el seed de DocType");
    }

    [Theory]
    [InlineData(DocTypeCodes.SalesInvoice, "01")]
    [InlineData(DocTypeCodes.SalesCreditNote, "04")]
    [InlineData(DocTypeCodes.PurchaseCreditNote, "04")]
    [InlineData(DocTypeCodes.ExpenseWithholding, "07")]
    public void Expected_SRI_mappings_exist_in_the_DocTypeSriMap_seed(string docTypeCode, string sriDocTypeCode)
    {
        var seededMap = GetSeededSriMap();

        seededMap.Should().ContainKey(docTypeCode);
        seededMap[docTypeCode].Should().Be(sriDocTypeCode);
    }

    private static HashSet<string> GetSeededActiveDocTypeCodes()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.ApplyConfiguration(new DocTypeConfiguration());

        var entityType = modelBuilder.Model.FindEntityType(typeof(DocType))!;
        return entityType
            .GetSeedData()
            .Where(seed => (bool)seed[nameof(DocType.IsActive)]!)
            .Select(seed => (string)seed[nameof(DocType.Code)]!)
            .ToHashSet();
    }

    private static Dictionary<string, string> GetSeededSriMap()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.ApplyConfiguration(new DocTypeSriMapConfiguration());

        var entityType = modelBuilder.Model.FindEntityType(typeof(DocTypeSriMap))!;
        return entityType
            .GetSeedData()
            .ToDictionary(
                seed => (string)seed[nameof(DocTypeSriMap.DocTypeCode)]!,
                seed => (string)seed[nameof(DocTypeSriMap.SriDocTypeCode)]!
            );
    }
}
