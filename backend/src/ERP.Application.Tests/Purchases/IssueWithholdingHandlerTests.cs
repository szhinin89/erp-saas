using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// Regresión del bug SRI [65] FECHA EMISIÓN EXTEMPORÁNEA en Retenciones (Compras):
/// <see cref="IssueWithholdingHandler"/> debe rechazar fechas de emisión futuras o fuera de la
/// tolerancia SRI (90 días) ANTES de capturar secuencial, usando la fecha empresarial de
/// <see cref="ICompanyClock"/> — nunca <c>DateTime.UtcNow.Date</c>. Mismo patrón ya corregido en
/// Ventas (ver AuthorizeSalesInvoiceHandlerTests) — aquí se corrige la fuente original del bug:
/// usePurchasesPage.ts generaba la fecha con <c>new Date().toISOString()</c> (UTC).
/// </summary>
public sealed class IssueWithholdingHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PtId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();

    /// <summary>Compra confirmada sin códigos de retención en el proveedor (config = null) —
    /// aísla exclusivamente la validación de fecha: si pasa esa validación, el handler falla
    /// después por "Sin códigos de retención configurados", nunca llega a capturar secuencial.</summary>
    private static PurchaseInvoice CreateConfirmedInvoice(DateOnly issueDate)
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000001",
            issueDate,
            UserId,
            PtId,
            "Contado",
            1,
            30
        );

        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto Test",
            quantity: 1,
            unitPrice: 100m,
            vatCode: "10",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, UserId);
        inv.Confirm(UserId);
        return inv;
    }

    private static (IssueWithholdingHandler handler, Mock<ICompanyClock> companyClock) BuildHandler(
        PurchaseInvoice inv,
        DateOnly companyToday
    )
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        repo.Setup(r =>
                r.GetWithholdingByPurchaseIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((IssuedWithholding?)null);

        var roleRepo = new Mock<IBusinessPartnerRoleRepository>();
        roleRepo
            .Setup(r =>
                r.GetByTypeAsync(
                    SupplierId,
                    ERP.Domain.MasterData.Enums.RoleType.Supplier,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ERP.Domain.MasterData.Entities.BusinessPartnerRole?)null);

        var companyClock = new Mock<ICompanyClock>();
        companyClock
            .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyToday);

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var handler = new IssueWithholdingHandler(
            repo.Object,
            Mock.Of<IAccountsPayableRepository>(),
            roleRepo.Object,
            Mock.Of<IRetentionCodeResolver>(),
            Mock.Of<IEmissionPointRepository>(),
            Mock.Of<IEstablishmentRepository>(),
            Mock.Of<IDocumentSequenceRepository>(),
            Mock.Of<IPurchaseReturnRepository>(),
            Mock.Of<IUnitOfWork>(),
            companyClock.Object,
            tenant.Object,
            company.Object,
            user.Object
        );

        return (handler, companyClock);
    }

    [Fact]
    public async Task Rejects_future_issue_date()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateConfirmedInvoice(issueDate: today.AddDays(-1));
        var (handler, _) = BuildHandler(inv, today);

        var result = await handler.Handle(
            new IssueWithholdingCommand(inv.Id, EmissionPointId, today.AddDays(4)),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no puede ser posterior");
    }

    [Fact]
    public async Task Rejects_issue_date_older_than_90_days()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateConfirmedInvoice(issueDate: today);
        var (handler, _) = BuildHandler(inv, today);

        var result = await handler.Handle(
            new IssueWithholdingCommand(inv.Id, EmissionPointId, today.AddDays(-91)),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("excede el rango permitido");
    }

    [Fact]
    public async Task Accepts_issue_date_exactly_90_days_old_boundary()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateConfirmedInvoice(issueDate: today);
        var (handler, companyClock) = BuildHandler(inv, today);

        var result = await handler.Handle(
            new IssueWithholdingCommand(inv.Id, EmissionPointId, today.AddDays(-90)),
            CancellationToken.None
        );

        // Pasa la validación de fecha y llega al siguiente gate de negocio (sin códigos de
        // retención configurados) — nunca falla por fecha.
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotContain("excede el rango permitido");
        result.Error.Should().NotContain("no puede ser posterior");
        companyClock.Verify(
            c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Accepts_issue_date_equal_to_company_local_today()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateConfirmedInvoice(issueDate: today);
        var (handler, companyClock) = BuildHandler(inv, today);

        var result = await handler.Handle(
            new IssueWithholdingCommand(inv.Id, EmissionPointId, today),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Sin códigos de retención configurados en el proveedor.");
        companyClock.Verify(
            c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Uses_company_clock_not_utc_now_for_date_comparison()
    {
        // Congela "hoy" en un valor arbitrario que NO coincide con DateTime.UtcNow.Date real —
        // si el handler alguna vez volviera a usar DateTime.UtcNow.Date en lugar de
        // ICompanyClock, esta retención (fechada "ayer" respecto al UTC real de la máquina de
        // pruebas) fallaría de forma intermitente según la hora en que corra la suite.
        var companyToday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var inv = CreateConfirmedInvoice(issueDate: companyToday);
        var (handler, companyClock) = BuildHandler(inv, companyToday);

        var result = await handler.Handle(
            new IssueWithholdingCommand(inv.Id, EmissionPointId, companyToday),
            CancellationToken.None
        );

        result.Error.Should().Be("Sin códigos de retención configurados en el proveedor.");
        companyClock.Verify(
            c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
