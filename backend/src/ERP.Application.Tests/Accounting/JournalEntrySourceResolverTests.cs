using ERP.Application.Modules.Accounting.Queries;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-SOURCE-TRACEABILITY-04 — resolución de origen documental humano para JournalEntry.
/// Cubre: número de factura de venta, número de factura de compra, origen inexistente (no rompe
/// la consulta, solo queda sin resolver) y aislamiento por tenant (una cuenta/factura de otro
/// tenant nunca se filtra).
/// </summary>
public sealed class JournalEntrySourceResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OtherTenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Fact]
    public async Task SalesJournalSourceResolver_devuelve_numero_de_factura_y_cliente()
    {
        var journalEntryId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var salesRepo = new Mock<ISalesInvoiceRepository>();
        salesRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(invoiceId)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (string, string, string, DateOnly)>
                {
                    [invoiceId] = ("001-001-000000123", "Cliente ACME", "Authorized", new DateOnly(2026, 8, 1)),
                }
            );

        var resolver = new SalesJournalSourceResolver(salesRepo.Object);
        var result = await resolver.ResolveAsync(
            TenantId,
            CompanyId,
            new[] { new JournalEntrySourceRequest(journalEntryId, "Sales", "InvoiceIssued", invoiceId) },
            CancellationToken.None
        );

        result.Should().ContainKey(journalEntryId);
        var info = result[journalEntryId];
        info.SourceDocumentType.Should().Be("Factura de venta");
        info.SourceDocumentNumber.Should().Be("001-001-000000123");
        info.SourcePartyName.Should().Be("Cliente ACME");
        info.SourceStatus.Should().Be("Authorized");
        info.SourceDocumentDate.Should().Be(new DateOnly(2026, 8, 1));
        info.SourceRoute.Should().Be($"/sales?invoiceId={invoiceId}");
    }

    [Fact]
    public async Task PurchaseJournalSourceResolver_devuelve_numero_de_compra_y_proveedor()
    {
        var journalEntryId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var purchaseRepo = new Mock<IPurchaseInvoiceRepository>();
        purchaseRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(invoiceId)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (string, string, string, DateOnly)>
                {
                    [invoiceId] = ("FAC-000456", "Proveedor XYZ", "Confirmed", new DateOnly(2026, 8, 2)),
                }
            );

        var resolver = new PurchaseJournalSourceResolver(
                purchaseRepo.Object,
                Mock.Of<IPurchaseCreditNoteRepository>(),
                Mock.Of<IBusinessPartnerRepository>()
            );
        var result = await resolver.ResolveAsync(
            TenantId,
            CompanyId,
            new[] { new JournalEntrySourceRequest(journalEntryId, "Purchases", "InvoiceReceived", invoiceId) },
            CancellationToken.None
        );

        result.Should().ContainKey(journalEntryId);
        var info = result[journalEntryId];
        info.SourceDocumentType.Should().Be("Factura de compra");
        info.SourceDocumentNumber.Should().Be("FAC-000456");
        info.SourcePartyName.Should().Be("Proveedor XYZ");
        info.SourceStatus.Should().Be("Confirmed");
        info.SourceRoute.Should().Be($"/purchases?invoiceId={invoiceId}");
    }

    // ── ACCOUNTING-CREDIT-NOTES-POSTING-08 ──────────────────────────────────

    [Fact]
    public async Task PurchaseJournalSourceResolver_resuelve_numero_y_proveedor_de_NC_de_compra_autorizada()
    {
        var journalEntryId = Guid.NewGuid();
        var creditNoteId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var creditNoteRepo = new Mock<IPurchaseCreditNoteRepository>();
        creditNoteRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(creditNoteId)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (Guid, string, string, DateOnly)>
                {
                    [creditNoteId] = (supplierId, "NC-001-001-000000005", "Authorized", new DateOnly(2026, 8, 5)),
                }
            );
        var partnerRepo = new Mock<IBusinessPartnerRepository>();
        partnerRepo
            .Setup(r => r.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [supplierId] = "Proveedor ACME" });

        var resolver = new PurchaseJournalSourceResolver(
            Mock.Of<IPurchaseInvoiceRepository>(),
            creditNoteRepo.Object,
            partnerRepo.Object
        );
        var result = await resolver.ResolveAsync(
            TenantId,
            CompanyId,
            new[]
            {
                new JournalEntrySourceRequest(journalEntryId, "Purchases", "PurchaseCreditNoteAuthorized", creditNoteId),
            },
            CancellationToken.None
        );

        result.Should().ContainKey(journalEntryId);
        var info = result[journalEntryId];
        info.SourceDocumentType.Should().Be("Nota de crédito de compra");
        info.SourceDocumentNumber.Should().Be("NC-001-001-000000005");
        info.SourcePartyName.Should().Be("Proveedor ACME");
        info.SourceStatus.Should().Be("Authorized");
        info.SourceDocumentDate.Should().Be(new DateOnly(2026, 8, 5));
        info.SourceRoute.Should().Be($"/purchases/credit-notes/{creditNoteId}");
    }

    [Fact]
    public async Task PurchaseJournalSourceResolver_diferencia_etiqueta_para_NC_cancelada()
    {
        var journalEntryId = Guid.NewGuid();
        var creditNoteId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var creditNoteRepo = new Mock<IPurchaseCreditNoteRepository>();
        creditNoteRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (Guid, string, string, DateOnly)>
                {
                    [creditNoteId] = (supplierId, "NC-001-001-000000005", "Cancelled", new DateOnly(2026, 8, 5)),
                }
            );
        var partnerRepo = new Mock<IBusinessPartnerRepository>();
        partnerRepo
            .Setup(r => r.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [supplierId] = "Proveedor ACME" });

        var resolver = new PurchaseJournalSourceResolver(
            Mock.Of<IPurchaseInvoiceRepository>(),
            creditNoteRepo.Object,
            partnerRepo.Object
        );
        var result = await resolver.ResolveAsync(
            TenantId,
            CompanyId,
            new[]
            {
                new JournalEntrySourceRequest(journalEntryId, "Purchases", "PurchaseCreditNoteCancelled", creditNoteId),
            },
            CancellationToken.None
        );

        result[journalEntryId].SourceDocumentType.Should().Be("Nota de crédito de compra (cancelación)");
    }

    [Fact]
    public async Task PurchaseJournalSourceResolver_NC_inexistente_no_rompe_y_queda_sin_resolver()
    {
        var journalEntryId = Guid.NewGuid();
        var creditNoteId = Guid.NewGuid();
        var creditNoteRepo = new Mock<IPurchaseCreditNoteRepository>();
        creditNoteRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, (Guid, string, string, DateOnly)>());

        var resolver = new PurchaseJournalSourceResolver(
            Mock.Of<IPurchaseInvoiceRepository>(),
            creditNoteRepo.Object,
            Mock.Of<IBusinessPartnerRepository>()
        );
        var result = await resolver.ResolveAsync(
            TenantId,
            CompanyId,
            new[]
            {
                new JournalEntrySourceRequest(journalEntryId, "Purchases", "PurchaseCreditNoteAuthorized", creditNoteId),
            },
            CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SalesJournalSourceResolver_origen_inexistente_no_rompe_y_queda_sin_resolver()
    {
        var journalEntryId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var salesRepo = new Mock<ISalesInvoiceRepository>();
        salesRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, (string, string, string, DateOnly)>()); // factura no encontrada

        var resolver = new SalesJournalSourceResolver(salesRepo.Object);
        var result = await resolver.ResolveAsync(
            TenantId,
            CompanyId,
            new[] { new JournalEntrySourceRequest(journalEntryId, "Sales", "InvoiceIssued", invoiceId) },
            CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SalesJournalSourceResolver_ignora_FactType_no_soportado_sin_consultar_el_repositorio()
    {
        var journalEntryId = Guid.NewGuid();
        var salesRepo = new Mock<ISalesInvoiceRepository>();

        var resolver = new SalesJournalSourceResolver(salesRepo.Object);
        var result = await resolver.ResolveAsync(
            TenantId,
            CompanyId,
            new[]
            {
                new JournalEntrySourceRequest(journalEntryId, "Sales", "SalesReturn", Guid.NewGuid()),
            },
            CancellationToken.None
        );

        result.Should().BeEmpty();
        salesRepo.Verify(
            r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task JournalEntrySourceResolver_despacha_por_SourceModule_a_cada_resolver_de_modulo()
    {
        var salesEntryId = Guid.NewGuid();
        var salesInvoiceId = Guid.NewGuid();
        var purchaseEntryId = Guid.NewGuid();
        var purchaseInvoiceId = Guid.NewGuid();

        var salesRepo = new Mock<ISalesInvoiceRepository>();
        salesRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (string, string, string, DateOnly)>
                {
                    [salesInvoiceId] = ("001-001-1", "Cliente", "Authorized", new DateOnly(2026, 8, 1)),
                }
            );

        var purchaseRepo = new Mock<IPurchaseInvoiceRepository>();
        purchaseRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (string, string, string, DateOnly)>
                {
                    [purchaseInvoiceId] = ("FAC-1", "Proveedor", "Confirmed", new DateOnly(2026, 8, 2)),
                }
            );

        var composite = new JournalEntrySourceResolver(
            new IJournalEntrySourceModuleResolver[]
            {
                new SalesJournalSourceResolver(salesRepo.Object),
                new PurchaseJournalSourceResolver(
                purchaseRepo.Object,
                Mock.Of<IPurchaseCreditNoteRepository>(),
                Mock.Of<IBusinessPartnerRepository>()
            ),
            }
        );

        var result = await composite.ResolveManyAsync(
            TenantId,
            CompanyId,
            new[]
            {
                new JournalEntrySourceRequest(salesEntryId, "Sales", "InvoiceIssued", salesInvoiceId),
                new JournalEntrySourceRequest(purchaseEntryId, "Purchases", "InvoiceReceived", purchaseInvoiceId),
                new JournalEntrySourceRequest(Guid.NewGuid(), "Finance", "CollectionApplied", Guid.NewGuid()),
            },
            CancellationToken.None
        );

        result.Should().HaveCount(2);
        result[salesEntryId].SourceDocumentType.Should().Be("Factura de venta");
        result[purchaseEntryId].SourceDocumentType.Should().Be("Factura de compra");
    }

    [Fact]
    public async Task SalesJournalSourceResolver_aisla_por_tenant_no_filtra_factura_de_otro_tenant()
    {
        var journalEntryId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var salesRepo = new Mock<ISalesInvoiceRepository>();
        // El repositorio real filtra por tenantId — simulamos ese comportamiento: solo responde
        // para OtherTenantId, nunca para TenantId, aunque el Id de factura coincida.
        salesRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    OtherTenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (string, string, string, DateOnly)>
                {
                    [invoiceId] = ("001-001-1", "Cliente de otro tenant", "Authorized", new DateOnly(2026, 8, 1)),
                }
            );
        salesRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, (string, string, string, DateOnly)>());

        var resolver = new SalesJournalSourceResolver(salesRepo.Object);
        var result = await resolver.ResolveAsync(
            TenantId,
            CompanyId,
            new[] { new JournalEntrySourceRequest(journalEntryId, "Sales", "InvoiceIssued", invoiceId) },
            CancellationToken.None
        );

        result.Should().BeEmpty();
        salesRepo.Verify(
            r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}

/// <summary>
/// ACCOUNTING-CASH-POSTING-06 — FinanceJournalSourceResolver: cobro de cliente
/// (Finance/CollectionApplied) y pago a proveedor (Finance/SupplierPaymentApplied). Payment no
/// tiene numeración documental propia — solo resuelve si Reference fue capturado; sin él, se
/// omite el request (nunca se inventa un número), cayendo al fallback técnico en el consumidor.
/// </summary>
public sealed class FinanceJournalSourceResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();
    private static readonly Guid PartnerId = Guid.NewGuid();

    private static Mock<IPaymentRepository> PaymentRepo() => new();

    private static Mock<IBusinessPartnerRepository> PartnerRepo(string? name = "Cliente ACME")
    {
        var repo = new Mock<IBusinessPartnerRepository>();
        repo.Setup(r => r.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                name is null
                    ? new Dictionary<Guid, string>()
                    : new Dictionary<Guid, string> { [PartnerId] = name }
            );
        return repo;
    }

    [Fact]
    public async Task Cobro_con_Reference_resuelve_tipo_numero_tercero_estado_y_fecha()
    {
        var journalEntryId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var paymentRepo = PaymentRepo();
        paymentRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    CompanyId,
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(paymentId)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (Guid, decimal, DateOnly, string?, string)>
                {
                    [paymentId] = (PartnerId, 300m, new DateOnly(2026, 8, 10), "TRANS-000123", "Applied"),
                }
            );

        var resolver = new FinanceJournalSourceResolver(paymentRepo.Object, PartnerRepo().Object);
        var result = await resolver.ResolveAsync(
            TenantId,
            CompanyId,
            new[] { new JournalEntrySourceRequest(journalEntryId, "Finance", "CollectionApplied", paymentId) },
            CancellationToken.None
        );

        result.Should().ContainKey(journalEntryId);
        var info = result[journalEntryId];
        info.SourceDocumentType.Should().Be("Cobro de cliente");
        info.SourceDocumentNumber.Should().Be("TRANS-000123");
        info.SourcePartyName.Should().Be("Cliente ACME");
        info.SourceStatus.Should().Be("Applied");
        info.SourceDocumentDate.Should().Be(new DateOnly(2026, 8, 10));
        info.SourceRoute.Should().BeNull("no existe hoy una vista de detalle de Payment con deep link seguro");
    }

    [Fact]
    public async Task Pago_a_proveedor_con_Reference_resuelve_tipo_Pago_a_proveedor()
    {
        var journalEntryId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var paymentRepo = PaymentRepo();
        paymentRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (Guid, decimal, DateOnly, string?, string)>
                {
                    [paymentId] = (PartnerId, 300m, new DateOnly(2026, 8, 10), "CHEQUE-045", "Applied"),
                }
            );

        var resolver = new FinanceJournalSourceResolver(paymentRepo.Object, PartnerRepo("Proveedor XYZ").Object);
        var result = await resolver.ResolveAsync(
            TenantId,
            CompanyId,
            new[]
            {
                new JournalEntrySourceRequest(journalEntryId, "Finance", "SupplierPaymentApplied", paymentId),
            },
            CancellationToken.None
        );

        result.Should().ContainKey(journalEntryId);
        result[journalEntryId].SourceDocumentType.Should().Be("Pago a proveedor");
        result[journalEntryId].SourceDocumentNumber.Should().Be("CHEQUE-045");
        result[journalEntryId].SourcePartyName.Should().Be("Proveedor XYZ");
    }

    [Fact]
    public async Task Cobro_sin_Reference_no_resuelve_nunca_inventa_un_numero()
    {
        var journalEntryId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var paymentRepo = PaymentRepo();
        paymentRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (Guid, decimal, DateOnly, string?, string)>
                {
                    [paymentId] = (PartnerId, 300m, new DateOnly(2026, 8, 10), null, "Applied"),
                }
            );

        var resolver = new FinanceJournalSourceResolver(paymentRepo.Object, PartnerRepo().Object);
        var result = await resolver.ResolveAsync(
            TenantId,
            CompanyId,
            new[] { new JournalEntrySourceRequest(journalEntryId, "Finance", "CollectionApplied", paymentId) },
            CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Pago_inexistente_no_rompe_la_consulta_y_queda_sin_resolver()
    {
        var journalEntryId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var paymentRepo = PaymentRepo();
        paymentRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, (Guid, decimal, DateOnly, string?, string)>());

        var resolver = new FinanceJournalSourceResolver(paymentRepo.Object, PartnerRepo(null).Object);
        var result = await resolver.ResolveAsync(
            TenantId,
            CompanyId,
            new[] { new JournalEntrySourceRequest(journalEntryId, "Finance", "CollectionApplied", paymentId) },
            CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Aisla_por_empresa_pago_de_otra_company_nunca_se_filtra()
    {
        var journalEntryId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var paymentRepo = PaymentRepo();
        // El repositorio real filtra por (tenantId, companyId) — simulamos ese comportamiento:
        // solo responde para OtherCompanyId, nunca para CompanyId, aunque el Id de pago coincida.
        paymentRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    OtherCompanyId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, (Guid, decimal, DateOnly, string?, string)>
                {
                    [paymentId] = (PartnerId, 300m, new DateOnly(2026, 8, 10), "REF-1", "Applied"),
                }
            );
        paymentRepo
            .Setup(r =>
                r.GetJournalSourceSummariesByIdsAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, (Guid, decimal, DateOnly, string?, string)>());

        var resolver = new FinanceJournalSourceResolver(paymentRepo.Object, PartnerRepo().Object);
        var result = await resolver.ResolveAsync(
            TenantId,
            CompanyId,
            new[] { new JournalEntrySourceRequest(journalEntryId, "Finance", "CollectionApplied", paymentId) },
            CancellationToken.None
        );

        result.Should().BeEmpty();
    }
}
