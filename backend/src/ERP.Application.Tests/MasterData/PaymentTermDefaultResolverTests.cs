using ERP.Application.Common;
using ERP.Application.MasterData.Services;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>
/// ADR-033, Fases 3b/3c — cadena de resolución compartida por Compras (CompanyBpPurchaseSettings)
/// y Ventas (CompanyBpSalesSettings): explícito (validado activo) → default company-scoped del
/// tercero (validado activo) → exigir selección. Nunca "primer registro", nunca inferencia por
/// duración numérica, nunca condición inactiva, nunca fallback silencioso a
/// SupplierRoleConfig.PaymentTermId ni a un default genérico de empresa (ninguno de los dos
/// participa en esta clase en absoluto).
/// </summary>
public sealed class PaymentTermDefaultResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IPaymentTermRepository> PaymentTerms { get; } = new();
        public Mock<ICompanyBpPurchaseSettingsRepository> PurchaseSettings { get; } = new();
        public Mock<ICompanyBpSalesSettingsRepository> SalesSettings { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
        }

        public PaymentTermDefaultResolver BuildResolver() =>
            new(PaymentTerms.Object, PurchaseSettings.Object, SalesSettings.Object, Tenant.Object);
    }

    [Fact]
    public async Task Explicito_activo_se_usa_tal_cual()
    {
        var f = new Fixture();
        var pt = PaymentTerm.Create(TenantId, "30D", "30 días", 1, 30, UserId);
        f.PaymentTerms
            .Setup(r => r.GetByIdAsync(TenantId, pt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pt);

        var result = await f.BuildResolver().ResolveForPurchaseAsync(SupplierId, pt.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(pt);
        f.PurchaseSettings.Verify(
            r => r.GetByBusinessPartnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Explicito_inexistente_falla()
    {
        var f = new Fixture();
        var missingId = Guid.NewGuid();
        f.PaymentTerms
            .Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTerm?)null);

        var result = await f.BuildResolver().ResolveForPurchaseAsync(SupplierId, missingId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no existe");
    }

    [Fact]
    public async Task Explicito_inactivo_falla_y_no_cae_al_default()
    {
        var f = new Fixture();
        var pt = PaymentTerm.Create(TenantId, "30D", "30 días", 1, 30, UserId);
        pt.Disable(UserId);
        f.PaymentTerms
            .Setup(r => r.GetByIdAsync(TenantId, pt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pt);

        var result = await f.BuildResolver().ResolveForPurchaseAsync(SupplierId, pt.Id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactiva");
        f.PurchaseSettings.Verify(
            r => r.GetByBusinessPartnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Sin_explicito_usa_default_de_CompanyBpPurchaseSettings_si_esta_activo()
    {
        var f = new Fixture();
        var defaultPt = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId);
        var settings = CompanyBpPurchaseSettings.Create(
            TenantId, Guid.NewGuid(), SupplierId, defaultPt.Id, UserId
        );
        f.PurchaseSettings
            .Setup(r => r.GetByBusinessPartnerAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        f.PaymentTerms
            .Setup(r => r.GetByIdAsync(TenantId, defaultPt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultPt);

        var result = await f.BuildResolver().ResolveForPurchaseAsync(SupplierId, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(defaultPt);
    }

    [Fact]
    public async Task Sin_explicito_y_default_inactivo_exige_seleccion_no_cae_a_otro_lado()
    {
        var f = new Fixture();
        var defaultPt = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId);
        defaultPt.Disable(UserId);
        var settings = CompanyBpPurchaseSettings.Create(
            TenantId, Guid.NewGuid(), SupplierId, defaultPt.Id, UserId
        );
        f.PurchaseSettings
            .Setup(r => r.GetByBusinessPartnerAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        f.PaymentTerms
            .Setup(r => r.GetByIdAsync(TenantId, defaultPt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultPt);

        var result = await f.BuildResolver().ResolveForPurchaseAsync(SupplierId, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Debe seleccionar");
    }

    [Fact]
    public async Task Sin_explicito_y_sin_CompanyBpPurchaseSettings_exige_seleccion()
    {
        var f = new Fixture();
        f.PurchaseSettings
            .Setup(r => r.GetByBusinessPartnerAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyBpPurchaseSettings?)null);

        var result = await f.BuildResolver().ResolveForPurchaseAsync(SupplierId, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Debe seleccionar");
        f.PaymentTerms.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Sin_explicito_y_CompanyBpPurchaseSettings_sin_PaymentTermId_exige_seleccion()
    {
        var f = new Fixture();
        var settings = CompanyBpPurchaseSettings.Create(
            TenantId, Guid.NewGuid(), SupplierId, paymentTermId: null, UserId
        );
        f.PurchaseSettings
            .Setup(r => r.GetByBusinessPartnerAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var result = await f.BuildResolver().ResolveForPurchaseAsync(SupplierId, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Debe seleccionar");
    }

    [Fact]
    public async Task Dos_empresas_resuelven_su_propio_default_sin_mezclarse()
    {
        // El aislamiento tenant/company real de CompanyBpPurchaseSettings ya está probado contra
        // Postgres en CompanyBpPurchaseSettingsRepositoryTests (Fase 3a) — aquí solo se verifica
        // que el resolver usa tal cual lo que el repositorio le entrega (dependencia inyectada),
        // sin lógica propia de cruce entre empresas.
        var f = new Fixture();
        var ptA = PaymentTerm.Create(TenantId, "30D", "30 días", 1, 30, UserId);
        var companyAId = Guid.NewGuid();
        var settingsA = CompanyBpPurchaseSettings.Create(TenantId, companyAId, SupplierId, ptA.Id, UserId);
        f.PurchaseSettings
            .Setup(r => r.GetByBusinessPartnerAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settingsA);
        f.PaymentTerms
            .Setup(r => r.GetByIdAsync(TenantId, ptA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ptA);

        var result = await f.BuildResolver().ResolveForPurchaseAsync(SupplierId, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(ptA.Id);
    }

    // ── ResolveForSaleAsync — ADR-033, Fase 3c ──────────────────────────────────────────

    [Fact]
    public async Task Venta_explicito_activo_se_usa_tal_cual()
    {
        var f = new Fixture();
        var pt = PaymentTerm.Create(TenantId, "30D", "30 días", 1, 30, UserId);
        f.PaymentTerms
            .Setup(r => r.GetByIdAsync(TenantId, pt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pt);

        var result = await f.BuildResolver().ResolveForSaleAsync(CustomerId, pt.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(pt);
        f.SalesSettings.Verify(
            r => r.GetByBusinessPartnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Venta_explicito_inactivo_falla_y_no_cae_al_default()
    {
        var f = new Fixture();
        var pt = PaymentTerm.Create(TenantId, "30D", "30 días", 1, 30, UserId);
        pt.Disable(UserId);
        f.PaymentTerms
            .Setup(r => r.GetByIdAsync(TenantId, pt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pt);

        var result = await f.BuildResolver().ResolveForSaleAsync(CustomerId, pt.Id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactiva");
        f.SalesSettings.Verify(
            r => r.GetByBusinessPartnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Venta_sin_explicito_usa_default_de_CompanyBpSalesSettings_si_esta_activo()
    {
        var f = new Fixture();
        var defaultPt = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId);
        var settings = CompanyBpSalesSettings.Create(
            TenantId, Guid.NewGuid(), CustomerId, null, UserId
        );
        settings.SetPaymentTerm(defaultPt.Id, UserId);
        f.SalesSettings
            .Setup(r => r.GetByBusinessPartnerAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        f.PaymentTerms
            .Setup(r => r.GetByIdAsync(TenantId, defaultPt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultPt);

        var result = await f.BuildResolver().ResolveForSaleAsync(CustomerId, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(defaultPt);
    }

    [Fact]
    public async Task Venta_sin_explicito_y_default_inactivo_exige_seleccion()
    {
        var f = new Fixture();
        var defaultPt = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId);
        defaultPt.Disable(UserId);
        var settings = CompanyBpSalesSettings.Create(
            TenantId, Guid.NewGuid(), CustomerId, null, UserId
        );
        settings.SetPaymentTerm(defaultPt.Id, UserId);
        f.SalesSettings
            .Setup(r => r.GetByBusinessPartnerAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        f.PaymentTerms
            .Setup(r => r.GetByIdAsync(TenantId, defaultPt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultPt);

        var result = await f.BuildResolver().ResolveForSaleAsync(CustomerId, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Debe seleccionar");
    }

    [Fact]
    public async Task Venta_sin_explicito_y_sin_CompanyBpSalesSettings_exige_seleccion()
    {
        var f = new Fixture();
        f.SalesSettings
            .Setup(r => r.GetByBusinessPartnerAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyBpSalesSettings?)null);

        var result = await f.BuildResolver().ResolveForSaleAsync(CustomerId, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Debe seleccionar");
        f.PaymentTerms.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Venta_sin_explicito_y_CompanyBpSalesSettings_sin_PaymentTermId_exige_seleccion()
    {
        var f = new Fixture();
        var settings = CompanyBpSalesSettings.Create(
            TenantId, Guid.NewGuid(), CustomerId, null, UserId
        );
        f.SalesSettings
            .Setup(r => r.GetByBusinessPartnerAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var result = await f.BuildResolver().ResolveForSaleAsync(CustomerId, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Debe seleccionar");
        f.PaymentTerms.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
