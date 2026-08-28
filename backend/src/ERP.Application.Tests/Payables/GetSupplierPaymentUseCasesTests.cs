using ERP.Application.Common;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Payables;

/// <summary>
/// SUPPLIER-PAYMENTS-FRONTEND-15E — cobertura mínima de las queries de solo lectura que el
/// frontend consume (lista y detalle), mismo patrón que <c>AccountsPayableQueryUseCasesTests</c>.
/// </summary>
public sealed class GetSupplierPaymentUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SupplierPayment CreatePayment(string systemNumber = "00000001")
    {
        var methodId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();
        return SupplierPayment.Create(
            TenantId,
            CompanyId,
            Guid.NewGuid(),
            SupplierId,
            new DateOnly(2026, 8, 28),
            100m,
            systemNumber,
            null,
            new[] { new SupplierPaymentMethodLineInput(methodId, destinationId, 100m) },
            new[] { new SupplierPaymentApplicationLineInput(installmentId, 100m) },
            new[] { new SupplierPaymentAllocationInput(0, 0, 100m) },
            UserId
        );
    }

    [Fact]
    public async Task GetById_existente_retorna_el_detalle_completo()
    {
        var repo = new Mock<ISupplierPaymentRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var payment = CreatePayment();
        repo.Setup(r => r.GetByIdAsync(TenantId, payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var handler = new GetSupplierPaymentByIdHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(new GetSupplierPaymentByIdQuery(payment.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SystemNumber.Should().Be("00000001");
        result.Value.MethodLines.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_inexistente_retorna_NotFound()
    {
        var repo = new Mock<ISupplierPaymentRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var missingId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupplierPayment?)null);

        var handler = new GetSupplierPaymentByIdHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(new GetSupplierPaymentByIdQuery(missingId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task GetList_mapea_items_con_nombre_de_proveedor()
    {
        var repo = new Mock<ISupplierPaymentRepository>();
        var partners = new Mock<IBusinessPartnerRepository>();
        var tenant = new Mock<ICurrentTenant>();
        var company = new Mock<ICurrentCompany>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        company.Setup(c => c.CompanyId).Returns(CompanyId);

        var payment = CreatePayment();
        repo.Setup(r =>
                r.SearchAsync(TenantId, CompanyId, null, null, 1, 25, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((new[] { payment }, 1));
        partners
            .Setup(p => p.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Dictionary<Guid, string> { [SupplierId] = "Proveedor de Prueba S.A." }
            );

        var handler = new GetSupplierPaymentsListHandler(repo.Object, partners.Object, tenant.Object, company.Object);
        var result = await handler.Handle(new GetSupplierPaymentsListQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle();
        result.Value.Items[0].SupplierName.Should().Be("Proveedor de Prueba S.A.");
        result.Value.Items[0].DisplayNumber.Should().Be("00000001");
    }
}
