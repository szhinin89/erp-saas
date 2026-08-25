using ERP.Application.Common;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// Fase 5.4 — ReverseJournalEntryCommandHandler. Deliberadamente no pasa por PostingPipeline: el
/// asiento original ya existe (Posted) y el handler solo carga, valida el período (mismo guard
/// que Post()) y delega en JournalEntry.Reverse.
/// </summary>
public sealed class ReverseJournalEntryCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly Guid DebitAccountId = Guid.NewGuid();
    private static readonly Guid CreditAccountId = Guid.NewGuid();

    private static JournalEntry PostedEntry(Guid accountingPeriodId, int entryNumber = 1)
    {
        var entry = JournalEntry.Create(
            TenantId,
            CompanyId,
            new DateOnly(2026, 7, 25),
            accountingPeriodId,
            2026,
            "Sales",
            "InvoiceIssued",
            Guid.NewGuid(),
            "Asiento original",
            CreatedBy
        );
        entry.AddLine(DebitAccountId, null, 100m, 0m);
        entry.AddLine(CreditAccountId, null, 0m, 100m);
        entry.Post(CreatedBy, entryNumber);
        return entry;
    }

    private static AccountingPeriod OpenPeriod() =>
        AccountingPeriod.Create(
            TenantId,
            CompanyId,
            2026,
            7,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            CreatedBy
        );

    private sealed class Mocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IAccountingPeriodRepository> AccountingPeriods { get; } = new();
        public Mock<IJournalEntrySequenceRepository> JournalEntrySequences { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Mocks()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            User.Setup(u => u.UserId).Returns(CreatedBy);
            JournalEntrySequences
                .Setup(r =>
                    r.ReserveNextNumberAsync(
                        TenantId,
                        CompanyId,
                        2026,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(2);
        }

        public ReverseJournalEntryCommandHandler BuildHandler() =>
            new(
                JournalEntries.Object,
                AccountingPeriods.Object,
                JournalEntrySequences.Object,
                Tenant.Object,
                Company.Object,
                User.Object
            );
    }

    [Fact]
    public async Task Reverso_correcto_persiste_el_nuevo_asiento_y_actualiza_el_original()
    {
        var period = OpenPeriod();
        var original = PostedEntry(period.Id);
        var m = new Mocks();
        m.JournalEntries.Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, original.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(original);
        m.AccountingPeriods.Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(period);

        JournalEntry? captured = null;
        m.JournalEntries.Setup(r =>
                r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>())
            )
            .Callback<JournalEntry, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var handler = m.BuildHandler();
        var result = await handler.Handle(
            new ReverseJournalEntryCommand(original.Id, "Error de digitación"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.EntryNumber.Should().Be(2);
        result.Value.OriginalJournalEntryId.Should().Be(original.Id);

        captured.Should().NotBeNull();
        captured!.Status.Should().Be(JournalEntryStatus.Posted);
        original.Status.Should().Be(JournalEntryStatus.Reversed);
        original.ReverseReason.Should().Be("Error de digitación");

        // ACCOUNTING-REVERSALS-05: el reverso debe invertir Débito/Crédito por línea y conservar
        // la referencia bidireccional al original (ya probado a nivel de dominio en
        // JournalEntryTests.cs — aquí se confirma que el handler expone ese mismo comportamiento).
        captured.Lines.Should().Contain(l => l.AccountId == DebitAccountId && l.Credit == 100m && l.Debit == 0m);
        captured.Lines.Should().Contain(l => l.AccountId == CreditAccountId && l.Debit == 100m && l.Credit == 0m);
        captured.OriginalJournalEntryId.Should().Be(original.Id);
        original.ReverseJournalEntryId.Should().Be(captured.Id);

        m.JournalEntries.Verify(
            r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        m.JournalEntries.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Asiento_de_otra_empresa_retorna_NotFound_y_nunca_se_reversa()
    {
        var period = OpenPeriod();
        var original = PostedEntry(period.Id);
        var m = new Mocks();
        // GetByIdAsync está scoped por (TenantId, CompanyId) — un asiento de otra empresa/tenant
        // nunca aparece bajo la clave (TenantId, CompanyId) de este test, así que el repositorio
        // (fail-closed) devuelve null exactamente igual que "no existe".
        m.JournalEntries
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, original.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);

        var handler = m.BuildHandler();
        var result = await handler.Handle(
            new ReverseJournalEntryCommand(original.Id, "Intento cross-tenant"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        original.Status.Should().Be(JournalEntryStatus.Posted, because: "el asiento real de otra empresa nunca debe verse afectado");
        m.JournalEntries.Verify(
            r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Asiento_inexistente_retorna_NotFound()
    {
        var m = new Mocks();
        m.JournalEntries.Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((JournalEntry?)null);

        var handler = m.BuildHandler();
        var result = await handler.Handle(
            new ReverseJournalEntryCommand(Guid.NewGuid(), "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Periodo_cerrado_rechaza_el_reverso_y_no_persiste()
    {
        var period = OpenPeriod();
        var original = PostedEntry(period.Id);
        period.Close(CreatedBy, new JournalEntryClosureReadiness(false, false, false));

        var m = new Mocks();
        m.JournalEntries.Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, original.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(original);
        m.AccountingPeriods.Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(period);

        var handler = m.BuildHandler();
        var result = await handler.Handle(
            new ReverseJournalEntryCommand(original.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("PERIOD_NOT_OPEN");
        original
            .Status.Should()
            .Be(
                JournalEntryStatus.Posted,
                because: "un período cerrado nunca debe dejar avanzar el reverso"
            );
        m.JournalEntries.Verify(
            r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Periodo_bloqueado_rechaza_el_reverso()
    {
        var period = OpenPeriod();
        var original = PostedEntry(period.Id);
        period.Close(CreatedBy, new JournalEntryClosureReadiness(false, false, false));
        period.Lock(CreatedBy);

        var m = new Mocks();
        m.JournalEntries.Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, original.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(original);
        m.AccountingPeriods.Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(period);

        var handler = m.BuildHandler();
        var result = await handler.Handle(
            new ReverseJournalEntryCommand(original.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("PERIOD_NOT_OPEN");
        m.JournalEntries.Verify(
            r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Asiento_Draft_es_rechazado_por_el_dominio()
    {
        var period = OpenPeriod();
        var draft = JournalEntry.Create(
            TenantId,
            CompanyId,
            new DateOnly(2026, 7, 25),
            period.Id,
            2026,
            "Sales",
            "InvoiceIssued",
            Guid.NewGuid(),
            "Asiento sin publicar",
            CreatedBy
        );

        var m = new Mocks();
        m.JournalEntries.Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, draft.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(draft);
        m.AccountingPeriods.Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(period);

        var handler = m.BuildHandler();
        var result = await handler.Handle(
            new ReverseJournalEntryCommand(draft.Id, "Motivo"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        m.JournalEntries.Verify(
            r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Doble_reverso_es_rechazado()
    {
        var period = OpenPeriod();
        var original = PostedEntry(period.Id);
        original.Reverse(CreatedBy, 2, "Primer reverso");

        var m = new Mocks();
        m.JournalEntries.Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, original.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(original);
        m.AccountingPeriods.Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, period.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(period);

        var handler = m.BuildHandler();
        var result = await handler.Handle(
            new ReverseJournalEntryCommand(original.Id, "Segundo intento"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        m.JournalEntries.Verify(
            r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
