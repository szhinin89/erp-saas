using ERP.Application.MasterData.UseCases.AddSupplierRetentionDefault;
using ERP.Application.Modules.Purchases.Services;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01 — <see cref="AddSupplierRetentionDefaultValidator"/>
/// valida que SriRetentionCodeId sea un concepto SELECCIONABLE del catálogo real (sri_retention_code) —
/// nunca se acepta un código huérfano o no habilitado al agregar un nuevo default.
/// ZH-SRI-RETENTION-CATALOG-SSOT-01: la habilitación se consulta a la lectura oficial
/// <see cref="IRetentionCodeResolver.GetSelectableByIdAsync"/> (ADR-037 D13), no con un filtro propio.
/// </summary>
public sealed class AddSupplierRetentionDefaultValidatorTests
{
    private static readonly Guid Concept728 = Guid.Parse("10000000-0000-0000-0000-000000000006");

    private readonly Mock<IRetentionCodeResolver> _resolver = new();

    private AddSupplierRetentionDefaultValidator CreateValidator() => new(_resolver.Object);

    [Fact]
    public async Task SriRetentionCodeId_vacio_es_invalido()
    {
        var cmd = new AddSupplierRetentionDefaultCommand(Guid.NewGuid(), Guid.Empty);

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("SriRetentionCodeId"));
    }

    [Fact]
    public async Task SriRetentionCodeId_seleccionable_es_valido()
    {
        var codeId = Guid.NewGuid();
        _resolver
            .Setup(r => r.GetSelectableByIdAsync(codeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new RetentionCodeInfo("RENTA", "303", "Honorarios profesionales", 10m, codeId)
            );

        var cmd = new AddSupplierRetentionDefaultCommand(Guid.NewGuid(), codeId);

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("SriRetentionCodeId"));
    }

    [Fact]
    public async Task SriRetentionCodeId_no_seleccionable_o_inexistente_es_invalido()
    {
        var codeId = Guid.NewGuid();
        _resolver
            .Setup(r => r.GetSelectableByIdAsync(codeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetentionCodeInfo?)null);

        var cmd = new AddSupplierRetentionDefaultCommand(Guid.NewGuid(), codeId);

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("SriRetentionCodeId"));
    }

    [Fact]
    public async Task No_se_puede_crear_un_default_nuevo_con_728_no_habilitado()
    {
        // 728 (IVA 15 %) existe y es legible como histórico, pero no es seleccionable.
        _resolver
            .Setup(r => r.GetSelectableByIdAsync(Concept728, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetentionCodeInfo?)null);
        _resolver
            .Setup(r => r.GetByIdIncludingDisabledAsync(Concept728, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new RetentionCodeInfo(
                    "IVA",
                    "728",
                    "Ret. IVA 15% – Constructoras",
                    15m,
                    Concept728,
                    IsActive: false
                )
            );

        var result = await CreateValidator()
            .ValidateAsync(new AddSupplierRetentionDefaultCommand(Guid.NewGuid(), Concept728));

        result.Errors.Should().Contain(e => e.PropertyName.Contains("SriRetentionCodeId"));
        _resolver.Verify(
            r => r.GetSelectableByIdAsync(Concept728, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
