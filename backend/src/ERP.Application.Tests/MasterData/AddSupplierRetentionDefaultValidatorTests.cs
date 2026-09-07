using ERP.Application.MasterData.UseCases.AddSupplierRetentionDefault;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01 — <see cref="AddSupplierRetentionDefaultValidator"/>
/// valida que SriRetentionCodeId corresponda a un código activo del catálogo real
/// (sri_retention_code) — nunca se acepta un código huérfano/inactivo al agregar un nuevo default.
/// </summary>
public sealed class AddSupplierRetentionDefaultValidatorTests
{
    private readonly Mock<ISriCatalogLookupRepository> _catalogRepo = new();

    private AddSupplierRetentionDefaultValidator CreateValidator() => new(_catalogRepo.Object);

    [Fact]
    public async Task SriRetentionCodeId_vacio_es_invalido()
    {
        var cmd = new AddSupplierRetentionDefaultCommand(Guid.NewGuid(), Guid.Empty);

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("SriRetentionCodeId"));
    }

    [Fact]
    public async Task SriRetentionCodeId_activo_en_catalogo_es_valido()
    {
        var codeId = Guid.NewGuid();
        _catalogRepo
            .Setup(r => r.GetRetentionCodeByIdAsync(codeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SriRetentionCode
                {
                    Id = codeId,
                    TaxType = "RENTA",
                    Code = "303",
                    Name = "Honorarios profesionales",
                    Percentage = 10m,
                    IsActive = true,
                }
            );

        var cmd = new AddSupplierRetentionDefaultCommand(Guid.NewGuid(), codeId);

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("SriRetentionCodeId"));
    }

    [Fact]
    public async Task SriRetentionCodeId_inactivo_en_catalogo_es_invalido()
    {
        var codeId = Guid.NewGuid();
        _catalogRepo
            .Setup(r => r.GetRetentionCodeByIdAsync(codeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SriRetentionCode
                {
                    Id = codeId,
                    TaxType = "RENTA",
                    Code = "303",
                    Name = "Honorarios profesionales",
                    Percentage = 10m,
                    IsActive = false,
                }
            );

        var cmd = new AddSupplierRetentionDefaultCommand(Guid.NewGuid(), codeId);

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("SriRetentionCodeId"));
    }

    [Fact]
    public async Task SriRetentionCodeId_inexistente_es_invalido()
    {
        var codeId = Guid.NewGuid();
        _catalogRepo
            .Setup(r => r.GetRetentionCodeByIdAsync(codeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SriRetentionCode?)null);

        var cmd = new AddSupplierRetentionDefaultCommand(Guid.NewGuid(), codeId);

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("SriRetentionCodeId"));
    }
}
