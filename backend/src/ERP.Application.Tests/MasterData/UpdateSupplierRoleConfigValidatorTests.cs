using ERP.Application.MasterData.UseCases.UpdateRoleConfig;
using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>
/// SUPPLIER-SRI-CATALOGS-01 — <see cref="UpdateSupplierRoleConfigValidator"/> ya no valida
/// <c>DefaultPaymentMethodCode</c> contra un HashSet fijo de Domain: valida los 5 códigos SRI de
/// forma async contra sus catálogos reales (globales, sin tenant/company scope). Cubre: código
/// activo válido, código inexistente/inactivo, null permitido sin consultar el repo, y que
/// Retención IVA/Renta no acepten el código del otro tipo de impuesto.
/// </summary>
public sealed class UpdateSupplierRoleConfigValidatorTests
{
    private static readonly Guid RoleId = Guid.NewGuid();

    private readonly Mock<ISriCatalogLookupRepository> _catalogRepo = new();
    private readonly Mock<IRetentionCodeResolver> _retentionResolver = new();

    private UpdateSupplierRoleConfigValidator CreateValidator() =>
        new(_catalogRepo.Object, _retentionResolver.Object);

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

    // ── DefaultRetentionVatCode (IVA) ────────────────────────────────────────

    [Fact]
    public async Task DefaultRetentionVatCode_null_es_valido_sin_consultar_el_resolver()
    {
        var cmd = CommandWith(SupplierRoleConfig.Create());

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("DefaultRetentionVatCode"));
        _retentionResolver.Verify(
            r => r.GetRetentionCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DefaultRetentionVatCode_activo_como_IVA_es_valido()
    {
        _retentionResolver
            .Setup(r => r.GetRetentionCodeAsync("725", "IVA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetentionCodeInfo("725", "Retención 100% IVA", 100m));

        var cmd = CommandWith(SupplierRoleConfig.Create(defaultRetentionVatCode: "725"));

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("DefaultRetentionVatCode"));
    }

    [Fact]
    public async Task DefaultRetentionVatCode_inexistente_o_inactivo_es_invalido()
    {
        _retentionResolver
            .Setup(r => r.GetRetentionCodeAsync("999", "IVA", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetentionCodeInfo?)null);

        var cmd = CommandWith(SupplierRoleConfig.Create(defaultRetentionVatCode: "999"));

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("DefaultRetentionVatCode"));
    }

    [Fact]
    public async Task DefaultRetentionVatCode_no_acepta_un_codigo_que_solo_existe_como_RENTA()
    {
        // El código "303" existe, pero solo bajo taxType RENTA — el resolver, consultado con
        // "IVA", debe devolver null (no matchea TaxType), y el validator debe rechazarlo.
        _retentionResolver
            .Setup(r => r.GetRetentionCodeAsync("303", "IVA", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetentionCodeInfo?)null);

        var cmd = CommandWith(SupplierRoleConfig.Create(defaultRetentionVatCode: "303"));

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("DefaultRetentionVatCode"));
    }

    // ── DefaultRetentionIncomeCode (RENTA) ───────────────────────────────────

    [Fact]
    public async Task DefaultRetentionIncomeCode_null_es_valido_sin_consultar_el_resolver()
    {
        var cmd = CommandWith(SupplierRoleConfig.Create());

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("DefaultRetentionIncomeCode"));
    }

    [Fact]
    public async Task DefaultRetentionIncomeCode_activo_como_RENTA_es_valido()
    {
        _retentionResolver
            .Setup(r => r.GetRetentionCodeAsync("303", "RENTA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetentionCodeInfo("303", "Honorarios profesionales", 10m));

        var cmd = CommandWith(SupplierRoleConfig.Create(defaultRetentionIncomeCode: "303"));

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName.Contains("DefaultRetentionIncomeCode"));
    }

    [Fact]
    public async Task DefaultRetentionIncomeCode_inexistente_o_inactivo_es_invalido()
    {
        _retentionResolver
            .Setup(r => r.GetRetentionCodeAsync("999", "RENTA", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetentionCodeInfo?)null);

        var cmd = CommandWith(SupplierRoleConfig.Create(defaultRetentionIncomeCode: "999"));

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("DefaultRetentionIncomeCode"));
    }

    [Fact]
    public async Task DefaultRetentionIncomeCode_no_acepta_un_codigo_que_solo_existe_como_IVA()
    {
        _retentionResolver
            .Setup(r => r.GetRetentionCodeAsync("725", "RENTA", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetentionCodeInfo?)null);

        var cmd = CommandWith(SupplierRoleConfig.Create(defaultRetentionIncomeCode: "725"));

        var result = await CreateValidator().ValidateAsync(cmd);

        result.Errors.Should().Contain(e => e.PropertyName.Contains("DefaultRetentionIncomeCode"));
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
