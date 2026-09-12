using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.BatchDownloadPurchaseReceptionXml;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

/// <summary>
/// PURCHASE-RECEPTION-BULK-SRI-XML-DOWNLOAD-01 — el handler de lote no reimplementa la descarga
/// SRI: reutiliza <c>DownloadPurchaseReceptionXmlHandler</c> vía <see cref="IMediator"/> dentro de
/// un contenedor DI real (mismo mecanismo que <c>AddMediatR</c> en producción, sin registrar los
/// pipeline behaviors de Company/Branch — esos ya se prueban por separado, aquí solo interesa la
/// orquestación del lote). Cada caso construye su propio contenedor con un tenant fijo y mocks
/// singleton, para poder verificar cuántas veces se invoca el SRI real.
/// </summary>
public sealed class BatchDownloadPurchaseReceptionXmlHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseReceptionDocument SampleDocument(
        string accessKey,
        PurchaseReceptionSourceDocType sourceDocType = PurchaseReceptionSourceDocType.Invoice
    ) =>
        PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            sourceDocType,
            "1791352688001",
            "QUALA ECUADOR S A",
            null,
            accessKey,
            "015-027-000161740",
            new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 21, 6, 55, DateTimeKind.Utc),
            15.96m,
            2.4m,
            18.35m,
            UserId
        );

    private sealed record Harness(
        BatchDownloadPurchaseReceptionXmlHandler Handler,
        Mock<IPurchaseReceptionDocumentRepository> Repo,
        Mock<ISriReceptionXmlProvider> Provider,
        Mock<IPurchaseInvoiceRepository> PurchaseRepo,
        Mock<IExpenseDocumentRepository> ExpenseRepo,
        Mock<IPurchaseCreditNoteRepository> CreditNoteRepo,
        ServiceProvider Container
    ) : IDisposable
    {
        public void Dispose() => Container.Dispose();
    }

    private static Harness BuildHarness()
    {
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        var provider = new Mock<ISriReceptionXmlProvider>();
        var purchaseRepo = new Mock<IPurchaseInvoiceRepository>();
        var expenseRepo = new Mock<IExpenseDocumentRepository>();
        var creditNoteRepo = new Mock<IPurchaseCreditNoteRepository>();
        var detailProcessor = new Mock<IPurchaseReceptionDetailProcessor>();
        detailProcessor
            .Setup(p =>
                p.ProcessAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PurchaseReceptionDetailProcessingResult(
                    [],
                    PurchaseReceptionProcessingOutcome.Failed("sin detalle de prueba"),
                    null,
                    null,
                    null
                )
            );

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(repo.Object);
        services.AddSingleton(purchaseRepo.Object);
        services.AddSingleton(expenseRepo.Object);
        services.AddSingleton(creditNoteRepo.Object);
        services.AddSingleton(Mock.Of<IBusinessPartnerRepository>());
        services.AddSingleton(provider.Object);
        services.AddSingleton(detailProcessor.Object);
        services.AddSingleton(tenant.Object);
        services.AddSingleton(company.Object);
        services.AddSingleton(user.Object);
        // Escaneo de ensamblado real (mismo mecanismo que ERP.Application.DependencyInjection),
        // deliberadamente sin registrar CompanyScopeBehavior/BranchScopeBehavior: ambos ya se
        // prueban en su propio archivo y aquí solo interesa la orquestación del lote.
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<BatchDownloadPurchaseReceptionXmlCommand>()
        );

        var container = services.BuildServiceProvider();
        var scopeFactory = container.GetRequiredService<IServiceScopeFactory>();
        var handler = new BatchDownloadPurchaseReceptionXmlHandler(
            scopeFactory,
            NullLogger<BatchDownloadPurchaseReceptionXmlHandler>.Instance
        );

        return new Harness(handler, repo, provider, purchaseRepo, expenseRepo, creditNoteRepo, container);
    }

    [Fact]
    public async Task Handle_downloads_only_documents_without_xml_and_skips_the_rest()
    {
        using var h = BuildHarness();
        var pending = SampleDocument("AK-PENDING-0000000000000000000000000001");
        var already = SampleDocument("AK-ALREADY-0000000000000000000000000002");
        already.AttachSriAuthorization(
            already.AccessKey,
            DateTime.UtcNow,
            "<factura/>",
            DateTime.UtcNow,
            [],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "20",
            processing: PurchaseReceptionProcessingOutcome.Failed("sin detalle de prueba")
        );

        h.Repo.Setup(r => r.GetByIdAsync(TenantId, pending.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        h.Repo.Setup(r => r.GetByIdAsync(TenantId, already.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(already);
        h.Provider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(TenantId, CompanyId, pending.AccessKey, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new SriReceptionXmlQueryResult(true, pending.AccessKey, DateTime.UtcNow, "<factura>ok</factura>", null)
            );

        var result = await h.Handler.Handle(
            new BatchDownloadPurchaseReceptionXmlCommand([pending.Id, already.Id]),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Total.Should().Be(2);
        result.Value.Downloaded.Should().Be(1);
        result.Value.Skipped.Should().Be(1);
        result.Value.Failed.Should().Be(0);
        result.Value.Items.Should().Contain(i => i.DocumentId == pending.Id && i.Status == "Downloaded");
        result.Value.Items.Should().Contain(i => i.DocumentId == already.Id && i.Status == "SkippedAlreadyHasXml");

        // PURCHASE-RECEPTION-BULK-SRI-XML-DOWNLOAD-01 — el resumen por documento trae lo mínimo
        // para refrescar los badges de la fila sin abrir "Ver XML".
        var downloadedItem = result.Value.Items.Single(i => i.DocumentId == pending.Id);
        downloadedItem.DocumentStatus.Should().Be("VERIFIED");
        downloadedItem.HasXml.Should().BeTrue();
        downloadedItem.PurchaseExists.Should().BeFalse();
        downloadedItem.ExpenseExists.Should().BeFalse();
        downloadedItem.CreditNoteExists.Should().BeFalse();

        var skippedItem = result.Value.Items.Single(i => i.DocumentId == already.Id);
        skippedItem.DocumentStatus.Should().Be("VERIFIED");
        skippedItem.HasXml.Should().BeTrue();

        // Nunca se llama al SRI para el documento que ya tenía XML — regla no negociable del feature.
        h.Provider.Verify(
            p => p.GetAuthorizedXmlAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), already.AccessKey, It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
        h.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_continues_the_batch_when_one_document_fails()
    {
        using var h = BuildHarness();
        var ok = SampleDocument("AK-OK-00000000000000000000000000000003");
        var failing = SampleDocument("AK-FAIL-0000000000000000000000000004");

        h.Repo.Setup(r => r.GetByIdAsync(TenantId, ok.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        h.Repo.Setup(r => r.GetByIdAsync(TenantId, failing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(failing);

        h.Provider
            .Setup(p => p.GetAuthorizedXmlAsync(TenantId, CompanyId, ok.AccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriReceptionXmlQueryResult(true, ok.AccessKey, DateTime.UtcNow, "<factura>ok</factura>", null));
        h.Provider
            .Setup(p => p.GetAuthorizedXmlAsync(TenantId, CompanyId, failing.AccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriReceptionXmlQueryResult(false, null, null, null, "ERROR_CONEXION"));

        var result = await h.Handler.Handle(
            new BatchDownloadPurchaseReceptionXmlCommand([ok.Id, failing.Id]),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Downloaded.Should().Be(1);
        result.Value.Failed.Should().Be(1);
        result.Value.Items.Should().Contain(i => i.DocumentId == ok.Id && i.Status == "Downloaded");
        result.Value.Items.Should().Contain(i => i.DocumentId == failing.Id && i.Status == "SriError");
    }

    [Fact]
    public async Task Handle_reports_error_for_a_document_id_that_does_not_exist()
    {
        using var h = BuildHarness();
        var missingId = Guid.NewGuid();
        h.Repo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseReceptionDocument?)null);

        var result = await h.Handler.Handle(
            new BatchDownloadPurchaseReceptionXmlCommand([missingId]),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Failed.Should().Be(1);
        result.Value.Items.Single().Status.Should().Be("Error");
        h.Provider.Verify(
            p => p.GetAuthorizedXmlAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_never_creates_a_purchase_expense_or_credit_note()
    {
        using var h = BuildHarness();
        var pending = SampleDocument("AK-NOSIDE-000000000000000000000000005");
        h.Repo.Setup(r => r.GetByIdAsync(TenantId, pending.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        h.Provider
            .Setup(p => p.GetAuthorizedXmlAsync(TenantId, CompanyId, pending.AccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriReceptionXmlQueryResult(true, pending.AccessKey, DateTime.UtcNow, "<factura>ok</factura>", null));

        await h.Handler.Handle(
            new BatchDownloadPurchaseReceptionXmlCommand([pending.Id]),
            CancellationToken.None
        );

        // El repositorio de compras/gastos solo se consulta (lectura, para el DTO del caso de uso
        // individual y para el resumen de badges del lote) — nunca se invoca un método de
        // creación/persistencia.
        h.PurchaseRepo
            .Invocations.Should()
            .OnlyContain(i => i.Method.Name == nameof(IPurchaseInvoiceRepository.GetByAccessKeyAsync));
        h.ExpenseRepo
            .Invocations.Should()
            .OnlyContain(i =>
                i.Method.Name == nameof(IExpenseDocumentRepository.ExistsByAccessKeyAsync)
                || i.Method.Name == nameof(IExpenseDocumentRepository.GetActiveIdByAccessKeyAsync)
            );
        // Un documento de factura nunca consulta el repositorio de NC — no aplica a su tipo.
        h.CreditNoteRepo.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_populates_credit_note_badges_for_a_credit_note_document()
    {
        using var h = BuildHarness();
        var creditNoteDoc = SampleDocument(
            "AK-NC-000000000000000000000000000007",
            PurchaseReceptionSourceDocType.CreditNote
        );
        var linkedCreditNoteId = Guid.NewGuid();

        h.Repo.Setup(r => r.GetByIdAsync(TenantId, creditNoteDoc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditNoteDoc);
        h.Provider
            .Setup(p => p.GetAuthorizedXmlAsync(
                TenantId, CompanyId, creditNoteDoc.AccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriReceptionXmlQueryResult(
                false, null, null, null, "No autorizado en el SRI."));
        h.CreditNoteRepo
            .Setup(r => r.GetIdByReceptionDocumentIdAsync(
                TenantId, creditNoteDoc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(linkedCreditNoteId);

        var result = await h.Handler.Handle(
            new BatchDownloadPurchaseReceptionXmlCommand([creditNoteDoc.Id]),
            CancellationToken.None
        );

        var item = result.Value!.Items.Single();
        item.Status.Should().Be("SriError");
        item.CreditNoteExists.Should().BeTrue();
        item.CreditNoteId.Should().Be(linkedCreditNoteId);
        item.CancelledCreditNoteId.Should().BeNull();
        // Ya hay una NC activa vinculada — nunca se consulta la Cancelled más reciente.
        h.CreditNoteRepo.Verify(
            r => r.GetLatestCancelledIdByReceptionDocumentIdAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_bounds_concurrent_sri_calls_to_at_most_three()
    {
        using var h = BuildHarness();
        var documents = Enumerable.Range(0, 8)
            .Select(i => SampleDocument($"AK-CONC-{i:D2}-00000000000000000000000006"))
            .ToList();
        foreach (var doc in documents)
            h.Repo.Setup(r => r.GetByIdAsync(TenantId, doc.Id, It.IsAny<CancellationToken>())).ReturnsAsync(doc);

        var concurrent = 0;
        var maxObserved = 0;
        var gate = new object();
        h.Provider
            .Setup(p => p.GetAuthorizedXmlAsync(TenantId, CompanyId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid _, Guid _, string accessKey, CancellationToken ct) =>
            {
                lock (gate)
                {
                    concurrent++;
                    maxObserved = Math.Max(maxObserved, concurrent);
                }
                await Task.Delay(30, ct);
                lock (gate)
                {
                    concurrent--;
                }
                return new SriReceptionXmlQueryResult(true, accessKey, DateTime.UtcNow, "<factura>ok</factura>", null);
            });

        var result = await h.Handler.Handle(
            new BatchDownloadPurchaseReceptionXmlCommand(documents.Select(d => d.Id).ToList()),
            CancellationToken.None
        );

        result.Value!.Downloaded.Should().Be(8);
        maxObserved.Should().BeLessThanOrEqualTo(3);
    }
}
