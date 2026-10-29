using ERP.Application.Common.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;
using Moq;
using PurchaseTaxResolver = ERP.Application.Modules.Purchases.Services.ISriTaxResolver;

namespace ERP.Application.Tests.Purchases;

public sealed class PurchaseIceSnapshotTests
{
    private static PurchaseInvoiceDetail Line() => PurchaseInvoiceDetail.Create(
        Guid.NewGuid(), Guid.NewGuid(), "XML ICE", 1m, 100m, "4", "UNIT", iceCode: "3031");

    private static Mock<PurchaseTaxResolver> Resolver(SriTaxCalculationType mode, decimal? unitValue)
    {
        var tax = new Mock<PurchaseTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(15m, "IVA"));
        tax.Setup(t => t.GetVatCatalogEntryAsync("4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriTaxCatalogEntry("4", "IVA", 15m, null, SriTaxCalculationType.Percentage));
        tax.Setup(t => t.GetXmlIceCatalogEntryAsync("3031", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriTaxCatalogEntry("3031", "ICE", 75m, unitValue, mode));
        return tax;
    }

    [Theory]
    [InlineData(SriTaxCalculationType.Specific, null)]
    [InlineData(SriTaxCalculationType.Specific, 0.18)]
    [InlineData(SriTaxCalculationType.Mixed, 10.41)]
    public async Task Manual_ICE_without_verified_physical_basis_is_rejected(
        SriTaxCalculationType mode, double? unitValue)
    {
        var line = Line();
        var tax = Resolver(mode, (decimal?)unitValue);
        tax.Setup(t => t.GetIceCatalogEntryAsync("3031", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriTaxCatalogEntry("3031", "ICE", 75m, (decimal?)unitValue, mode));

        var error = await PurchaseTaxHelper.ResolveTaxesAsync(line, tax.Object, CancellationToken.None);

        error.Should().NotBeNull();
        error!.Error.Should().Contain(line.Description).And.Contain("base física");
        tax.Verify(t => t.GetXmlIceCatalogEntryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(SriTaxCalculationType.Specific, 0)]
    [InlineData(SriTaxCalculationType.Mixed, 18.55)]
    [InlineData(SriTaxCalculationType.Percentage, 9.87)]
    public async Task XML_recognizes_inactive_code_and_preserves_declared_values(
        SriTaxCalculationType mode, double amount)
    {
        var line = Line();
        var tax = Resolver(mode, null);
        var rows = new[] {
            PurchaseReceptionLineTax.Create(Guid.NewGuid(), line.TenantId, "2", "4", 12m, 118.55m, 14.23m),
            PurchaseReceptionLineTax.Create(Guid.NewGuid(), line.TenantId, "3", "3031", 0.18m, 35.81m, (decimal)amount),
        };

        var error = await ReceptionTaxHelper.ApplyReceptionTaxesAsync(line, rows, true, tax.Object, CancellationToken.None);
        error.Should().BeNull();
        // Same shared policy used by Confirm and Recalculate, without activating the code.
        (await PurchaseTaxHelper.ResolveTaxesAsync(line, tax.Object, CancellationToken.None)).Should().BeNull();
        var ice = line.Taxes.Single(t => t.TaxCode == "3");
        ice.Rate.Should().Be(0.18m);
        ice.TaxableBase.Should().Be(35.81m);
        ice.TaxAmount.Should().Be((decimal)amount);
        ice.Source.Should().Be(PurchaseTaxSource.Xml);
        line.Taxes.Single(t => t.TaxCode == "2").Rate.Should().Be(12m);
    }

    [Theory]
    [InlineData(SriTaxCalculationType.Specific)]
    [InlineData(SriTaxCalculationType.Mixed)]
    public async Task Edited_XML_preserves_specific_or_mixed_fiscal_ICE(SriTaxCalculationType mode)
    {
        var line = Line();
        var rows = new[] {
            PurchaseReceptionLineTax.Create(Guid.NewGuid(), line.TenantId, "2", "4", 15m, 100m, 15m),
            PurchaseReceptionLineTax.Create(Guid.NewGuid(), line.TenantId, "3", "3031", 0m, 35.81m, 0m),
        };
        var error = await ReceptionTaxHelper.ApplyReceptionTaxesAsync(line, rows, false,
            Resolver(mode, null).Object, CancellationToken.None);
        error.Should().BeNull();
        line.Taxes.Single(t => t.TaxCode == "3").TaxAmount.Should().Be(0m);
        line.Taxes.Should().OnlyContain(t => t.Source == PurchaseTaxSource.Xml);
    }

    [Fact]
    public async Task Edited_XML_preserves_inactive_percentage_ICE()
    {
        var line = Line();
        var rows = new[] {
            PurchaseReceptionLineTax.Create(Guid.NewGuid(), line.TenantId, "2", "4", 15m, 100m, 15m),
            PurchaseReceptionLineTax.Create(Guid.NewGuid(), line.TenantId, "3", "3031", 10m, 100m, 10m),
        };
        var error = await ReceptionTaxHelper.ApplyReceptionTaxesAsync(line, rows, false,
            Resolver(SriTaxCalculationType.Percentage, null).Object, CancellationToken.None);
        error.Should().BeNull();
        line.Taxes.Single(t => t.TaxCode == "3").TaxAmount.Should().Be(10m);
        line.Taxes.Should().OnlyContain(t => t.Source == PurchaseTaxSource.Xml);
    }

    [Fact]
    public async Task Inactive_code_cannot_be_selected_manually()
    {
        var error = await PurchaseTaxHelper.ResolveTaxesAsync(Line(),
            Resolver(SriTaxCalculationType.Mixed, 10.41m).Object, CancellationToken.None);
        error.Should().NotBeNull();
        error!.Error.Should().Contain("inactivo");
    }
}
