using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Queries;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-LEDGER-VISIBILITY-01 — cobertura mínima de las queries de solo lectura de
/// JournalEntry (listado paginado, detalle con líneas, búsqueda por documento de origen).
/// </summary>
public sealed class GetJournalEntriesUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static JournalEntry PostedEntryWithLines(Guid debitAccountId, Guid creditAccountId, int entryNumber = 1)
    {
        var entry = JournalEntry.Create(
            TenantId,
            CompanyId,
            new DateOnly(2026, 7, 25),
            Guid.NewGuid(),
            2026,
            "Sales",
            "InvoiceIssued",
            Guid.NewGuid(),
            "Asiento de prueba",
            CreatedBy
        );
        entry.AddLine(debitAccountId, "Débito", 150m, 0m);
        entry.AddLine(creditAccountId, "Crédito", 0m, 150m);
        entry.Post(CreatedBy, entryNumber);
        return entry;
    }

    private static Account NewAccount(string code, string name) =>
        Account.Create(
            TenantId,
            CompanyId,
            AccountCode.Create(code),
            name,
            null,
            AccountType.Asset,
            AccountNature.Debit,
            true,
            CreatedBy
        );

    private sealed class Mocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IAccountRepository> Accounts { get; } = new();
        public Mock<IJournalEntrySourceResolver> SourceResolver { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();

        public Mocks()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            // Por defecto, sin origen resuelto — cada test que lo necesite sobrescribe el Setup.
            SourceResolver
                .Setup(r =>
                    r.ResolveManyAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<IReadOnlyList<JournalEntrySourceRequest>>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(new Dictionary<Guid, JournalEntrySourceInfo>());
        }
    }

    [Fact]
    public async Task GetJournalEntries_retorna_totales_calculados_y_paginacion()
    {
        var entry = PostedEntryWithLines(Guid.NewGuid(), Guid.NewGuid());
        var m = new Mocks();
        m.JournalEntries.Setup(r =>
                r.GetPageAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<JournalEntryListFilter>(),
                    1,
                    20,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((new List<JournalEntry> { entry }, 1));

        var handler = new GetJournalEntriesHandler(
            m.JournalEntries.Object,
            m.SourceResolver.Object,
            m.Tenant.Object,
            m.Company.Object
        );
        var result = await handler.Handle(new GetJournalEntriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].TotalDebit.Should().Be(150m);
        result.Value.Items[0].TotalCredit.Should().Be(150m);
        result.Value.Items[0].Status.Should().Be("Posted");
    }

    // ── ACCOUNTING-SOURCE-TRACEABILITY-04 ──────────────────────────────────

    [Fact]
    public async Task GetJournalEntries_incluye_origen_documental_cuando_el_resolver_lo_devuelve()
    {
        var entry = PostedEntryWithLines(Guid.NewGuid(), Guid.NewGuid());
        var m = new Mocks();
        m.JournalEntries.Setup(r =>
                r.GetPageAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<JournalEntryListFilter>(),
                    1,
                    20,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((new List<JournalEntry> { entry }, 1));
        m.SourceResolver
            .Setup(r =>
                r.ResolveManyAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<IReadOnlyList<JournalEntrySourceRequest>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, JournalEntrySourceInfo>
                {
                    [entry.Id] = new(
                        "Factura de venta",
                        "001-001-000000123",
                        entry.EntryDate,
                        "Cliente de prueba",
                        "Authorized",
                        "/sales?invoiceId=" + entry.SourceEventId
                    ),
                }
            );

        var handler = new GetJournalEntriesHandler(
            m.JournalEntries.Object,
            m.SourceResolver.Object,
            m.Tenant.Object,
            m.Company.Object
        );
        var result = await handler.Handle(new GetJournalEntriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Value!.Items[0];
        item.SourceDocumentType.Should().Be("Factura de venta");
        item.SourceDocumentNumber.Should().Be("001-001-000000123");
        item.SourcePartyName.Should().Be("Cliente de prueba");
        item.SourceStatus.Should().Be("Authorized");
        item.SourceRoute.Should().Be("/sales?invoiceId=" + entry.SourceEventId);
    }

    [Fact]
    public async Task GetJournalEntryById_origen_no_resuelto_no_rompe_la_consulta_y_deja_campos_nulos()
    {
        var debitAccount = NewAccount("1.1.01", "Caja");
        var creditAccount = NewAccount("4.1.01", "Ventas");
        var entry = PostedEntryWithLines(debitAccount.Id, creditAccount.Id);

        var m = new Mocks();
        m.JournalEntries
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { debitAccount, creditAccount });
        // SourceResolver mock por defecto ya devuelve diccionario vacío (documento no encontrado
        // o módulo sin resolver dedicado) — la query debe seguir teniendo éxito.

        var handler = new GetJournalEntryByIdHandler(
            m.JournalEntries.Object,
            m.Accounts.Object,
            m.SourceResolver.Object,
            m.Tenant.Object,
            m.Company.Object
        );
        var result = await handler.Handle(new GetJournalEntryByIdQuery(entry.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SourceDocumentType.Should().BeNull();
        result.Value.SourceDocumentNumber.Should().BeNull();
        result.Value.SourceRoute.Should().BeNull();
        // El dato técnico crudo nunca se oculta, con o sin origen resuelto.
        result.Value.SourceModule.Should().Be("Sales");
        result.Value.SourceEventId.Should().Be(entry.SourceEventId);
    }

    [Fact]
    public async Task GetJournalEntryById_resuelve_AccountCode_y_AccountName_de_cada_linea()
    {
        var debitAccount = NewAccount("1.1.01", "Caja");
        var creditAccount = NewAccount("4.1.01", "Ventas");
        var entry = PostedEntryWithLines(debitAccount.Id, creditAccount.Id);

        var m = new Mocks();
        m.JournalEntries
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { debitAccount, creditAccount });

        var handler = new GetJournalEntryByIdHandler(
            m.JournalEntries.Object,
            m.Accounts.Object,
            m.SourceResolver.Object,
            m.Tenant.Object,
            m.Company.Object
        );
        var result = await handler.Handle(new GetJournalEntryByIdQuery(entry.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().HaveCount(2);
        result.Value.Lines[0].AccountCode.Should().Be("1.1.01");
        result.Value.Lines[0].AccountName.Should().Be("Caja");
        result.Value.TotalDebit.Should().Be(150m);
        result.Value.TotalCredit.Should().Be(150m);
        result.Value.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task GetJournalEntryById_asiento_inexistente_retorna_NotFound()
    {
        var m = new Mocks();
        m.JournalEntries
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);

        var handler = new GetJournalEntryByIdHandler(
            m.JournalEntries.Object,
            m.Accounts.Object,
            m.SourceResolver.Object,
            m.Tenant.Object,
            m.Company.Object
        );
        var result = await handler.Handle(new GetJournalEntryByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    // ── ACCOUNTING-REVERSALS-05 ─────────────────────────────────────────────

    [Fact]
    public async Task GetJournalEntryById_de_un_asiento_reversado_incluye_numero_y_fecha_del_reverso()
    {
        var debitAccount = NewAccount("1.1.01", "Caja");
        var creditAccount = NewAccount("4.1.01", "Ventas");
        var original = PostedEntryWithLines(debitAccount.Id, creditAccount.Id, entryNumber: 1);
        var reversal = original.Reverse(CreatedBy, 2, "Factura anulada");

        var m = new Mocks();
        m.JournalEntries
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, original.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(original);
        m.JournalEntries
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, reversal.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversal);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { debitAccount, creditAccount });

        var handler = new GetJournalEntryByIdHandler(
            m.JournalEntries.Object,
            m.Accounts.Object,
            m.SourceResolver.Object,
            m.Tenant.Object,
            m.Company.Object
        );
        var result = await handler.Handle(new GetJournalEntryByIdQuery(original.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Reversed");
        result.Value.ReverseJournalEntryId.Should().Be(reversal.Id);
        result.Value.ReverseJournalEntryNumber.Should().Be(2);
        result.Value.ReverseJournalEntryDate.Should().Be(reversal.EntryDate);
    }

    [Fact]
    public async Task GetJournalEntryById_de_un_asiento_de_reverso_incluye_numero_del_original_y_hereda_su_origen()
    {
        var debitAccount = NewAccount("1.1.01", "Caja");
        var creditAccount = NewAccount("4.1.01", "Ventas");
        var original = PostedEntryWithLines(debitAccount.Id, creditAccount.Id, entryNumber: 1);
        var reversal = original.Reverse(CreatedBy, 2, "Factura anulada");

        var m = new Mocks();
        m.JournalEntries
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, reversal.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversal);
        m.JournalEntries
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, original.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(original);
        m.Accounts
            .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { debitAccount, creditAccount });
        // El asiento de reverso lleva SourceModule="Accounting" (no resuelve por sí mismo) — solo
        // el original ("Sales"/"InvoiceIssued") resuelve, y ese origen debe heredarse.
        m.SourceResolver
            .Setup(r =>
                r.ResolveManyAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<IReadOnlyList<JournalEntrySourceRequest>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, JournalEntrySourceInfo>
                {
                    [original.Id] = new(
                        "Factura de venta",
                        "001-001-000000123",
                        original.EntryDate,
                        "Cliente de prueba",
                        "Authorized",
                        "/sales?invoiceId=" + original.SourceEventId
                    ),
                }
            );

        var handler = new GetJournalEntryByIdHandler(
            m.JournalEntries.Object,
            m.Accounts.Object,
            m.SourceResolver.Object,
            m.Tenant.Object,
            m.Company.Object
        );
        var result = await handler.Handle(new GetJournalEntryByIdQuery(reversal.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.OriginalJournalEntryId.Should().Be(original.Id);
        result.Value.OriginalJournalEntryNumber.Should().Be(1);
        result.Value.OriginalJournalEntryDate.Should().Be(original.EntryDate);
        // Origen heredado del original, aunque el propio SourceModule del reverso sea "Accounting".
        result.Value.SourceDocumentNumber.Should().Be("001-001-000000123");
        result.Value.SourcePartyName.Should().Be("Cliente de prueba");
    }

    [Fact]
    public async Task GetJournalEntriesBySource_delega_en_el_repositorio_con_los_mismos_parametros()
    {
        var entry = PostedEntryWithLines(Guid.NewGuid(), Guid.NewGuid());
        var m = new Mocks();
        m.JournalEntries
            .Setup(r =>
                r.GetBySourceAsync(TenantId, CompanyId, "Sales", entry.SourceEventId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<JournalEntry> { entry });

        var handler = new GetJournalEntriesBySourceHandler(
            m.JournalEntries.Object,
            m.SourceResolver.Object,
            m.Tenant.Object,
            m.Company.Object
        );
        var result = await handler.Handle(
            new GetJournalEntriesBySourceQuery("Sales", entry.SourceEventId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(x => x.Id == entry.Id);
    }
}
