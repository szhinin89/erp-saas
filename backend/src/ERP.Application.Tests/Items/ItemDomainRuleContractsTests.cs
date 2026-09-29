using ERP.Application.Common;
using ERP.Application.Items.UseCases.AddItemVariant;
using ERP.Application.Items.UseCases.Barcodes;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Items;

/// <summary>
/// ZH-DOMAIN-RULE-ERROR-SSOT-01B — catches tipados residuales de Items con contrato HTTP distinto
/// deliberado (no son la traducción genérica a DOMAIN_RULE_VIOLATION): duplicado → 409 CONFLICT,
/// código de barras inexistente → 404 NOT_FOUND. Se deciden por el TIPO de la excepción, nunca por
/// su texto, y el mensaje público del dominio se conserva.
/// </summary>
public sealed class ItemDomainRuleContractsTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static (Item Item, ItemVariant Variant) ItemWithVariant()
    {
        var item = Item.Create(
            TenantId, "SKU-001", "Item de prueba", "Descripción", Guid.NewGuid(), "UNIT",
            ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(), UserId);
        var variant = item.AddVariant([], "SKU-V1", 1, UserId);
        return (item, variant);
    }

    private static Mock<ICurrentTenant> Tenant()
    {
        var t = new Mock<ICurrentTenant>();
        t.Setup(x => x.TenantId).Returns(TenantId);
        return t;
    }

    private static Mock<ICurrentUser> User()
    {
        var u = new Mock<ICurrentUser>();
        u.Setup(x => x.UserId).Returns(UserId);
        return u;
    }

    [Fact]
    public async Task Codigo_de_barras_repetido_en_la_variante_es_409_CONFLICT_sin_persistir()
    {
        var (item, variant) = ItemWithVariant();
        variant.AddBarcode("7501234567890", "EAN13", TenantId, UserId);
        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        repo.Setup(r => r.BarcodeExistsAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var catalog = new Mock<IItemCatalogRepository>();
        catalog.Setup(c => c.BarcodeTypeExistsAndActiveAsync("EAN13", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await new AddBarcodeHandler(repo.Object, catalog.Object, Tenant().Object, User().Object)
            .Handle(new AddBarcodeCommand(item.Id, variant.Id, "7501234567890", "EAN13"), CancellationToken.None);

        (result.Code, result.Error).Should().Be((ApiResponseCodes.Common.Conflict, "El código de barras '7501234567890' ya existe en esta variante."));
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deshabilitar_codigo_de_barras_inexistente_es_404_NOT_FOUND()
    {
        var (item, variant) = ItemWithVariant();
        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await new DisableBarcodeHandler(repo.Object, Tenant().Object, User().Object)
            .Handle(new DisableBarcodeCommand(item.Id, variant.Id, Guid.NewGuid()), CancellationToken.None);

        (result.Code, result.Error).Should().Be((ApiResponseCodes.Common.NotFound, "Código de barras no encontrado en esta variante."));
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Variante_con_SKU_repetido_es_409_CONFLICT_sin_persistir()
    {
        var (item, _) = ItemWithVariant();
        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await new AddItemVariantCommandHandler(repo.Object, Tenant().Object, User().Object)
            .Handle(new AddItemVariantCommand(item.Id, [], "SKU-V1", 2), CancellationToken.None);

        (result.Code, result.Error).Should().Be((ApiResponseCodes.Common.Conflict, "Ya existe una variante con SKU 'SKU-V1'."));
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
