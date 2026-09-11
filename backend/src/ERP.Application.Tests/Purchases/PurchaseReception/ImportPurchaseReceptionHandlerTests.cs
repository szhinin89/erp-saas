using ERP.Application.Common;
using ERP.Application.Common.Models;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.ImportPurchaseReception;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

public sealed class ImportPurchaseReceptionHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseReceptionRecord SampleRecord(
        string accessKey = "0107202601179135268800120150270001617400016174011"
    ) =>
        new(
            2,
            PurchaseReceptionSourceDocType.Invoice,
            "1791352688001",
            "QUALA ECUADOR S A",
            "015-027-000161740",
            accessKey,
            new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 21, 6, 55),
            "0350016432",
            15.96m,
            2.4m,
            18.35m,
            null
        );

    private static (
        ImportPurchaseReceptionHandler handler,
        Mock<IPurchaseReceptionParser> parser,
        Mock<IPurchaseReceptionVerifier> verifier,
        Mock<IPurchaseReceptionDocumentRepository> repo,
        Mock<IPurchaseCreditNoteRepository> creditNoteRepo
    ) BuildHandler()
    {
        var parser = new Mock<IPurchaseReceptionParser>();
        var verifier = new Mock<IPurchaseReceptionVerifier>();
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        var creditNoteRepo = new Mock<IPurchaseCreditNoteRepository>();
        creditNoteRepo
            .Setup(r =>
                r.GetIdByReceptionDocumentIdAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Guid?)null);

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var branch = new Mock<ICurrentBranch>();
        branch.Setup(b => b.BranchId).Returns(BranchId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var handler = new ImportPurchaseReceptionHandler(
            parser.Object,
            verifier.Object,
            repo.Object,
            creditNoteRepo.Object,
            tenant.Object,
            company.Object,
            branch.Object,
            user.Object
        );

        return (handler, parser, verifier, repo, creditNoteRepo);
    }

    [Fact]
    public async Task Handle_persists_a_new_document_and_maps_it_into_the_response_dto()
    {
        var record = SampleRecord();
        var (handler, parser, verifier, repo, _) = BuildHandler();

        parser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseReceptionParseResult([record], [], 1));
        verifier
            .Setup(v =>
                v.VerifyAsync(
                    It.IsAny<IReadOnlyList<PurchaseReceptionRecord>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new PurchaseReceptionVerifiedItem(
                    record,
                    SupplierExists: true,
                    PurchaseExists: false,
                    PurchaseReceptionStatus.Pending
                ),
            ]);
        repo.Setup(r =>
                r.GetByAccessKeyAsync(TenantId, record.AccessKey, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PurchaseReceptionDocument?)null);

        PurchaseReceptionDocument? added = null;
        repo.Setup(r =>
                r.AddAsync(It.IsAny<PurchaseReceptionDocument>(), It.IsAny<CancellationToken>())
            )
            .Callback<PurchaseReceptionDocument, CancellationToken>((d, _) => added = d)
            .Returns(Task.CompletedTask);

        var command = new ImportPurchaseReceptionCommand(
            new MediaUploadContent(new MemoryStream(), "reception.txt", "text/plain", 10)
        );
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalParsed.Should().Be(1);
        result.Value.SkippedUnsupportedCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Status.Should().Be("PENDING");
        result.Value.Items[0].DocumentStatus.Should().Be("IMPORTED");
        result.Value.Items[0].SupplierRuc.Should().Be("1791352688001");
        result.Value.Items[0].Subtotal.Should().Be(15.96m);
        result.Value.Items[0].VatAmount.Should().Be(2.4m);

        added.Should().NotBeNull();
        added!.TenantId.Should().Be(TenantId);
        added.CompanyId.Should().Be(CompanyId);
        added.BranchId.Should().Be(BranchId);
        added.CreatedBy.Should().Be(UserId);
        added.AccessKey.Should().Be(record.AccessKey);
        result.Value.Items[0].DocumentId.Should().Be(added.Id);

        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_reuses_existing_document_when_access_key_was_already_imported()
    {
        var record = SampleRecord();
        var existing = PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.Invoice,
            record.SupplierRuc,
            record.SupplierName,
            supplierId: null,
            record.AccessKey,
            record.InvoiceNumber,
            record.IssueDate,
            record.AuthorizationDate,
            record.Subtotal,
            record.VatAmount,
            record.Total,
            UserId
        );

        var (handler, parser, verifier, repo, _) = BuildHandler();
        parser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseReceptionParseResult([record], [], 0));
        verifier
            .Setup(v =>
                v.VerifyAsync(
                    It.IsAny<IReadOnlyList<PurchaseReceptionRecord>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new PurchaseReceptionVerifiedItem(
                    record,
                    SupplierExists: true,
                    PurchaseExists: true,
                    PurchaseReceptionStatus.Imported
                ),
            ]);
        repo.Setup(r =>
                r.GetByAccessKeyAsync(TenantId, record.AccessKey, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existing);

        var command = new ImportPurchaseReceptionCommand(
            new MediaUploadContent(new MemoryStream(), "reception.txt", "text/plain", 10)
        );
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items[0].DocumentId.Should().Be(existing.Id);

        // No duplica: nunca se agrega un segundo documento ni se guarda cuando ya existía.
        repo.Verify(
            r => r.AddAsync(It.IsAny<PurchaseReceptionDocument>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_marks_credit_note_reception_as_already_processed_when_linked()
    {
        // PURCHASE-CREDIT-NOTE-RECEPTION-IDEMPOTENCY-UI-01 — re-importar el mismo TXT (mismo
        // AccessKey) de una NC/XML cuya recepción ya está vinculada a un PurchaseCreditNote debe
        // marcar CreditNoteExists=true con el Id existente, para que la UI ofrezca "Ver NC" en vez
        // de reabrir el formulario de creación.
        var record = SampleRecord() with { SourceDocType = PurchaseReceptionSourceDocType.CreditNote };
        var existing = PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.CreditNote,
            record.SupplierRuc,
            record.SupplierName,
            supplierId: null,
            record.AccessKey,
            record.InvoiceNumber,
            record.IssueDate,
            record.AuthorizationDate,
            record.Subtotal,
            record.VatAmount,
            record.Total,
            UserId
        );
        var linkedCreditNoteId = Guid.NewGuid();

        var (handler, parser, verifier, repo, creditNoteRepo) = BuildHandler();
        parser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseReceptionParseResult([record], [], 0));
        verifier
            .Setup(v =>
                v.VerifyAsync(
                    It.IsAny<IReadOnlyList<PurchaseReceptionRecord>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new PurchaseReceptionVerifiedItem(
                    record,
                    SupplierExists: true,
                    PurchaseExists: false,
                    PurchaseReceptionStatus.Pending
                ),
            ]);
        repo.Setup(r =>
                r.GetByAccessKeyAsync(TenantId, record.AccessKey, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existing);
        creditNoteRepo
            .Setup(r =>
                r.GetIdByReceptionDocumentIdAsync(
                    TenantId,
                    existing.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(linkedCreditNoteId);

        var command = new ImportPurchaseReceptionCommand(
            new MediaUploadContent(new MemoryStream(), "reception.txt", "text/plain", 10)
        );
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle();
        result.Value.Items[0].CreditNoteExists.Should().BeTrue();
        result.Value.Items[0].CreditNoteId.Should().Be(linkedCreditNoteId);
    }

    [Fact]
    public async Task Handle_leaves_credit_note_flags_false_when_source_doc_type_is_not_credit_note()
    {
        // El lookup de PurchaseCreditNote nunca debe dispararse para Factura/otros tipos — evita
        // una consulta innecesaria y confirma que el default (false/null) no es un accidente.
        var record = SampleRecord();
        var (handler, parser, verifier, repo, creditNoteRepo) = BuildHandler();

        parser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseReceptionParseResult([record], [], 0));
        verifier
            .Setup(v =>
                v.VerifyAsync(
                    It.IsAny<IReadOnlyList<PurchaseReceptionRecord>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new PurchaseReceptionVerifiedItem(
                    record,
                    SupplierExists: true,
                    PurchaseExists: false,
                    PurchaseReceptionStatus.Pending
                ),
            ]);
        repo.Setup(r =>
                r.GetByAccessKeyAsync(TenantId, record.AccessKey, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PurchaseReceptionDocument?)null);
        repo.Setup(r =>
                r.AddAsync(It.IsAny<PurchaseReceptionDocument>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);

        var command = new ImportPurchaseReceptionCommand(
            new MediaUploadContent(new MemoryStream(), "reception.txt", "text/plain", 10)
        );
        var result = await handler.Handle(command, CancellationToken.None);

        result.Value!.Items[0].CreditNoteExists.Should().BeFalse();
        result.Value.Items[0].CreditNoteId.Should().BeNull();
        creditNoteRepo.Verify(
            r =>
                r.GetIdByReceptionDocumentIdAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_deduplicates_repeated_access_key_within_the_same_file()
    {
        var record1 = SampleRecord();
        var record2 = record1 with { SourceLineNumber = 3 }; // misma AccessKey, otra línea del mismo TXT
        var (handler, parser, verifier, repo, _) = BuildHandler();

        parser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseReceptionParseResult([record1, record2], [], 0));
        verifier
            .Setup(v =>
                v.VerifyAsync(
                    It.IsAny<IReadOnlyList<PurchaseReceptionRecord>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new PurchaseReceptionVerifiedItem(
                    record1,
                    SupplierExists: false,
                    PurchaseExists: false,
                    PurchaseReceptionStatus.NewSupplier
                ),
                new PurchaseReceptionVerifiedItem(
                    record2,
                    SupplierExists: false,
                    PurchaseExists: false,
                    PurchaseReceptionStatus.NewSupplier
                ),
            ]);
        repo.Setup(r =>
                r.GetByAccessKeyAsync(TenantId, record1.AccessKey, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PurchaseReceptionDocument?)null);
        repo.Setup(r =>
                r.AddAsync(It.IsAny<PurchaseReceptionDocument>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);

        var command = new ImportPurchaseReceptionCommand(
            new MediaUploadContent(new MemoryStream(), "reception.txt", "text/plain", 10)
        );
        var result = await handler.Handle(command, CancellationToken.None);

        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items[0].DocumentId.Should().Be(result.Value.Items[1].DocumentId);
        repo.Verify(
            r => r.AddAsync(It.IsAny<PurchaseReceptionDocument>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_succeeds_with_empty_items_and_no_save_when_file_has_no_valid_rows()
    {
        var (handler, parser, verifier, repo, _) = BuildHandler();
        parser
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseReceptionParseResult([], [], 0));
        verifier
            .Setup(v =>
                v.VerifyAsync(
                    It.IsAny<IReadOnlyList<PurchaseReceptionRecord>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        var command = new ImportPurchaseReceptionCommand(
            new MediaUploadContent(new MemoryStream(), "reception.txt", "text/plain", 10)
        );
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
