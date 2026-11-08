using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.BulkMatchItems;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.CorrectMatch;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.MatchItem;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.ResolveLines;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.UnmatchItem;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Inventory.ItemMatching;

/// <summary>
/// PURCHASES-RECEPTION-WRITE-PERMISSIONS-01 (H3) — permiso contextual de la conciliación con el
/// servicio REAL <see cref="ReceptionMatchingAccess"/>: sin compra activa del comprobante exige
/// <c>purchases.create</c>; con una compra activa (Draft/Confirmed) exige <c>purchases.update</c>.
/// Ninguno sustituye al otro y, al denegar, ningún handler escribe.
/// </summary>
public sealed class ReceptionMatchingPermissionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string AccessKey = new('5', 49);

    private readonly Mock<IPurchaseInvoiceRepository> _purchases = new();
    private readonly Mock<IPurchaseReceptionDocumentRepository> _documents = new();
    private readonly PurchaseReceptionDocument _document;
    private readonly PurchaseReceptionLine _line;
    private HashSet<string> _granted = [];

    public ReceptionMatchingPermissionTests()
    {
        _document = PurchaseReceptionDocument.Create(
            TenantId, CompanyId, BranchId, PurchaseReceptionSourceDocType.Invoice, "1791111111001",
            "PROVEEDOR S.A.", Guid.NewGuid(), AccessKey, "001-001-000000001", new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 15, 0, 0, DateTimeKind.Utc), 10m, 1.5m, 11.5m, UserId);
        _line = PurchaseReceptionLine.Create(
            _document.Id, TenantId, "Producto", quantity: 1m, unitPrice: 10m, vatCode: "2", taxCode: "2",
            vatPercentage: 15m, taxValue: 1.5m, discountPct: 0m, discount: 0m, lineSubtotal: 10m,
            totalLine: 11.5m, supplierCode: "A1", itemId: Guid.NewGuid(),
            matchStatus: ItemMatchStatus.ManuallyMatched);
        _documents
            .Setup(r => r.GetByLineIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _document);
    }

    private ReceptionMatchingAccess Access()
    {
        var authorizer = new Mock<IRuntimePermissionAuthorizer>();
        authorizer
            .Setup(a => a.IsAuthorizedAsync(It.IsAny<string>(), UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string permission, Guid _, string _, CancellationToken _) => _granted.Contains(permission));
        return new ReceptionMatchingAccess(
            authorizer.Object,
            Mock.Of<ICurrentUser>(u => u.UserId == UserId && u.Role == "Compras"),
            _purchases.Object);
    }

    private void GivenActivePurchase(bool confirmed = false)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            TenantId, CompanyId, BranchId, Guid.NewGuid(), "PROVEEDOR S.A.", "1791111111001", "01",
            "001-001-000000001", new DateOnly(2026, 7, 1), UserId, Guid.NewGuid(), "Contado", 1, 30,
            accessKey: AccessKey, globalWarehouseId: Guid.NewGuid());
        if (confirmed)
        {
            invoice.ReplaceLines(
                [PurchaseInvoiceDetail.Create(invoice.Id, TenantId, "Producto", 1m, 10m, "4", "UNIT", itemId: Guid.NewGuid())],
                UserId);
            invoice.Confirm(UserId);
        }
        _purchases
            .Setup(r => r.GetByAccessKeyAsync(TenantId, AccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
    }

    private void Grant(params string[] permissions) => _granted = [.. permissions];

    // ── Servicio ───────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false, PurchasePermissions.Create, true)]
    [InlineData(false, PurchasePermissions.Update, false)]
    [InlineData(true, PurchasePermissions.Update, true)]
    [InlineData(true, PurchasePermissions.Create, false)]
    [InlineData(true, PurchasePermissions.View, false)]
    public async Task Required_permission_depends_on_an_active_purchase_of_the_document(
        bool activePurchase,
        string granted,
        bool expected
    )
    {
        if (activePurchase)
            GivenActivePurchase();
        Grant(granted);

        (await Access().DecideAsync(_document) == ReceptionMatchingDecision.Allowed).Should().Be(expected);
    }

    [Theory]
    [InlineData(PurchasePermissions.Update, ReceptionMatchingDecision.PurchaseConfirmed)]
    [InlineData(PurchasePermissions.Create, ReceptionMatchingDecision.Forbidden)]
    public async Task A_confirmed_purchase_is_never_modified_from_matching(
        string granted,
        ReceptionMatchingDecision expected
    )
    {
        GivenActivePurchase(confirmed: true);
        Grant(granted);

        (await Access().DecideAsync(_document)).Should().Be(expected);
    }

    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { PurchasePermissions.View }, false)]
    [InlineData(new[] { PurchasePermissions.Create }, true)]
    [InlineData(new[] { PurchasePermissions.Update }, true)]
    public async Task Pre_check_requires_create_or_update(string[] granted, bool expected)
    {
        Grant(granted);

        (await Access().CanMatchInAnyContextAsync()).Should().Be(expected);
    }

    // ── Handlers con el servicio real: 403 y ninguna escritura ─────────────────────────────────

    private UnmatchPurchaseReceptionItemHandler Unmatch(Mock<IItemMatchConfirmationService> confirmation) =>
        new(_documents.Object, confirmation.Object, Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId), Access());

    [Fact]
    public async Task Create_permission_cannot_modify_the_lines_of_an_existing_purchase()
    {
        GivenActivePurchase();
        Grant(PurchasePermissions.View, PurchasePermissions.Create);
        var confirmation = new Mock<IItemMatchConfirmationService>();

        var result = await Unmatch(confirmation).Handle(new(_line.Id), CancellationToken.None);

        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        confirmation.VerifyNoOtherCalls();
        _documents.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_permission_cannot_prepare_a_new_purchase()
    {
        Grant(PurchasePermissions.View, PurchasePermissions.Update);
        var confirmation = new Mock<IItemMatchConfirmationService>();

        var result = await Unmatch(confirmation).Handle(new(_line.Id), CancellationToken.None);

        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        _documents.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, PurchasePermissions.Create)]
    [InlineData(true, PurchasePermissions.Update)]
    public async Task The_matching_permission_of_each_context_is_accepted(bool activePurchase, string permission)
    {
        if (activePurchase)
            GivenActivePurchase();
        Grant(PurchasePermissions.View, permission);
        _document.Lines.Should().BeEmpty("la línea de la prueba no pertenece al documento");
        var confirmation = new Mock<IItemMatchConfirmationService>();

        // Supera la autorización y continúa con su lógica (aquí, la línea no está en el documento).
        var act = () => Unmatch(confirmation).Handle(new(_line.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task View_only_is_rejected_before_reading_the_document_in_every_operation()
    {
        Grant(PurchasePermissions.View);
        var access = Access();
        var tenant = Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);
        var user = Mock.Of<ICurrentUser>(u => u.UserId == UserId);
        var company = Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId);
        var branch = Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId);

        var results = new[]
        {
            (await new MatchItemHandler(_documents.Object, Mock.Of<IItemRepository>(),
                Mock.Of<IItemMatchConfirmationService>(), tenant, user, access)
                .Handle(new MatchItemCommand(_line.Id, Guid.NewGuid(), null), CancellationToken.None)).Code,
            (await Unmatch(new Mock<IItemMatchConfirmationService>()).Handle(new(_line.Id), CancellationToken.None)).Code,
            (await new BulkMatchItemsHandler(_documents.Object, Mock.Of<IItemRepository>(),
                Mock.Of<IItemMatchConfirmationService>(), tenant, user, access)
                .Handle(new BulkMatchItemsCommand([new BulkMatchItemEntry(_line.Id, Guid.NewGuid())]),
                    CancellationToken.None)).Code,
            (await new CorrectReceptionLineMatchHandler(_documents.Object, _purchases.Object,
                Mock.Of<IItemRepository>(), Mock.Of<IItemMatchConfirmationService>(),
                Mock.Of<IPurchaseReceptionAutoMatcher>(), Mock.Of<IUnitOfWork>(), tenant, company, branch,
                user, Mock.Of<IDatabaseExceptionTranslator>(), access)
                .Handle(new CorrectReceptionLineMatchCommand(_line.Id, Guid.NewGuid(), null),
                    CancellationToken.None)).Code,
            (await new ResolvePurchaseReceptionLinesHandler(_documents.Object, Mock.Of<IItemRepository>(),
                Mock.Of<IItemMatchConfirmationService>(), Mock.Of<IPurchaseReceptionAutoMatcher>(),
                [new CreateItemCommandValidator()], Mock.Of<IMediator>(), Mock.Of<IUnitOfWork>(),
                Mock.Of<IRuntimePermissionAuthorizer>(), tenant, company, branch, user,
                Mock.Of<IDatabaseExceptionTranslator>(), access)
                .Handle(new ResolvePurchaseReceptionLinesCommand([], [new(_line.Id, ItemId: Guid.NewGuid())]),
                    CancellationToken.None)).Code,
        };

        results.Should().AllBe(ApiResponseCodes.Common.Forbidden);
        _documents.Verify(r => r.GetByLineIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _documents.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Compra Confirmed: con <c>purchases.update</c> ninguna de las cinco operaciones modifica las
    /// líneas de recepción ni las equivalencias (409, sin escrituras). Corregir conserva su mensaje.
    /// </summary>
    [Fact]
    public async Task Confirmed_purchase_rejects_every_matching_operation_without_writes()
    {
        GivenActivePurchase(confirmed: true);
        Grant(PurchasePermissions.View, PurchasePermissions.Update);
        var access = Access();
        var tenant = Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);
        var user = Mock.Of<ICurrentUser>(u => u.UserId == UserId);
        var company = Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId);
        var branch = Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId);
        var confirmation = new Mock<IItemMatchConfirmationService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var match = await new MatchItemHandler(_documents.Object, Mock.Of<IItemRepository>(),
                confirmation.Object, tenant, user, access)
            .Handle(new MatchItemCommand(_line.Id, Guid.NewGuid(), null), CancellationToken.None);
        var unmatch = await Unmatch(confirmation).Handle(new(_line.Id), CancellationToken.None);
        var bulk = await new BulkMatchItemsHandler(_documents.Object, Mock.Of<IItemRepository>(),
                confirmation.Object, tenant, user, access)
            .Handle(new BulkMatchItemsCommand([new BulkMatchItemEntry(_line.Id, Guid.NewGuid())]),
                CancellationToken.None);
        var resolve = await new ResolvePurchaseReceptionLinesHandler(_documents.Object, Mock.Of<IItemRepository>(),
                confirmation.Object, Mock.Of<IPurchaseReceptionAutoMatcher>(),
                [new CreateItemCommandValidator()], Mock.Of<IMediator>(), unitOfWork.Object,
                Mock.Of<IRuntimePermissionAuthorizer>(), tenant, company, branch, user,
                Mock.Of<IDatabaseExceptionTranslator>(), access)
            .Handle(new ResolvePurchaseReceptionLinesCommand([], [new(_line.Id, ItemId: Guid.NewGuid())]),
                CancellationToken.None);

        foreach (var code in new[] { match.Code, unmatch.Code, bulk.Code, resolve.Code })
            code.Should().Be(ApiResponseCodes.Common.Conflict);
        match.Error.Should().Be(ReceptionMatchingAccess.ConfirmedPurchaseMessage);
        confirmation.VerifyNoOtherCalls();
        unitOfWork.VerifyNoOtherCalls();
        _documents.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Bulk_is_rejected_entirely_before_the_first_write_when_one_document_is_not_allowed()
    {
        GivenActivePurchase();
        Grant(PurchasePermissions.View, PurchasePermissions.Create);
        var confirmation = new Mock<IItemMatchConfirmationService>();

        var result = await new BulkMatchItemsHandler(_documents.Object, Mock.Of<IItemRepository>(),
                confirmation.Object, Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId), Access())
            .Handle(new BulkMatchItemsCommand([new BulkMatchItemEntry(_line.Id, Guid.NewGuid())]),
                CancellationToken.None);

        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        confirmation.VerifyNoOtherCalls();
        _documents.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
