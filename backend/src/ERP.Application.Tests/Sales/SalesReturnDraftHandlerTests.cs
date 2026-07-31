using ERP.Application.Common;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// P0-01 Fase 4 — casos de uso de Draft (Create/Update/Cancel) de <c>SalesReturn</c>. No cubre
/// autorización (fase posterior): ningún test de este archivo invoca
/// <c>SalesReturn.Authorize()</c> a través de un handler, solo lo usa para construir un fixture
/// en estado <c>Authorized</c>/<c>Cancelled</c> y verificar que Draft/Update/Cancel lo rechazan.
/// </summary>
public sealed class SalesReturnDraftHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid CashSessionId = Guid.NewGuid();

    // ── Fixtures de dominio ───────────────────────────────────────────

    private static (SalesInvoice Invoice, List<SalesInvoiceDetail> Lines) BuildAuthorizedInvoice(
        params (string Description, decimal Quantity, decimal UnitPrice)[] lineSpecs
    )
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(
            PaymentTermId,
            "Contado",
            installments: 1,
            daysBetween: 0
        );

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

    private static SalesReturn BuildDraftReturn(Guid invoiceId, string returnNumber = "DEV-000001") =>
        SalesReturn.CreateDraft(
            TenantId,
            CompanyId,
            invoiceId,
            CustomerId,
            returnNumber,
            "Producto en mal estado",
            UserId
        );

    // ── Mocks ──────────────────────────────────────────────────────────

    private static Mock<ISalesInvoiceRepository> MockInvoiceRepo(SalesInvoice invoice)
    {
        var repo = new Mock<ISalesInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        return repo;
    }

    private static Mock<ISalesReturnRepository> MockReturnRepo(
        decimal alreadyReturned = 0m,
        SalesReturn? existing = null
    )
    {
        var repo = new Mock<ISalesReturnRepository>();
        repo.Setup(r =>
                r.GetReturnedQuantityByInvoiceDetailAsync(
                    TenantId,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(alreadyReturned);
        if (existing is not null)
            repo.Setup(r => r.GetByIdAsync(TenantId, existing.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
        return repo;
    }

    private static ICurrentTenant Tenant() => Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);

    private static ICurrentCompany Company() =>
        Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId);

    private static ICurrentUser User() => Mock.Of<ICurrentUser>(u => u.UserId == UserId);

    // ══════════════════════════════ CreateSalesReturnDraft ══════════════════════════════

    [Fact]
    public async Task Create_con_una_linea_crea_el_borrador()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m));
        var invoiceRepo = MockInvoiceRepo(invoice);
        var returnRepo = MockReturnRepo();

        var handler = new CreateSalesReturnDraftHandler(
            returnRepo.Object,
            invoiceRepo.Object,
            Tenant(),
            Company(),
            User()
        );

        var result = await handler.Handle(
            new CreateSalesReturnDraftCommand(
                invoice.Id,
                "Producto en mal estado",
                new List<SalesReturnLineInput> { new(lines[0].Id, 3m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().ContainSingle();
        result.Value.Lines[0].Quantity.Should().Be(3m);
        result.Value.Status.Should().Be(SalesReturnStatus.Draft.ToString());
        returnRepo.Verify(r => r.AddAsync(It.IsAny<SalesReturn>(), It.IsAny<CancellationToken>()), Times.Once);
        returnRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_con_multiples_lineas_crea_el_borrador()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(
            ("Producto A", 10m, 5m),
            ("Producto B", 4m, 20m)
        );
        var invoiceRepo = MockInvoiceRepo(invoice);
        var returnRepo = MockReturnRepo();

        var handler = new CreateSalesReturnDraftHandler(
            returnRepo.Object,
            invoiceRepo.Object,
            Tenant(),
            Company(),
            User()
        );

        var result = await handler.Handle(
            new CreateSalesReturnDraftCommand(
                invoice.Id,
                "Producto en mal estado",
                new List<SalesReturnLineInput> { new(lines[0].Id, 2m), new(lines[1].Id, 1m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_rechaza_factura_inexistente()
    {
        var invoiceRepo = new Mock<ISalesInvoiceRepository>();
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalesInvoice?)null);
        var returnRepo = MockReturnRepo();

        var handler = new CreateSalesReturnDraftHandler(
            returnRepo.Object,
            invoiceRepo.Object,
            Tenant(),
            Company(),
            User()
        );

        var result = await handler.Handle(
            new CreateSalesReturnDraftCommand(
                Guid.NewGuid(),
                "Motivo",
                new List<SalesReturnLineInput> { new(Guid.NewGuid(), 1m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Create_rechaza_factura_en_borrador()
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);
        var invoice = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            "DRAFT-TEST",
            new DateOnly(2026, 7, 25),
            UserId,
            paymentTerm,
            CashSessionId
        );
        var invoiceRepo = MockInvoiceRepo(invoice);
        var returnRepo = MockReturnRepo();

        var handler = new CreateSalesReturnDraftHandler(
            returnRepo.Object,
            invoiceRepo.Object,
            Tenant(),
            Company(),
            User()
        );

        var result = await handler.Handle(
            new CreateSalesReturnDraftCommand(
                invoice.Id,
                "Motivo",
                new List<SalesReturnLineInput> { new(Guid.NewGuid(), 1m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("autorizadas");
    }

    [Fact]
    public async Task Create_rechaza_factura_anulada()
    {
        var (invoice, _) = BuildAuthorizedInvoice(("Producto A", 5m, 10m));
        invoice.Cancel("Anulación de prueba", UserId);
        var invoiceRepo = MockInvoiceRepo(invoice);
        var returnRepo = MockReturnRepo();

        var handler = new CreateSalesReturnDraftHandler(
            returnRepo.Object,
            invoiceRepo.Object,
            Tenant(),
            Company(),
            User()
        );

        var result = await handler.Handle(
            new CreateSalesReturnDraftCommand(
                invoice.Id,
                "Motivo",
                new List<SalesReturnLineInput> { new(Guid.NewGuid(), 1m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("autorizadas");
    }

    [Fact]
    public async Task Create_rechaza_cantidad_mayor_al_remanente()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 5m, 10m));
        var invoiceRepo = MockInvoiceRepo(invoice);
        var returnRepo = MockReturnRepo(alreadyReturned: 3m); // remanente = 5 - 3 = 2

        var handler = new CreateSalesReturnDraftHandler(
            returnRepo.Object,
            invoiceRepo.Object,
            Tenant(),
            Company(),
            User()
        );

        var result = await handler.Handle(
            new CreateSalesReturnDraftCommand(
                invoice.Id,
                "Motivo",
                new List<SalesReturnLineInput> { new(lines[0].Id, 3m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("remanente");
    }

    [Fact]
    public void Validator_rechaza_cantidad_cero_o_negativa()
    {
        var validator = new CreateSalesReturnDraftValidator();
        var cmd = new CreateSalesReturnDraftCommand(
            Guid.NewGuid(),
            "Motivo",
            new List<SalesReturnLineInput> { new(Guid.NewGuid(), 0m) }
        );

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_rechaza_motivo_vacio()
    {
        var validator = new CreateSalesReturnDraftValidator();
        var cmd = new CreateSalesReturnDraftCommand(
            Guid.NewGuid(),
            " ",
            new List<SalesReturnLineInput> { new(Guid.NewGuid(), 1m) }
        );

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    // ══════════════════════════════ UpdateSalesReturnDraft ══════════════════════════════

    [Fact]
    public async Task Update_modifica_un_borrador_correctamente()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m));
        var salesReturn = BuildDraftReturn(invoice.Id);
        salesReturn.AddLine(
            SalesReturnDetail.Create(
                salesReturn.Id,
                TenantId,
                lines[0].Id,
                lines[0].Description,
                2m,
                lines[0].UnitPrice,
                0m,
                lines[0].VatCode,
                lines[0].VatRate,
                lines[0].UomCode
            ),
            UserId
        );

        var invoiceRepo = MockInvoiceRepo(invoice);
        var returnRepo = MockReturnRepo(existing: salesReturn);

        var handler = new UpdateSalesReturnDraftHandler(
            returnRepo.Object,
            invoiceRepo.Object,
            Tenant(),
            User()
        );

        var result = await handler.Handle(
            new UpdateSalesReturnDraftCommand(
                salesReturn.Id,
                new List<SalesReturnLineInput> { new(lines[0].Id, 4m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().ContainSingle();
        result.Value.Lines[0].Quantity.Should().Be(4m);
        returnRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_rechaza_devolucion_autorizada()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m));
        var salesReturn = BuildDraftReturn(invoice.Id);
        salesReturn.AddLine(
            SalesReturnDetail.Create(
                salesReturn.Id,
                TenantId,
                lines[0].Id,
                lines[0].Description,
                2m,
                lines[0].UnitPrice,
                0m,
                lines[0].VatCode,
                lines[0].VatRate,
                lines[0].UomCode
            ),
            UserId
        );
        salesReturn.AddRefundAllocation(
            SalesReturnRefundAllocation.Create(
                salesReturn.Id,
                TenantId,
                SalesReturnRefundMethod.Cash,
                salesReturn.GrandTotal
            ),
            UserId
        );
        salesReturn.Authorize(UserId);

        var invoiceRepo = MockInvoiceRepo(invoice);
        var returnRepo = MockReturnRepo(existing: salesReturn);

        var handler = new UpdateSalesReturnDraftHandler(
            returnRepo.Object,
            invoiceRepo.Object,
            Tenant(),
            User()
        );

        var result = await handler.Handle(
            new UpdateSalesReturnDraftCommand(
                salesReturn.Id,
                new List<SalesReturnLineInput> { new(lines[0].Id, 1m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Update_rechaza_devolucion_cancelada()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m));
        var salesReturn = BuildDraftReturn(invoice.Id);
        salesReturn.Cancel(UserId);

        var invoiceRepo = MockInvoiceRepo(invoice);
        var returnRepo = MockReturnRepo(existing: salesReturn);

        var handler = new UpdateSalesReturnDraftHandler(
            returnRepo.Object,
            invoiceRepo.Object,
            Tenant(),
            User()
        );

        var result = await handler.Handle(
            new UpdateSalesReturnDraftCommand(
                salesReturn.Id,
                new List<SalesReturnLineInput> { new(lines[0].Id, 1m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    // ══════════════════════════════ CancelSalesReturnDraft ══════════════════════════════

    [Fact]
    public async Task Cancel_cancela_un_borrador()
    {
        var salesReturn = BuildDraftReturn(Guid.NewGuid());
        var returnRepo = MockReturnRepo(existing: salesReturn);

        var handler = new CancelSalesReturnDraftHandler(returnRepo.Object, Tenant(), User());

        var result = await handler.Handle(
            new CancelSalesReturnDraftCommand(salesReturn.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(SalesReturnStatus.Cancelled.ToString());
        returnRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_rechaza_devolucion_autorizada()
    {
        var (invoice, lines) = BuildAuthorizedInvoice(("Producto A", 10m, 5m));
        var salesReturn = BuildDraftReturn(invoice.Id);
        salesReturn.AddLine(
            SalesReturnDetail.Create(
                salesReturn.Id,
                TenantId,
                lines[0].Id,
                lines[0].Description,
                2m,
                lines[0].UnitPrice,
                0m,
                lines[0].VatCode,
                lines[0].VatRate,
                lines[0].UomCode
            ),
            UserId
        );
        salesReturn.AddRefundAllocation(
            SalesReturnRefundAllocation.Create(
                salesReturn.Id,
                TenantId,
                SalesReturnRefundMethod.Cash,
                salesReturn.GrandTotal
            ),
            UserId
        );
        salesReturn.Authorize(UserId);

        var returnRepo = MockReturnRepo(existing: salesReturn);
        var handler = new CancelSalesReturnDraftHandler(returnRepo.Object, Tenant(), User());

        var result = await handler.Handle(
            new CancelSalesReturnDraftCommand(salesReturn.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Cancel_rechaza_devolucion_ya_cancelada()
    {
        var salesReturn = BuildDraftReturn(Guid.NewGuid());
        salesReturn.Cancel(UserId);

        var returnRepo = MockReturnRepo(existing: salesReturn);
        var handler = new CancelSalesReturnDraftHandler(returnRepo.Object, Tenant(), User());

        var result = await handler.Handle(
            new CancelSalesReturnDraftCommand(salesReturn.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }
}
