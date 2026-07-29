using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// Fase 4 (ADR — Rediseño del módulo de Caja) — CreateSalesDraftHandler ya no recibe
/// EmissionPointId del cliente: lo resuelve exclusivamente desde ICurrentCashSession, junto con
/// CashSessionId, y rechaza la operación si el usuario no tiene una caja abierta.
/// </summary>
public sealed class CreateSalesDraftHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ISalesInvoiceRepository> Repo { get; } = new();
        public Mock<IBusinessPartnerRepository> BpRepo { get; } = new();
        public Mock<IBusinessPartnerRoleRepository> RoleRepo { get; } = new();
        public Mock<IPaymentTermRepository> PtRepo { get; } = new();
        public Mock<IPaymentMethodRepository> PmRepo { get; } = new();
        public Mock<IItemRepository> ItemRepo { get; } = new();
        public Mock<IEmissionPointRepository> EpRepo { get; } = new();
        public Mock<ISriTaxResolver> Tax { get; } = new();
        public Mock<IPricingResolver> Pricing { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();
        public Mock<ICurrentCashSession> CashSession { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            Branch.Setup(b => b.BranchId).Returns(BranchId);
            User.Setup(u => u.UserId).Returns(UserId);

            var bp = BusinessPartner.Create(
                TenantId,
                "05",
                "1710034065",
                PersonType.Natural,
                "Cliente Test",
                UserId
            );
            BpRepo
                .Setup(r => r.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(bp);
            // El mock siempre devuelve `bp`, independientemente del Id pedido — suficiente para
            // este test, que solo crea un draft con un único cliente.
            BpRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(bp);

            var role = BusinessPartnerRole.Create(TenantId, bp.Id, RoleType.Customer, UserId);
            RoleRepo
                .Setup(r =>
                    r.GetByTypeAsync(
                        It.IsAny<Guid>(),
                        RoleType.Customer,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(role);

            var pt = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId);
            PtRepo
                .Setup(r => r.ListAsync(TenantId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PaymentTerm> { pt });
            PtRepo
                .Setup(r =>
                    r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(pt);

            Tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));
        }

        public CreateSalesDraftHandler BuildHandler() =>
            new(
                Repo.Object,
                BpRepo.Object,
                RoleRepo.Object,
                PtRepo.Object,
                PmRepo.Object,
                ItemRepo.Object,
                EpRepo.Object,
                Tax.Object,
                Pricing.Object,
                Tenant.Object,
                Company.Object,
                Branch.Object,
                User.Object,
                CashSession.Object
            );

        public static CreateSalesDraftCommand ValidCommand() =>
            new(
                CustomerId,
                DateOnly.FromDateTime(DateTime.UtcNow),
                new List<SalesLineInput> { new(null, "Producto Test", 1, 100m, "10") }
            );
    }

    [Fact]
    public async Task Con_caja_abierta_crea_la_factura_y_asigna_CashSessionId_y_EmissionPointId_de_la_sesion()
    {
        var f = new Fixture();
        var cashSessionId = Guid.NewGuid();
        var emissionPointId = Guid.NewGuid();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(true);
        f.CashSession.Setup(c => c.CashSessionId).Returns(cashSessionId);
        f.CashSession.Setup(c => c.EmissionPointId).Returns(emissionPointId);
        f.EpRepo.Setup(r =>
                r.GetByIdAsync(emissionPointId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((ERP.Domain.Modules.Company.Entities.EmissionPoint?)null);

        SalesInvoice? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<SalesInvoice, CancellationToken>((inv, _) => captured = inv)
            .Returns(Task.CompletedTask);

        var handler = f.BuildHandler();
        var result = await handler.Handle(Fixture.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        captured.Should().NotBeNull();
        captured!.CashSessionId.Should().Be(cashSessionId);
        captured.EmissionPointId.Should().Be(emissionPointId);
        captured.BranchId.Should().Be(BranchId);
    }

    [Fact]
    public async Task Sin_caja_abierta_rechaza_con_el_mensaje_esperado()
    {
        var f = new Fixture();
        f.CashSession.Setup(c => c.HasOpenSession).Returns(false);

        var handler = f.BuildHandler();
        var result = await handler.Handle(Fixture.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No existe una caja abierta para realizar ventas.");
        f.Repo.Verify(
            r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.BpRepo.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "debe rechazar antes de tocar cualquier otro repositorio si no hay caja abierta"
        );
    }

    [Fact]
    public void El_cliente_nunca_puede_enviar_EmissionPointId_ni_CashSessionId()
    {
        typeof(CreateSalesDraftCommand)
            .GetProperty("EmissionPointId")
            .Should()
            .BeNull(
                "el punto de emisión se resuelve desde ICurrentCashSession, nunca desde el cliente"
            );
        typeof(CreateSalesDraftCommand)
            .GetProperty("CashSessionId")
            .Should()
            .BeNull(
                "la sesión de caja se resuelve desde ICurrentCashSession, nunca desde el cliente"
            );
    }
}
