using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// P0-01 Fase 8/10 — wiring de emisión de Nota de Crédito dentro de
/// <c>AuthorizeSalesReturnHandler</c>: mismo patrón exacto que
/// <c>AuthorizeSalesUseCases.cs</c> para Factura — captura de secuencial "04" ANTES del commit,
/// <c>IElectronicDocumentIssuer.RegisterAsync</c> SIEMPRE después del commit, y un fallo del
/// pipeline de emisión nunca revierte la autorización ya comprometida.
/// </summary>
public sealed class AuthorizeSalesReturnCreditNoteWiringTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid CashSessionId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();
    private static readonly Guid EstablishmentId = Guid.NewGuid();

    private static (SalesInvoice Invoice, SalesInvoiceDetail Line) BuildAuthorizedInvoice(
        bool withEmissionPoint
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
            "001-001-000000045",
            new DateOnly(2026, 7, 20),
            UserId,
            paymentTerm,
            CashSessionId,
            emissionPointId: withEmissionPoint ? EmissionPointId : null
        );
        var line = SalesInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto Test",
            10m,
            10m,
            vatCode: "0",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, UserId);
        var payment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            line.TaxInclusiveTotal
        );
        inv.ReplacePayments(new[] { payment }, UserId);
        inv.Authorize(UserId);
        return (inv, inv.Lines[0]);
    }

    private static SalesReturn BuildDraftReturn(SalesInvoice invoice, SalesInvoiceDetail line)
    {
        var salesReturn = SalesReturn.CreateDraft(
            TenantId,
            CompanyId,
            invoice.Id,
            CustomerId,
            $"DEV-{Guid.NewGuid():N}"[..10],
            "Producto en mal estado",
            UserId
        );
        salesReturn.AddLine(
            SalesReturnDetail.Create(
                salesReturn.Id,
                TenantId,
                line.Id,
                line.Description,
                4m,
                line.UnitPrice,
                0m,
                line.VatCode,
                line.VatRate,
                line.UomCode
            ),
            UserId
        );
        return salesReturn;
    }

    private sealed class Mocks
    {
        public Mock<ISalesReturnRepository> ReturnRepo { get; } = new();
        public Mock<ISalesInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IStockRepository> StockRepo { get; } = new();
        public Mock<IDocumentSequenceRepository> SeqRepo { get; } = new();
        public Mock<IEmissionPointRepository> EpRepo { get; } = new();
        public Mock<IEstablishmentRepository> EstRepo { get; } = new();
        public Mock<IElectronicDocumentIssuer> EdocIssuer { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();

        public readonly List<string> CallOrder = new();

        public Mocks()
        {
            StockRepo
                .Setup(s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            Uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
                .Callback(() => CallOrder.Add("Commit"))
                .Returns(Task.CompletedTask);
            Uow.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            EdocIssuer
                .Setup(e =>
                    e.RegisterAsync(
                        It.IsAny<RegisterElectronicDocumentRequest>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback(() => CallOrder.Add("RegisterAsync"))
                .ReturnsAsync(
                    Result<ElectronicDocumentDto>.Success(
                        new ElectronicDocumentDto(
                            Id: Guid.NewGuid(),
                            DocumentType: ElectronicDocumentType.CreditNote.ToString(),
                            SourceModule: "Sales",
                            SourceEntityId: Guid.NewGuid(),
                            CurrentState: "Draft",
                            AccessKey: null,
                            AuthorizationNumber: null,
                            AuthorizationDate: null,
                            RetryCount: 0,
                            LastAttemptUtc: null,
                            CreatedAt: DateTime.UtcNow,
                            UpdatedAt: null
                        )
                    )
                );
        }

        public AuthorizeSalesReturnHandler BuildHandler(
            SalesInvoice invoice,
            SalesReturn salesReturn,
            EmissionPoint? emissionPoint = null,
            Establishment? establishment = null,
            Mock<ISalesReceivableRepository>? receivableRepoOverride = null
        )
        {
            InvoiceRepo
                .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);
            ReturnRepo
                .Setup(r => r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(salesReturn);
            ReturnRepo
                .Setup(r =>
                    r.AcquireReturnLockAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>())
                )
                .Returns(Task.CompletedTask);
            ReturnRepo
                .Setup(r =>
                    r.GetReturnedQuantityByInvoiceDetailAsync(
                        TenantId,
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(0m);

            if (emissionPoint is not null)
                EpRepo
                    .Setup(r =>
                        r.GetByIdAsync(
                            EmissionPointId,
                            TenantId,
                            It.IsAny<CancellationToken>()
                        )
                    )
                    .ReturnsAsync(emissionPoint);
            if (establishment is not null)
                EstRepo
                    .Setup(r =>
                        r.GetByIdAsync(TenantId, EstablishmentId, It.IsAny<CancellationToken>())
                    )
                    .ReturnsAsync(establishment);

            SeqRepo
                .Setup(r =>
                    r.CaptureNextAsync(
                        TenantId,
                        CompanyId,
                        EmissionPointId,
                        "04",
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback(() => CallOrder.Add("CaptureNextAsync"))
                .ReturnsAsync("000000001");

            var cashRepo = new Mock<ICashSessionRepository>();
            var receivableRepo = receivableRepoOverride ?? new Mock<ISalesReceivableRepository>();
            var cashSessionCtx = new Mock<ICurrentCashSession>();
            cashSessionCtx.Setup(c => c.HasOpenSession).Returns(false);
            var refundHandler = new SalesReturnRefundHandler(
                cashRepo.Object,
                receivableRepo.Object,
                cashSessionCtx.Object
            );

            return new AuthorizeSalesReturnHandler(
                ReturnRepo.Object,
                InvoiceRepo.Object,
                StockRepo.Object,
                refundHandler,
                SeqRepo.Object,
                EpRepo.Object,
                EstRepo.Object,
                EdocIssuer.Object,
                Uow.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId),
                Mock.Of<ILogger<AuthorizeSalesReturnHandler>>()
            );
        }
    }

    /// <summary>Asignación de reembolso vía crédito de CxC — aísla el wiring de ElectronicDocuments
    /// bajo prueba sin depender de una sesión de caja abierta (Fase 6, módulo distinto).</summary>
    private static void AddReceivableCreditAllocation(
        SalesReturn salesReturn,
        Mock<ISalesReceivableRepository> receivableRepo,
        Guid invoiceId,
        decimal amount
    )
    {
        salesReturn.AddRefundAllocation(
            SalesReturnRefundAllocation.Create(
                salesReturn.Id,
                TenantId,
                ERP.Domain.Modules.Sales.Enums.SalesReturnRefundMethod.ReceivableCredit,
                amount
            ),
            UserId
        );
        receivableRepo
            .Setup(r => r.GetByInvoiceIdAsync(TenantId, invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Domain.Modules.Sales.Entities.SalesReceivable.Create(
                    TenantId,
                    CompanyId,
                    invoiceId,
                    CustomerId,
                    amount,
                    UserId
                )
            );
    }

    [Fact]
    public async Task Con_punto_de_emision_resoluble_captura_secuencial_04_y_emite_la_nota_de_credito()
    {
        var (invoice, line) = BuildAuthorizedInvoice(withEmissionPoint: true);
        var salesReturn = BuildDraftReturn(invoice, line);

        var establishment = Establishment.Create(
            TenantId,
            branchId: BranchId,
            CompanyId,
            code: "001",
            name: "Matriz",
            address: "Av. Principal 123",
            phone: null,
            isMain: true,
            createdBy: UserId
        );
        typeof(Establishment)
            .GetProperty(nameof(Establishment.Id))!
            .SetValue(establishment, EstablishmentId);
        var emissionPoint = EmissionPoint.Create(
            TenantId,
            CompanyId,
            EstablishmentId,
            code: "001",
            name: "PE-001",
            emissionType: ERP.Domain.Modules.Company.Enums.EmissionType.Electronic,
            isDefault: true,
            createdBy: UserId
        );

        var m = new Mocks();
        var receivableRepo = new Mock<ISalesReceivableRepository>();
        var handler = m.BuildHandler(
            invoice,
            salesReturn,
            emissionPoint,
            establishment,
            receivableRepo
        );
        AddReceivableCreditAllocation(salesReturn, receivableRepo, invoice.Id, salesReturn.GrandTotal);

        var result = await handler.Handle(
            new AuthorizeSalesReturnCommand(
                salesReturn.Id,
                new List<AuthorizeSalesReturnRefundAllocationInput>()
            ),
            CancellationToken.None
        );

        // La captura de secuencial ocurre antes del commit; RegisterAsync solo después.
        m.SeqRepo.Verify(
            r =>
                r.CaptureNextAsync(
                    TenantId,
                    CompanyId,
                    EmissionPointId,
                    "04",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        salesReturn.CreditNoteDocumentNumber.Should().Be("001-001-000000001");

        m.EdocIssuer.Verify(
            e =>
                e.RegisterAsync(
                    It.Is<RegisterElectronicDocumentRequest>(r =>
                        r.TenantId == TenantId
                        && r.CompanyId == CompanyId
                        && r.DocumentType == ElectronicDocumentType.CreditNote
                        && r.SourceModule == "Sales"
                        && r.SourceEntityId == salesReturn.Id
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        var commitIndex = m.CallOrder.IndexOf("Commit");
        var registerIndex = m.CallOrder.IndexOf("RegisterAsync");
        commitIndex.Should().BeGreaterThan(-1);
        registerIndex.Should().BeGreaterThan(-1);
        registerIndex
            .Should()
            .BeGreaterThan(
                commitIndex,
                because: "la emisión electrónica nunca debe ocurrir antes del commit de la transacción de la devolución"
            );

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_punto_de_emision_en_la_factura_original_no_emite_nota_de_credito()
    {
        var (invoice, line) = BuildAuthorizedInvoice(withEmissionPoint: false);
        var salesReturn = BuildDraftReturn(invoice, line);

        var m = new Mocks();
        var receivableRepo = new Mock<ISalesReceivableRepository>();
        var handler = m.BuildHandler(
            invoice,
            salesReturn,
            receivableRepoOverride: receivableRepo
        );
        AddReceivableCreditAllocation(salesReturn, receivableRepo, invoice.Id, salesReturn.GrandTotal);

        var result = await handler.Handle(
            new AuthorizeSalesReturnCommand(
                salesReturn.Id,
                new List<AuthorizeSalesReturnRefundAllocationInput>()
            ),
            CancellationToken.None
        );

        // La factura original no tiene punto de emisión — la devolución se autoriza igual
        // (mismo criterio que una factura sin EmissionPointId), pero nunca se intenta emitir la
        // Nota de Crédito electrónica.
        result.IsSuccess.Should().BeTrue();
        m.SeqRepo.Verify(
            r =>
                r.CaptureNextAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        m.EdocIssuer.Verify(
            e =>
                e.RegisterAsync(
                    It.IsAny<RegisterElectronicDocumentRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        salesReturn.CreditNoteDocumentNumber.Should().BeNull();
    }

    [Fact]
    public async Task Fallo_del_pipeline_de_emision_no_revierte_la_autorizacion_ya_comprometida()
    {
        var (invoice, line) = BuildAuthorizedInvoice(withEmissionPoint: true);
        var salesReturn = BuildDraftReturn(invoice, line);
        salesReturn.AddRefundAllocation(
            SalesReturnRefundAllocation.Create(
                salesReturn.Id,
                TenantId,
                ERP.Domain.Modules.Sales.Enums.SalesReturnRefundMethod.ReceivableCredit,
                40m
            ),
            UserId
        );

        var establishment = Establishment.Create(
            TenantId,
            branchId: BranchId,
            CompanyId,
            code: "001",
            name: "Matriz",
            address: "Av. Principal 123",
            phone: null,
            isMain: true,
            createdBy: UserId
        );
        typeof(Establishment)
            .GetProperty(nameof(Establishment.Id))!
            .SetValue(establishment, EstablishmentId);
        var emissionPoint = EmissionPoint.Create(
            TenantId,
            CompanyId,
            EstablishmentId,
            code: "001",
            name: "PE-001",
            emissionType: ERP.Domain.Modules.Company.Enums.EmissionType.Electronic,
            isDefault: true,
            createdBy: UserId
        );

        var m = new Mocks();
        // Sobreescribe el resultado por defecto (éxito) para simular fallo del pipeline.
        m.EdocIssuer
            .Setup(e =>
                e.RegisterAsync(
                    It.IsAny<RegisterElectronicDocumentRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<ElectronicDocumentDto>.ValidationFailure("XSD sin versión activa.")
            );

        var receivableRepo = new Mock<ISalesReceivableRepository>();
        receivableRepo
            .Setup(r =>
                r.GetByInvoiceIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Domain.Modules.Sales.Entities.SalesReceivable.Create(
                    TenantId,
                    CompanyId,
                    invoice.Id,
                    CustomerId,
                    100m,
                    UserId
                )
            );

        var handler = m.BuildHandler(
            invoice,
            salesReturn,
            emissionPoint,
            establishment,
            receivableRepo
        );

        var result = await handler.Handle(
            new AuthorizeSalesReturnCommand(
                salesReturn.Id,
                new List<AuthorizeSalesReturnRefundAllocationInput>()
            ),
            CancellationToken.None
        );

        // La devolución ya se autorizó y comprometió (commit) antes de que el pipeline de
        // emisión fallara — el fallo del documento electrónico es informativo, no revierte nada.
        result.IsSuccess.Should().BeTrue();
        m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
