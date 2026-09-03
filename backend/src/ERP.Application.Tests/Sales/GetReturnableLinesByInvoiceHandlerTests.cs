using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>P0-01 Fase 4 — GetReturnableLinesByInvoiceQuery: remanente devolvible por línea.</summary>
public sealed class GetReturnableLinesByInvoiceHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid CashSessionId = Guid.NewGuid();

    private static (SalesInvoice Invoice, List<SalesInvoiceDetail> Lines) BuildAuthorizedInvoice(
        params (string Description, decimal Quantity, decimal UnitPrice)[] lineSpecs
    )
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);

        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "001-001-000000001",
            issueDate: new DateOnly(2026, 7, 25),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId
        );

        var lines = lineSpecs
            .Select(s =>
                SalesInvoiceDetail.Create(
                    inv.Id,
                    TenantId,
                    s.Description,
                    s.Quantity,
                    s.UnitPrice,
                    vatCode: "0",
                    uomCode: "UNIT"
                )
            )
            .ToList();
        inv.ReplaceLines(lines, UserId);

        var total = lines.Sum(l => l.TaxInclusiveTotal);
        var payment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            total
        );
        inv.ReplacePayments(new[] { payment }, UserId);

        inv.Authorize(UserId);
        return (inv, inv.Lines.ToList());
    }

    private static GetReturnableLinesByInvoiceHandler BuildHandler(
        SalesInvoice invoice,
        Mock<ISalesReturnRepository> returnRepo,
        Guid? activeBranchId = null
    )
    {
        var invoiceRepo = new Mock<ISalesInvoiceRepository>();
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var tenant = Mock.Of<ERP.Application.Common.ICurrentTenant>(t => t.TenantId == TenantId);
        var branch = Mock.Of<ERP.Application.Common.ICurrentBranch>(b =>
            b.BranchId == (activeBranchId ?? BranchId)
        );

        return new GetReturnableLinesByInvoiceHandler(
            invoiceRepo.Object,
            returnRepo.Object,
            tenant,
            branch
        );
    }

    [Fact]
    public async Task Sin_devoluciones_previas_devuelve_la_cantidad_completa_como_remanente()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m));

        var returnRepo = new Mock<ISalesReturnRepository>();
        returnRepo
            .Setup(r =>
                r.GetReturnedQuantityByInvoiceDetailAsync(
                    TenantId,
                    lines[0].Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(0m);

        var handler = BuildHandler(invoice, returnRepo);

        var result = await handler.Handle(
            new GetReturnableLinesByInvoiceQuery(invoice.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        var line = result.Value!.Should().ContainSingle().Which;
        line.OriginalQuantity.Should().Be(10m);
        line.ReturnedQuantity.Should().Be(0m);
        line.RemainingQuantity.Should().Be(10m);
    }

    [Fact]
    public async Task Con_devoluciones_autorizadas_calcula_el_remanente_correcto()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m));

        var returnRepo = new Mock<ISalesReturnRepository>();
        returnRepo
            .Setup(r =>
                r.GetReturnedQuantityByInvoiceDetailAsync(
                    TenantId,
                    lines[0].Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(4m);

        var handler = BuildHandler(invoice, returnRepo);

        var result = await handler.Handle(
            new GetReturnableLinesByInvoiceQuery(invoice.Id),
            CancellationToken.None
        );

        var line = result.Value!.Should().ContainSingle().Which;
        line.OriginalQuantity.Should().Be(10m);
        line.ReturnedQuantity.Should().Be(4m);
        line.RemainingQuantity.Should().Be(6m);
    }

    [Fact]
    public async Task Ignora_devoluciones_en_borrador()
    {
        // Usa un doble en memoria que replica el filtro real de estado (no un mock con valor
        // fijo) — así el test verifica genuinamente que una devolución Draft no reduce el
        // remanente, en vez de asumirlo.
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m));

        var draftReturn = BuildDraftReturnWithLine(invoice.Id, lines[0], quantity: 4m);
        var authorizedReturn = BuildAuthorizedReturnWithLine(invoice.Id, lines[0], quantity: 2m);

        var fakeRepo = new InMemorySalesReturnRepository();
        fakeRepo.Returns.Add(draftReturn);
        fakeRepo.Returns.Add(authorizedReturn);

        var handler = BuildHandlerWithFakeRepo(invoice, fakeRepo);

        var result = await handler.Handle(
            new GetReturnableLinesByInvoiceQuery(invoice.Id),
            CancellationToken.None
        );

        var line = result.Value!.Single();
        line.ReturnedQuantity.Should().Be(2m, because: "solo debe contar la devolución Authorized");
        line.RemainingQuantity.Should().Be(8m);
    }

    [Fact]
    public async Task Ignora_devoluciones_canceladas()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m));

        var cancelledReturn = BuildDraftReturnWithLine(invoice.Id, lines[0], quantity: 5m);
        cancelledReturn.Cancel(UserId);
        var authorizedReturn = BuildAuthorizedReturnWithLine(invoice.Id, lines[0], quantity: 1m);

        var fakeRepo = new InMemorySalesReturnRepository();
        fakeRepo.Returns.Add(cancelledReturn);
        fakeRepo.Returns.Add(authorizedReturn);

        var handler = BuildHandlerWithFakeRepo(invoice, fakeRepo);

        var result = await handler.Handle(
            new GetReturnableLinesByInvoiceQuery(invoice.Id),
            CancellationToken.None
        );

        var line = result.Value!.Single();
        line.ReturnedQuantity.Should().Be(1m, because: "solo debe contar la devolución Authorized");
        line.RemainingQuantity.Should().Be(9m);
    }

    /// <summary>
    /// Hallazgo ALTO auditoría de aislamiento (Sales/Purchases cross-branch): la query está marcada
    /// IBranchScopedRequest, pero eso solo exige sucursal activa autorizada — no que la factura
    /// pertenezca a esa sucursal. Debe rechazar con NotFound cuando la factura es de otra sucursal
    /// (nunca revelar existencia cross-branch, ni exponer sus líneas devolvibles).
    /// </summary>
    [Fact]
    public async Task Factura_de_otra_sucursal_retorna_NotFound()
    {
        var (invoice, _) = BuildAuthorizedInvoice(("Producto A", 10m, 5m));
        var returnRepo = new Mock<ISalesReturnRepository>();
        var handler = BuildHandler(invoice, returnRepo, activeBranchId: Guid.NewGuid());

        var result = await handler.Handle(
            new GetReturnableLinesByInvoiceQuery(invoice.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ERP.Application.Common.ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Factura_inexistente_retorna_NotFound()
    {
        var invoiceRepo = new Mock<ISalesInvoiceRepository>();
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalesInvoice?)null);
        var returnRepo = new Mock<ISalesReturnRepository>();
        var tenant = Mock.Of<ERP.Application.Common.ICurrentTenant>(t => t.TenantId == TenantId);
        var branch = Mock.Of<ERP.Application.Common.ICurrentBranch>(b => b.BranchId == BranchId);

        var handler = new GetReturnableLinesByInvoiceHandler(
            invoiceRepo.Object,
            returnRepo.Object,
            tenant,
            branch
        );

        var result = await handler.Handle(
            new GetReturnableLinesByInvoiceQuery(Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ERP.Application.Common.ApiResponseCodes.Common.NotFound);
    }

    // ── Helpers para los tests de filtrado por estado ──────────────────

    private static SalesReturn BuildDraftReturnWithLine(
        Guid invoiceId,
        SalesInvoiceDetail originalLine,
        decimal quantity
    )
    {
        var salesReturn = SalesReturn.CreateDraft(
            TenantId,
            CompanyId,
            invoiceId,
            CustomerId,
            $"DEV-{Guid.NewGuid():N}"[..10],
            "Producto en mal estado",
            UserId
        );
        salesReturn.AddLine(
            SalesReturnDetail.Create(
                salesReturn.Id,
                TenantId,
                originalLine.Id,
                originalLine.Description,
                quantity,
                originalLine.UnitPrice,
                0m,
                originalLine.VatCode,
                originalLine.VatRate,
                originalLine.UomCode
            ),
            UserId
        );
        return salesReturn;
    }

    private static SalesReturn BuildAuthorizedReturnWithLine(
        Guid invoiceId,
        SalesInvoiceDetail originalLine,
        decimal quantity
    )
    {
        var salesReturn = BuildDraftReturnWithLine(invoiceId, originalLine, quantity);
        salesReturn.AddRefundAllocation(
            ERP.Domain.Modules.Sales.Entities.SalesReturnRefundAllocation.Create(
                salesReturn.Id,
                TenantId,
                ERP.Domain.Modules.Sales.Enums.SalesReturnRefundMethod.Cash,
                salesReturn.GrandTotal
            ),
            UserId
        );
        salesReturn.Authorize(UserId);
        return salesReturn;
    }

    private static GetReturnableLinesByInvoiceHandler BuildHandlerWithFakeRepo(
        SalesInvoice invoice,
        ISalesReturnRepository returnRepo
    )
    {
        var invoiceRepo = new Mock<ISalesInvoiceRepository>();
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var tenant = Mock.Of<ERP.Application.Common.ICurrentTenant>(t => t.TenantId == TenantId);
        var branch = Mock.Of<ERP.Application.Common.ICurrentBranch>(b => b.BranchId == BranchId);

        return new GetReturnableLinesByInvoiceHandler(invoiceRepo.Object, returnRepo, tenant, branch);
    }

    /// <summary>
    /// Doble en memoria que replica exactamente el filtro real de
    /// <c>SalesReturnRepository.GetReturnedQuantityByInvoiceDetailAsync</c> (Fase 3): suma
    /// <c>Quantity</c> solo de líneas cuyo <c>SalesReturn</c> padre está <c>Authorized</c>.
    /// </summary>
    private sealed class InMemorySalesReturnRepository : ISalesReturnRepository
    {
        public List<SalesReturn> Returns { get; } = new();

        public Task<decimal> GetReturnedQuantityByInvoiceDetailAsync(
            Guid tenantId,
            Guid invoiceDetailId,
            CancellationToken ct = default
        )
        {
            var sum = Returns
                .Where(r =>
                    r.TenantId == tenantId
                    && r.Status == ERP.Domain.Modules.Sales.Enums.SalesReturnStatus.Authorized
                )
                .SelectMany(r => r.Lines)
                .Where(l => l.OriginalInvoiceDetailId == invoiceDetailId)
                .Sum(l => l.Quantity);
            return Task.FromResult(sum);
        }

        public Task<SalesReturn?> GetByIdAsync(
            Guid tenantId,
            Guid id,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(IReadOnlyList<SalesReturn> Items, int Total)> GetPagedAsync(
            Guid tenantId,
            string? search,
            string? status,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task AcquireReturnLockAsync(
            Guid tenantId,
            Guid salesInvoiceId,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task AddAsync(SalesReturn salesReturn, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
