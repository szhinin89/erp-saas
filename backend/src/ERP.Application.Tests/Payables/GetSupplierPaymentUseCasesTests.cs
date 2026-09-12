using ERP.Application.Common;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
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
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SupplierPayment CreatePayment(string systemNumber = "00000001", Guid? installmentId = null)
    {
        var methodId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
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
            new[] { new SupplierPaymentApplicationLineInput(installmentId ?? Guid.NewGuid(), 100m) },
            new[] { new SupplierPaymentAllocationInput(0, 0, 100m) },
            UserId
        );
    }

    /// <summary>CxP dueña de la cuota aplicada — mismo agregado que resuelve el detalle vía <c>GetByInstallmentIdAsync</c>.</summary>
    private static AccountsPayable CreatePayableWithInstallment(out Guid installmentId)
    {
        var issueDate = new DateOnly(2026, 8, 1);
        var payable = AccountsPayable.CreateFromOrigin(
            TenantId, CompanyId, BranchId, SupplierId,
            AccountsPayableOriginType.PurchaseInvoice, Guid.NewGuid(), "01",
            "001-001-000031760", issueDate, issueDate, UserId
        );
        payable.AddInstallment(1, new DateOnly(2026, 9, 3), 100m);
        installmentId = payable.Installments[0].Id;
        return payable;
    }

    [Fact]
    public async Task GetById_existente_retorna_el_detalle_completo()
    {
        var repo = new Mock<ISupplierPaymentRepository>();
        var accountsPayables = new Mock<IAccountsPayableRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var payment = CreatePayment();
        repo.Setup(r => r.GetByIdAsync(TenantId, payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        accountsPayables
            .Setup(r => r.GetByInstallmentIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountsPayable?)null);

        var handler = new GetSupplierPaymentByIdHandler(repo.Object, accountsPayables.Object, tenant.Object);
        var result = await handler.Handle(new GetSupplierPaymentByIdQuery(payment.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SystemNumber.Should().Be("00000001");
        result.Value.MethodLines.Should().HaveCount(1);
    }

    /// <summary>
    /// SUPPLIER-PAYMENT-DETAIL-APPLICATION-LINE-DISPLAY-NAMES-01 — la cuota aplicada trae
    /// documentNumber/installmentNumber/dueDate resueltos contra la CxP dueña, no solo el GUID.
    /// </summary>
    [Fact]
    public async Task GetById_resuelve_documentNumber_installmentNumber_y_dueDate_de_la_cuota_aplicada()
    {
        var payable = CreatePayableWithInstallment(out var installmentId);
        var payment = CreatePayment(installmentId: installmentId);

        var repo = new Mock<ISupplierPaymentRepository>();
        var accountsPayables = new Mock<IAccountsPayableRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        repo.Setup(r => r.GetByIdAsync(TenantId, payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        accountsPayables
            .Setup(r => r.GetByInstallmentIdAsync(TenantId, installmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);

        var handler = new GetSupplierPaymentByIdHandler(repo.Object, accountsPayables.Object, tenant.Object);
        var result = await handler.Handle(new GetSupplierPaymentByIdQuery(payment.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.ApplicationLines.Should().ContainSingle().Subject;
        line.AccountsPayableInstallmentId.Should().Be(installmentId);
        line.DocumentNumber.Should().Be("001-001-000031760");
        line.InstallmentNumber.Should().Be(1);
        line.DueDate.Should().Be(new DateOnly(2026, 9, 3));
        line.IssueDate.Should().Be(new DateOnly(2026, 8, 1));
        line.OriginType.Should().Be("PurchaseInvoice");
    }

    /// <summary>
    /// Reversar un pago no muta <c>ApplicationLines</c> (histórico intacto) — el detalle sigue
    /// resolviendo la misma información legible aunque el pago ya esté Reversed.
    /// </summary>
    [Fact]
    public async Task GetById_de_un_pago_reversado_sigue_mostrando_datos_legibles_de_la_cuota()
    {
        var payable = CreatePayableWithInstallment(out var installmentId);
        var payment = CreatePayment(installmentId: installmentId);
        payment.Reverse("Duplicado", UserId, DateTime.UtcNow);

        var repo = new Mock<ISupplierPaymentRepository>();
        var accountsPayables = new Mock<IAccountsPayableRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        repo.Setup(r => r.GetByIdAsync(TenantId, payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        accountsPayables
            .Setup(r => r.GetByInstallmentIdAsync(TenantId, installmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payable);

        var handler = new GetSupplierPaymentByIdHandler(repo.Object, accountsPayables.Object, tenant.Object);
        var result = await handler.Handle(new GetSupplierPaymentByIdQuery(payment.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be("Reversed");
        var line = result.Value.ApplicationLines.Should().ContainSingle().Subject;
        line.DocumentNumber.Should().Be("001-001-000031760");
        line.InstallmentNumber.Should().Be(1);
    }

    /// <summary>
    /// Si la cuota ya no puede resolverse (caso excepcional), el detalle no se rompe — la línea
    /// simplemente queda con los campos de proyección en null (fallback técnico en frontend).
    /// </summary>
    [Fact]
    public async Task GetById_si_no_resuelve_la_cuota_no_rompe_el_detalle_y_deja_los_campos_en_null()
    {
        var payment = CreatePayment();

        var repo = new Mock<ISupplierPaymentRepository>();
        var accountsPayables = new Mock<IAccountsPayableRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        repo.Setup(r => r.GetByIdAsync(TenantId, payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        accountsPayables
            .Setup(r => r.GetByInstallmentIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountsPayable?)null);

        var handler = new GetSupplierPaymentByIdHandler(repo.Object, accountsPayables.Object, tenant.Object);
        var result = await handler.Handle(new GetSupplierPaymentByIdQuery(payment.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.ApplicationLines.Should().ContainSingle().Subject;
        line.DocumentNumber.Should().BeNull();
        line.InstallmentNumber.Should().BeNull();
        line.DueDate.Should().BeNull();
    }

    [Fact]
    public async Task GetById_inexistente_retorna_NotFound()
    {
        var repo = new Mock<ISupplierPaymentRepository>();
        var accountsPayables = new Mock<IAccountsPayableRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var missingId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupplierPayment?)null);

        var handler = new GetSupplierPaymentByIdHandler(repo.Object, accountsPayables.Object, tenant.Object);
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
