using ERP.Application.MasterData.UseCases.GetSupplierRetentionDefaults;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>
/// ZH-SRI-RETENTION-CATALOG-SSOT-01 — un default histórico que referencia un concepto ya no habilitado
/// (728 / IVA 15 %) sigue siendo legible: se muestra con su estado de catálogo, nunca se oculta ni se borra.
/// </summary>
public sealed class GetSupplierRetentionDefaultsHandlerTests
{
    private static readonly Guid Concept728 = Guid.Parse("10000000-0000-0000-0000-000000000006");

    [Fact]
    public async Task Default_historico_con_728_no_habilitado_sigue_siendo_legible()
    {
        var supplierId = Guid.NewGuid();
        var entry = SupplierRetentionDefault.Create(Guid.NewGuid(), Guid.NewGuid(), supplierId, Concept728, 1, Guid.NewGuid());

        var repo = new Mock<ISupplierRetentionDefaultRepository>();
        repo.Setup(r => r.GetByBusinessPartnerAsync(supplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([entry]);
        var catalog = new Mock<ISriCatalogLookupRepository>();
        catalog.Setup(c => c.GetRetentionCodeByIdAsync(Concept728, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriRetentionCode
            {
                Id = Concept728,
                TaxType = "IVA",
                Code = "728",
                Name = "Ret. IVA 15% – Constructoras",
                Percentage = 15m,
                IsActive = false,
            });

        var result = await new GetSupplierRetentionDefaultsHandler(repo.Object, catalog.Object)
            .Handle(new GetSupplierRetentionDefaultsQuery(supplierId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!.Should().ContainSingle().Subject;
        dto.SriRetentionCodeId.Should().Be(Concept728);
        dto.Code.Should().Be("728");
        dto.Percentage.Should().Be(15m);
        dto.IsCatalogCodeActive.Should().BeFalse("se muestra como concepto no habilitado, no se oculta");
        dto.IsActive.Should().BeTrue("el default del proveedor no se modifica");
    }
}
