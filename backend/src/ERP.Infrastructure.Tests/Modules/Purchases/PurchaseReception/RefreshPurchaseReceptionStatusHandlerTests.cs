using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.RefreshPurchaseReceptionStatus;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Infrastructure.Modules.Purchases.PurchaseReception;
using FluentAssertions;
using Moq;

namespace ERP.Infrastructure.Tests.Modules.Purchases.PurchaseReception;

/// <summary>
/// FASE 5 PURCHASE-UX — "Actualizar estados" de Recepción: el handler re-verifica con el
/// PurchaseReceptionVerifier REAL (mismo que la importación del TXT) las recepciones ya guardadas,
/// sin escribir en BD, sin asignar proveedor y sin consultar el SRI.
/// </summary>
public sealed class RefreshPurchaseReceptionStatusHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string SupplierRuc = "0990011223001";

    private sealed class Fixture
    {
        public Mock<IPurchaseReceptionDocumentRepository> Documents { get; } = new();
        public Mock<IBusinessPartnerRepository> Partners { get; } = new();
        public Mock<IBusinessPartnerRoleRepository> Roles { get; } = new();
        public Mock<IPurchaseInvoiceRepository> Purchases { get; } = new();
        public Mock<IExpenseDocumentRepository> Expenses { get; } = new();
        public Mock<IPurchaseCreditNoteRepository> CreditNotes { get; } = new();
        public BusinessPartner Supplier { get; }

        public Fixture()
        {
            Supplier = BusinessPartner.Create(
                TenantId,
                TaxIdentification.SriRuc,
                SupplierRuc,
                2,
                "DINADEC S.A.",
                UserId
            );
            Partners
                .Setup(r => r.GetByIdentificationAsync(TaxIdentification.SriRuc, SupplierRuc, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Supplier);
            Roles
                .Setup(r => r.GetByTypeAsync(Supplier.Id, RoleType.Supplier, It.IsAny<CancellationToken>()))
                .ReturnsAsync(BusinessPartnerRole.Create(TenantId, Supplier.Id, RoleType.Supplier, UserId));
        }

        public RefreshPurchaseReceptionStatusHandler Build()
        {
            var tenant = Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);
            var verifier = new PurchaseReceptionVerifier(
                Partners.Object,
                Roles.Object,
                Purchases.Object,
                Expenses.Object,
                tenant
            );
            return new RefreshPurchaseReceptionStatusHandler(
                Documents.Object,
                verifier,
                CreditNotes.Object,
                tenant
            );
        }
    }

    /// <summary>Recepción ya importada (sin proveedor asignado aún, como queda antes de crearlo).</summary>
    private static PurchaseReceptionDocument Reception(string accessKey, string invoiceNumber) =>
        PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.Invoice,
            SupplierRuc,
            "DINADEC S.A.",
            supplierId: null,
            accessKey,
            invoiceNumber,
            new DateOnly(2026, 10, 12),
            new DateTime(2026, 10, 12, 15, 0, 0, DateTimeKind.Utc),
            300m,
            45m,
            345m,
            UserId
        );

    private static PurchaseInvoice Draft(string accessKey) =>
        PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            Guid.NewGuid(),
            "DINADEC S.A.",
            SupplierRuc,
            "01",
            "034-005-002226390",
            new DateOnly(2026, 10, 12),
            UserId,
            Guid.NewGuid(),
            "Contado",
            1,
            0,
            accessKey: accessKey
        );

    [Fact]
    public async Task Devuelve_los_estados_actuales_de_cada_recepcion()
    {
        var f = new Fixture();
        var withDraft = Reception(new string('1', 49), "034-005-000000001");
        var withDiscarded = Reception(new string('2', 49), "034-005-000000002");
        var draft = Draft(withDraft.AccessKey);
        var discardedId = Guid.NewGuid();
        f.Documents
            .Setup(r => r.GetByIdsAsync(TenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([withDraft, withDiscarded]);
        f.Purchases
            .Setup(r => r.GetByAccessKeyAsync(TenantId, withDraft.AccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        f.Purchases
            .Setup(r => r.GetLatestCancelledByAccessKeyAsync(TenantId, withDiscarded.AccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CancelledPurchaseRef(discardedId, IsDiscardedDraft: true));

        var result = await f.Build().Handle(
            new RefreshPurchaseReceptionStatusQuery([withDraft.Id, withDiscarded.Id]),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var draftRow = result.Value!.Single(i => i.DocumentId == withDraft.Id);
        draftRow.PurchaseState.Should().Be("DRAFT");
        draftRow.PurchaseExists.Should().BeTrue();
        draftRow.PurchaseId.Should().Be(draft.Id);
        draftRow.Status.Should().Be("IMPORTED");
        draftRow.DocumentStatus.Should().Be("IMPORTED");
        draftRow.SupplierExists.Should().BeTrue("el proveedor se creó después de importar");
        draftRow.SupplierId.Should().Be(f.Supplier.Id);
        draftRow.AccessKey.Should().Be(withDraft.AccessKey);
        draftRow.Total.Should().Be(345m);

        var discardedRow = result.Value!.Single(i => i.DocumentId == withDiscarded.Id);
        discardedRow.PurchaseState.Should().Be("DISCARDED");
        discardedRow.PurchaseExists.Should().BeFalse();
        discardedRow.CancelledPurchaseId.Should().Be(discardedId);
        discardedRow.Status.Should().Be("PENDING");
    }

    [Fact]
    public async Task Ids_fuera_de_alcance_se_omiten()
    {
        var f = new Fixture();
        var ownReception = Reception(new string('3', 49), "034-005-000000003");
        var foreignId = Guid.NewGuid();
        // El repositorio (tenant + empresa) solo devuelve las propias: la ajena no vuelve.
        f.Documents
            .Setup(r => r.GetByIdsAsync(TenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ownReception]);

        var result = await f.Build().Handle(
            new RefreshPurchaseReceptionStatusQuery([ownReception.Id, foreignId, ownReception.Id]),
            CancellationToken.None
        );

        result.Value!.Select(i => i.DocumentId).Should().Equal(ownReception.Id);
        f.Documents.Verify(
            r => r.GetByIdsAsync(
                TenantId,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(foreignId)),
                It.IsAny<CancellationToken>()
            ),
            Times.Once,
            "ids distintos, en una sola consulta"
        );
    }

    [Fact]
    public async Task No_escribe_en_BD_ni_asigna_proveedor()
    {
        var f = new Fixture();
        var reception = Reception(new string('4', 49), "034-005-000000004");
        f.Documents
            .Setup(r => r.GetByIdsAsync(TenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([reception]);

        var result = await f.Build().Handle(
            new RefreshPurchaseReceptionStatusQuery([reception.Id]),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Single().SupplierId.Should().Be(f.Supplier.Id, "la fila muestra el proveedor resuelto");
        reception.SupplierId.Should().BeNull("el refresco no asigna proveedor (eso es de la importación)");
        f.Documents.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.Documents.Verify(
            r => r.AddAsync(It.IsAny<PurchaseReceptionDocument>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.Purchases.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void No_depende_del_SRI_ni_de_escrituras()
    {
        // Estructural: solo recepción (lectura), verificador, NC (lectura) y tenant — sin proveedor
        // SRI, sin unidad de trabajo, sin clock ni usuario (no crea ni modifica nada).
        var dependencies = typeof(RefreshPurchaseReceptionStatusHandler)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType)
            .ToList();

        dependencies.Should().BeEquivalentTo(
            [
                typeof(IPurchaseReceptionDocumentRepository),
                typeof(IPurchaseReceptionVerifier),
                typeof(IPurchaseCreditNoteRepository),
                typeof(ICurrentTenant),
            ]
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Valida_un_tope_razonable_de_ids(int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

        new RefreshPurchaseReceptionStatusValidator()
            .Validate(new RefreshPurchaseReceptionStatusQuery(ids))
            .IsValid.Should().BeFalse();
    }
}
