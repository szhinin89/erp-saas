using ERP.Application.Common;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.MasterData.Constants;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// SALES-SETTLEMENT-CREDIT-01 — <see cref="SalesCreditRequirementPolicy"/> en aislamiento: orden
/// b) dueDate manual, c) schedule manual, e) default de empresa (org_settings vía
/// IInvoiceDefaultsResolver); y el fallback de Contado sembrado por el sistema
/// (<see cref="PaymentTermCodes.Cash"/>). Los órdenes a) explícito y d) default de cliente viven
/// en <c>IPaymentTermDefaultResolver</c> (probado en PaymentTermDefaultResolverTests.cs) — el
/// handler los invoca directamente, no esta política.
/// </summary>
public sealed class SalesCreditRequirementPolicyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IPaymentTermRepository> PaymentTerms { get; } = new();
        public Mock<IInvoiceDefaultsResolver> InvoiceDefaults { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            Branch.Setup(b => b.BranchId).Returns(BranchId);
            InvoiceDefaults
                .Setup(r =>
                    r.GetAsync(TenantId, CompanyId, BranchId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(
                    new InvoiceDefaultsResult(null, null, null, null, null, "None", true, Array.Empty<string>())
                );
        }

        public SalesCreditRequirementPolicy Build() =>
            new(PaymentTerms.Object, InvoiceDefaults.Object, Tenant.Object, Company.Object, Branch.Object);
    }

    [Fact]
    public async Task DueDate_manual_satisface_sin_resolver_PaymentTerm()
    {
        var f = new Fixture();

        var result = await f.Build().ResolveCompanyOrManualAsync(
            dueDate: new DateOnly(2026, 8, 1),
            hasManualSchedule: false,
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().BeNull();
        f.InvoiceDefaults.Verify(
            r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "dueDate manual no debe siquiera consultar el default de empresa"
        );
    }

    [Fact]
    public async Task Schedule_manual_satisface_sin_resolver_PaymentTerm()
    {
        var f = new Fixture();

        var result = await f.Build().ResolveCompanyOrManualAsync(
            dueDate: null,
            hasManualSchedule: true,
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Default_de_empresa_activo_se_usa_cuando_no_hay_dueDate_ni_schedule()
    {
        var f = new Fixture();
        var pt = PaymentTerm.Create(TenantId, "30D", "Crédito 30 días", 1, 30, UserId);
        f.InvoiceDefaults
            .Setup(r => r.GetAsync(TenantId, CompanyId, BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new InvoiceDefaultsResult(null, null, pt.Id, null, null, "None", true, Array.Empty<string>())
            );
        f.PaymentTerms
            .Setup(r => r.GetByIdAsync(TenantId, pt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pt);

        var result = await f.Build().ResolveCompanyOrManualAsync(null, false, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Be(pt);
    }

    [Fact]
    public async Task Default_de_empresa_inactivo_no_se_usa_y_falla()
    {
        var f = new Fixture();
        var pt = PaymentTerm.Create(TenantId, "30D", "Crédito 30 días", 1, 30, UserId);
        pt.Disable(UserId);
        f.InvoiceDefaults
            .Setup(r => r.GetAsync(TenantId, CompanyId, BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new InvoiceDefaultsResult(null, null, pt.Id, null, null, "None", true, Array.Empty<string>())
            );
        f.PaymentTerms
            .Setup(r => r.GetByIdAsync(TenantId, pt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pt);

        var result = await f.Build().ResolveCompanyOrManualAsync(null, false, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(SalesCreditRequirementPolicy.MissingCreditRuleMessage);
    }

    [Fact]
    public async Task Sin_dueDate_sin_schedule_sin_default_de_empresa_falla_con_el_mensaje_de_negocio()
    {
        var f = new Fixture();

        var result = await f.Build().ResolveCompanyOrManualAsync(null, false, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(
            "Debe definir una fecha de vencimiento, cuotas o una condición de pago para el saldo pendiente."
        );
    }

    [Fact]
    public async Task GetCashFallbackAsync_devuelve_el_Contado_sembrado_por_el_sistema()
    {
        var f = new Fixture();
        var contado = PaymentTerm.Create(TenantId, PaymentTermCodes.Cash, "Contado", 1, 0, UserId);
        f.PaymentTerms
            .Setup(r => r.GetByCodeAsync(TenantId, PaymentTermCodes.Cash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contado);

        var result = await f.Build().GetCashFallbackAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Be(contado);
    }

    [Fact]
    public async Task GetCashFallbackAsync_sin_Contado_sembrado_falla_con_mensaje_claro()
    {
        var f = new Fixture();
        f.PaymentTerms
            .Setup(r => r.GetByCodeAsync(TenantId, PaymentTermCodes.Cash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTerm?)null);

        var result = await f.Build().GetCashFallbackAsync(CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("contado");
    }
}
