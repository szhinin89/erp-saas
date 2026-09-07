using ERP.Application.MasterData.UseCases.UpdateRoleConfig;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>
/// SUPPLIER-SRI-CATALOGS-01 — <see cref="UpdateSupplierRoleConfigValidator"/> ya no valida
/// <c>DefaultPaymentMethodCode</c> contra un HashSet fijo de Domain: valida los códigos SRI de
/// forma async contra sus catálogos reales (globales, sin tenant/company scope). Cubre: código
/// activo válido, código inexistente/inactivo, null permitido sin consultar el repo.
///
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01: DefaultRetentionVatCode/DefaultRetentionIncomeCode se
/// eliminaron de SupplierRoleConfig — su cobertura de validación contra catálogo ahora vive en
/// AddSupplierRetentionDefaultValidatorTests, sobre la lista dinámica.
/// </summary>
public sealed class UpdateSupplierRoleConfigValidatorTests
{
    private static readonly Guid RoleId = Guid.NewGuid();

    private readonly Mock<ISriCatalogLookupRepository> _catalogRepo = new();

    private UpdateSupplierRoleConfigValidator CreateValidator() => new(_catalogRepo.Object);

    private static UpdateSupplierRoleConfigCommand CommandWith(SupplierRoleConfig config) =>
        new(RoleId, config);

    // ── DefaultTaxSupportCode ────────────────────────────────────────────────

    [Fact]
    public async Task DefaultTaxSupportCode_null_es_valido_sin_consultar_el_catalogo()
    {
        var cmd = CommandWith(SupplierRoleConfig.Create());

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("DefaultTaxSupportCode"));
        _catalogRepo.Verify(
            r => r.TaxSupportCodeExistsActiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DefaultTaxSupportCode_activo_en_el_catalogo_es_valido()
    {
        _catalogRepo
            .Setup(r => r.TaxSupportCodeExistsActiveAsync("01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cmd = CommandWith(SupplierRoleConfig.Create(defaultTaxSupportCode: "01"));

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("DefaultTaxSupportCode"));
    }

    [Fact]
    public async Task DefaultTaxSupportCode_inexistente_o_inactivo_es_invalido()
    {
        _catalogRepo
            .Setup(r => r.TaxSupportCodeExistsActiveAsync("99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var cmd = CommandWith(SupplierRoleConfig.Create(defaultTaxSupportCode: "99"));

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("DefaultTaxSupportCode"));
    }

    // ── DefaultPaymentMethodCode ──────────────────────────────────────────────

    [Fact]
    public async Task DefaultPaymentMethodCode_null_es_valido_sin_consultar_el_catalogo()
    {
        var cmd = CommandWith(SupplierRoleConfig.Create());

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("DefaultPaymentMethodCode"));
        _catalogRepo.Verify(
            r => r.PaymentMethodCodeExistsActiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DefaultPaymentMethodCode_activo_en_el_catalogo_es_valido()
    {
        _catalogRepo
            .Setup(r => r.PaymentMethodCodeExistsActiveAsync("01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cmd = CommandWith(SupplierRoleConfig.Create(defaultPaymentMethodCode: "01"));

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("DefaultPaymentMethodCode"));
    }

    [Fact]
    public async Task DefaultPaymentMethodCode_inexistente_o_inactivo_es_invalido()
    {
        _catalogRepo
            .Setup(r => r.PaymentMethodCodeExistsActiveAsync("99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var cmd = CommandWith(SupplierRoleConfig.Create(defaultPaymentMethodCode: "99"));

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("DefaultPaymentMethodCode"));
    }

    // ── RefundProviderTypeCode ────────────────────────────────────────────────

    [Fact]
    public async Task RefundProviderTypeCode_null_es_valido_sin_consultar_el_catalogo()
    {
        var cmd = CommandWith(SupplierRoleConfig.Create());

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("RefundProviderTypeCode"));
        _catalogRepo.Verify(
            r => r.SupplierTypeCodeExistsActiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task RefundProviderTypeCode_activo_en_el_catalogo_es_valido()
    {
        _catalogRepo
            .Setup(r => r.SupplierTypeCodeExistsActiveAsync("01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cmd = CommandWith(SupplierRoleConfig.Create(refundProviderTypeCode: "01"));

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("RefundProviderTypeCode"));
    }

    [Fact]
    public async Task RefundProviderTypeCode_inexistente_o_inactivo_es_invalido()
    {
        _catalogRepo
            .Setup(r => r.SupplierTypeCodeExistsActiveAsync("99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var cmd = CommandWith(SupplierRoleConfig.Create(refundProviderTypeCode: "99"));

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("RefundProviderTypeCode"));
    }
}
