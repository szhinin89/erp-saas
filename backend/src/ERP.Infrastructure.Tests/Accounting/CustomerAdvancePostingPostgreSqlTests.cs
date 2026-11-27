using ERP.Application;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Accounting.UseCases.PostingRules;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Steps;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F2 (PostgreSQL 16 real, DI oficial) — los eventos del agregado registran
/// una obligación por movimiento en la transacción operativa, el intento publica el asiento con las
/// cuentas de la empresa, las reversas espejan el asiento histórico y la completitud detecta los
/// movimientos sin contabilizar. Cada prueba aprovisiona su propia empresa con el bootstrap oficial.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CustomerAdvancePostingPostgreSqlTests
    : IClassFixture<CustomerAdvancePostingPostgreSqlTests.Fixture>
{
    private const string Module = "Finance";
    private const string Received = "CustomerAdvanceReceived";
    private const string Applied = "CustomerAdvanceApplied";
    private const string Refunded = "CustomerAdvanceRefunded";
    private const string Reason = "Configuración contable corregida";

    private readonly Fixture _f;

    public CustomerAdvancePostingPostgreSqlTests(Fixture fixture) => _f = fixture;

    // ── Asientos ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task La_recepcion_publica_Debe_destino_real_y_Haber_anticipos(bool cash)
    {
        var c = await _f.CompanyAsync();

        var advance = await c.RegisterAsync(100m, cash);

        var movement = advance.Movements[0].Id;
        var posting = await c.PostingAsync(Received, movement);
        posting.Status.Should().Be(AccountingPostingStatus.Posted, posting.ErrorMessage);
        posting.EntryDate.Should().Be(advance.AdvanceDate);
        var destination = cash ? c.CashAccount : c.BankAccount;
        destination.Should().NotBe(c.DefaultCashAccount, "la prueba debe demostrar la sustitución");
        var entry = await c.SingleEntryAsync(movement);
        entry.EntryDate.Should().Be(advance.AdvanceDate);
        AssertLines(entry, (destination, 100m, 0m), (c.Advances, 0m, 100m));
        // Ni Caja ni cobros contabilizan el mismo dinero.
        (await c.PostingsAsync())
            .Should()
            .ContainSingle();
        (await c.EntriesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Una_aplicacion_de_dos_lineas_publica_dos_asientos_independientes()
    {
        var c = await _f.CompanyAsync();
        var advance = await c.RegisterAsync(100m);

        var (movements, _) = await c.ApplyAsync(advance.Id, 30m, 20m);

        foreach (var (movement, amount) in movements.Zip(new[] { 30m, 20m }))
        {
            var posting = await c.PostingAsync(Applied, movement);
            posting.Status.Should().Be(AccountingPostingStatus.Posted, posting.ErrorMessage);
            AssertLines(
                await c.SingleEntryAsync(movement),
                (c.Advances, amount, 0m),
                (c.Receivables, 0m, amount)
            );
        }
        (await c.PostingsAsync())
            .Select(p => p.FactType)
            .Should()
            .BeEquivalentTo([Received, Applied, Applied]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task La_devolucion_publica_Haber_en_su_destino_aunque_difiera_del_ingreso(
        bool cash
    )
    {
        var c = await _f.CompanyAsync();
        var advance = await c.RegisterAsync(100m, cash: !cash);

        var (movement, _) = await c.RefundAsync(advance.Id, 40m, cash);

        var posting = await c.PostingAsync(Refunded, movement);
        posting.Status.Should().Be(AccountingPostingStatus.Posted, posting.ErrorMessage);
        AssertLines(
            await c.SingleEntryAsync(movement),
            (c.Advances, 40m, 0m),
            (cash ? c.CashAccount : c.BankAccount, 0m, 40m)
        );
        (await c.PostingsAsync())
            .Select(p => p.FactType)
            .Should()
            .BeEquivalentTo([Received, Refunded]);
    }

    // ── Idempotencia y atomicidad ────────────────────────────────────────

    [Fact]
    public async Task Repetir_el_hecho_y_el_intento_no_duplica_obligacion_ni_asiento()
    {
        var c = await _f.CompanyAsync();
        var advance = await c.RegisterAsync(100m);
        var movement = advance.Movements[0].Id;
        var replay = (CustomerAdvanceReceivedEvent)advance.Raised[0];

        // Reentrega secuencial y concurrente del mismo evento.
        Task Redeliver() =>
            c.CommitAsync(sp =>
                ActivatorUtilities
                    .CreateInstance<CustomerAdvanceReceivedPostingTranslator>(sp)
                    .Handle(replay, default)
            );
        await Redeliver();
        await Task.WhenAll(Task.Run(Redeliver), Task.Run(Redeliver));
        var posting = await c.PostingAsync(Received, movement);
        await c.ReprocessAsync(posting.Id);

        (await c.PostingsAsync()).Should().ContainSingle();
        (await c.EntriesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Una_operacion_rechazada_por_clave_repetida_no_deja_obligaciones()
    {
        var c = await _f.CompanyAsync();
        var key = Key();
        await c.RegisterAsync(100m, key: key);

        var act = () => c.RegisterAsync(50m, key: key);

        await act.Should().ThrowAsync<DbUpdateException>();
        (await c.PostingsAsync()).Should().ContainSingle();
        (await c.EntriesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Una_fecha_en_periodo_cerrado_rechaza_toda_la_operacion()
    {
        var c = await _f.CompanyAsync();
        var closed = new DateOnly(2020, 6, 15);
        await c.AddClosedPeriodAsync(closed);

        var act = () => c.RegisterAsync(100m, date: closed);

        (await act.Should().ThrowAsync<DomainRuleViolationException>()).WithMessage(
            $"*{AccountingPostingRecorder.PeriodClosedCode}*"
        );
        (await c.AdvanceCountAsync()).Should().Be(0);
        (await c.PostingsAsync()).Should().BeEmpty();
    }

    // ── Configuración incompleta: Failed reprocesable ────────────────────

    [Fact]
    public async Task Sin_regla_queda_Failed_y_tras_configurarla_el_reproceso_publica_una_sola_vez()
    {
        var c = await _f.CompanyAsync();
        await c.DeleteAdvanceRulesAsync(); // empresa ya aprovisionada antes de CA-F2
        var advance = await c.RegisterAsync(100m);
        var movement = advance.Movements[0].Id;

        var failed = await c.PostingAsync(Received, movement);
        failed.Status.Should().Be(AccountingPostingStatus.Failed);
        failed.ErrorCode.Should().Be("RULE_NOT_FOUND");
        (await c.EntriesAsync()).Should().BeEmpty();

        // Handler oficial de «Configurar» en la pantalla de reglas (sin el pipeline HTTP de acceso).
        await c.RunAsync(async sp =>
        {
            var created = await ActivatorUtilities
                .CreateInstance<CreatePostingRuleHandler>(sp)
                .Handle(
                    new CreatePostingRuleCommand(
                        Module,
                        Received,
                        [
                            new(
                                c.DefaultCashAccount,
                                AccountNature.Debit,
                                PostingAmountKind.GrandTotal
                            ),
                            new(c.Advances, AccountNature.Credit, PostingAmountKind.GrandTotal),
                        ]
                    ),
                    default
                );
            created.IsSuccess.Should().BeTrue(created.Error);
        });
        await c.ReprocessAsync(failed.Id);
        await c.ReprocessAsync(failed.Id);

        (await c.PostingAsync(Received, movement))
            .Status.Should()
            .Be(AccountingPostingStatus.Posted);
        AssertLines(
            await c.SingleEntryAsync(movement),
            (c.BankAccount, 100m, 0m),
            (c.Advances, 0m, 100m)
        );
    }

    [Fact]
    public async Task Destino_sin_cuenta_queda_Failed_y_se_reprocesa_al_configurarlo()
    {
        var c = await _f.CompanyAsync();
        await c.SetCashRegisterAccountAsync(null);
        var advance = await c.RegisterAsync(100m, cash: true);
        var movement = advance.Movements[0].Id;

        var failed = await c.PostingAsync(Received, movement);
        failed.Status.Should().Be(AccountingPostingStatus.Failed);
        failed
            .ErrorCode.Should()
            .Be(AccountingPostingFactBuilder.DestinationAccountNotConfiguredCode);
        (await c.EntriesAsync()).Should().BeEmpty();

        await c.SetCashRegisterAccountAsync(c.CashAccount);
        await c.ReprocessAsync(failed.Id);

        (await c.PostingAsync(Received, movement))
            .Status.Should()
            .Be(AccountingPostingStatus.Posted);
        AssertLines(
            await c.SingleEntryAsync(movement),
            (c.CashAccount, 100m, 0m),
            (c.Advances, 0m, 100m)
        );
    }

    [Fact]
    public async Task Cuenta_de_la_regla_inactiva_queda_Failed_sin_asiento()
    {
        var c = await _f.CompanyAsync();
        await c.DeactivateAccountAsync(c.Advances);

        var advance = await c.RegisterAsync(100m);

        var failed = await c.PostingAsync(Received, advance.Movements[0].Id);
        failed.Status.Should().Be(AccountingPostingStatus.Failed);
        failed.ErrorCode.Should().NotBeNullOrWhiteSpace();
        (await c.EntriesAsync()).Should().BeEmpty();
    }

    // ── Reversas ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Applied)]
    [InlineData(Refunded)]
    [InlineData(Received)]
    public async Task Cada_reversa_espeja_el_asiento_historico_aunque_cambie_la_configuracion(
        string fact
    )
    {
        var c = await _f.CompanyAsync();
        var advance = await c.RegisterAsync(100m, cash: true);
        Guid original;
        Func<Task> reverse;
        if (fact == Applied)
        {
            var (movements, key) = await c.ApplyAsync(advance.Id, 25m);
            original = movements[0];
            reverse = () =>
                c.OperateAsync(
                    advance.Id,
                    (a, _) =>
                    {
                        a.ReverseApplication(key, Undo(), c.BranchId, UserId, Key());
                        return Task.CompletedTask;
                    }
                );
        }
        else if (fact == Refunded)
        {
            var (movement, transaction) = await c.RefundAsync(advance.Id, 25m, cash: true);
            original = movement;
            reverse = () => c.ReverseRefundAsync(advance.Id, transaction);
        }
        else
        {
            original = advance.Movements[0].Id;
            reverse = () => c.ReverseIncomeAsync(advance.Id);
        }
        var posted = await c.SingleEntryAsync(original);

        // Cambio posterior de reglas y cuentas: el espejo no debe leerlos.
        await c.DeleteAdvanceRulesAsync();
        await c.SetCashRegisterAccountAsync(c.DefaultCashAccount);
        await reverse();

        var mirror = await c.MirrorOfAsync(posted.Id);
        mirror.Should().NotBeNull();
        mirror!.EntryDate.Should().Be(await c.TodayAsync());
        mirror
            .Lines.Select(l => (l.AccountId, l.Debit, l.Credit))
            .Should()
            .BeEquivalentTo(
                posted.Lines.Select(l => (l.AccountId, l.Credit, l.Debit)),
                "espejo exacto del asiento publicado"
            );
        (await c.PostingAsync(fact, original)).Status.Should().Be(AccountingPostingStatus.Posted);
        var reversalPosting = (await c.PostingsAsync()).Single(p => p.FactType == "Reversal");
        reversalPosting.SourceEventId.Should().Be(posted.Id);

        // Repetir la solicitud de reverso no crea un segundo espejo.
        await c.CommitAsync(sp =>
            sp.GetRequiredService<IAccountingReversalRecorder>()
                .RecordSourceAsync(
                    c.TenantId,
                    c.CompanyId,
                    Module,
                    fact,
                    original,
                    "Reintento",
                    DateTime.UtcNow,
                    default
                )
        );
        (await c.EntriesAsync()).Count(e => e.OriginalJournalEntryId == posted.Id).Should().Be(1);
        (await c.PostingsAsync()).Count(p => p.FactType == "Reversal").Should().Be(1);
    }

    [Theory]
    [InlineData(Received)]
    [InlineData(Applied)]
    [InlineData(Refunded)]
    [InlineData("AdministrativeCancellation")]
    public async Task Revertir_un_movimiento_sin_asiento_cancela_su_obligacion_sin_espejo(
        string scenario
    )
    {
        var c = await _f.CompanyAsync();
        await c.DeleteAdvanceRulesAsync();
        var advance = await c.RegisterAsync(100m);
        var original = advance.Movements[0].Id;
        var fact = Received;
        if (scenario == Applied)
        {
            var (movements, key) = await c.ApplyAsync(advance.Id, 25m);
            (original, fact) = (movements[0], Applied);
            await c.OperateAsync(
                advance.Id,
                (a, _) =>
                {
                    a.ReverseApplication(key, Undo(), c.BranchId, UserId, Key());
                    return Task.CompletedTask;
                }
            );
        }
        else if (scenario == Refunded)
        {
            var (movement, transaction) = await c.RefundAsync(advance.Id, 25m, cash: false);
            (original, fact) = (movement, Refunded);
            await c.ReverseRefundAsync(advance.Id, transaction);
        }
        else if (scenario == Received)
        {
            await c.ReverseIncomeAsync(advance.Id);
        }
        else
        {
            await c.CancelAsync(advance.Id);
        }

        (await c.PostingAsync(fact, original)).Status.Should().Be(AccountingPostingStatus.Canceled);
        (await c.EntriesAsync()).Should().BeEmpty();
        (await c.PostingsAsync()).Should().NotContain(p => p.FactType == "Reversal");
    }

    [Fact]
    public async Task La_anulacion_administrativa_con_asiento_publicado_se_rechaza_sin_efectos()
    {
        var c = await _f.CompanyAsync();
        var advance = await c.RegisterAsync(100m);

        var act = () => c.CancelAsync(advance.Id);

        (await act.Should().ThrowAsync<DomainRuleViolationException>()).WithMessage(
            CustomerAdvanceCancelledPostingTranslator.PostedEntryMessage
        );
        var (status, movementCount) = await c.AdvanceStateAsync(advance.Id);
        status.Should().Be(CustomerAdvanceStatus.Confirmed);
        movementCount.Should().Be(1);
        (await c.PostingAsync(Received, advance.Movements[0].Id))
            .Status.Should()
            .Be(AccountingPostingStatus.Posted);
        (await c.EntriesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Una_reversa_con_el_periodo_de_hoy_cerrado_se_rechaza_sin_efectos()
    {
        var c = await _f.CompanyAsync();
        var advance = await c.RegisterAsync(100m);
        await c.CloseCurrentPeriodAsync();

        var act = () => c.ReverseIncomeAsync(advance.Id);

        await act.Should().ThrowAsync<DomainRuleViolationException>();
        var (status, movementCount) = await c.AdvanceStateAsync(advance.Id);
        status.Should().Be(CustomerAdvanceStatus.Confirmed);
        movementCount.Should().Be(1);
        (await c.EntriesAsync()).Should().ContainSingle();
    }

    // ── Completitud y aislamiento ────────────────────────────────────────

    [Fact]
    public async Task La_completitud_cuenta_cada_movimiento_contabilizable_sin_obligacion_ni_asiento()
    {
        // Sin Accounting al operar: ningún movimiento nace con obligación.
        var c = await _f.CompanyAsync(AccountingOperationalMode.Disabled);
        var other = await _f.CompanyAsync();
        var today = await c.TodayAsync();
        (await c.RelevantAsync(today)).Should().BeFalse();

        var advance = await c.RegisterAsync(100m);
        var (_, key) = await c.ApplyAsync(advance.Id, 10m, 20m);
        await c.OperateAsync(
            advance.Id,
            (a, _) =>
            {
                a.ReverseApplication(key, Undo(), c.BranchId, UserId, Key());
                return Task.CompletedTask;
            }
        );
        var (_, transaction) = await c.RefundAsync(advance.Id, 5m, cash: false);
        await c.ReverseRefundAsync(advance.Id, transaction);
        await c.ReverseIncomeAsync(advance.Id);

        (await c.PostingsAsync()).Should().BeEmpty();
        // Recepción + 2 aplicaciones + devolución; las reversas no son orígenes.
        (await c.MissingAsync(today, today))
            .Should()
            .Be(4);
        (await c.MissingAsync(today.AddDays(-30), today.AddDays(-1))).Should().Be(0);
        (await c.RelevantAsync(today)).Should().BeTrue();
        (await c.RelevantAsync(today.AddDays(1))).Should().BeFalse();
        (await other.MissingAsync(today, today)).Should().Be(0);
        (await other.RelevantAsync(today)).Should().BeFalse();
        (await c.MissingAsync(today, today, Guid.NewGuid()))
            .Should()
            .Be(0, "otro tenant no ve los orígenes");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Una_obligacion_en_cualquier_estado_cubre_el_origen_aun_revertido(bool failed)
    {
        var c = await _f.CompanyAsync();
        if (failed)
            await c.DeleteAdvanceRulesAsync();
        var today = await c.TodayAsync();
        var advance = await c.RegisterAsync(100m);
        await c.ApplyAsync(advance.Id, 10m);
        var (_, transaction) = await c.RefundAsync(advance.Id, 5m, cash: false);

        (await c.MissingAsync(today, today)).Should().Be(0);
        await c.ReverseRefundAsync(advance.Id, transaction);
        (await c.MissingAsync(today, today)).Should().Be(0);
    }

    [Fact]
    public async Task Cada_empresa_contabiliza_con_sus_cuentas_y_no_usa_destinos_ni_obligaciones_ajenas()
    {
        var own = await _f.CompanyAsync();
        var foreign = await _f.CompanyAsync();

        var advance = await own.RegisterAsync(100m, bankAccountId: foreign.BankAccountId);

        var failed = await own.PostingAsync(Received, advance.Movements[0].Id);
        failed.CompanyId.Should().Be(own.CompanyId);
        failed.Status.Should().Be(AccountingPostingStatus.Failed);
        failed.ErrorCode.Should().Be(AccountingPostingFactBuilder.DestinationNotFoundCode);
        (await foreign.PostingsAsync()).Should().BeEmpty();
        await foreign.RunAsync(async sp =>
        {
            var result = await sp.GetRequiredService<AccountingPostingExecutor>()
                .ExecuteAsync(failed.Id, AccountingPostingAttemptTrigger.Manual, Reason, default);
            result.IsSuccess.Should().BeFalse();
        });

        var valid = await foreign.RegisterAsync(70m);
        var entry = await foreign.SingleEntryAsync(valid.Movements[0].Id);
        entry.CompanyId.Should().Be(foreign.CompanyId);
        AssertLines(entry, (foreign.BankAccount, 70m, 0m), (foreign.Advances, 0m, 70m));
        foreign.Advances.Should().NotBe(own.Advances);
        (await own.EntriesAsync()).Should().BeEmpty();
    }

    // ── Infraestructura ──────────────────────────────────────────────────

    private static readonly Guid UserId = Guid.NewGuid();

    private static ClientRequestKey Key(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), new string('A', ClientRequestKey.PayloadHashLength));

    private static CustomerAdvanceCompensation Undo()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
        return new("Motivo", "REF-1", date, date);
    }

    private static void AssertLines(
        JournalEntry entry,
        params (Guid Account, decimal Debit, decimal Credit)[] expected
    )
    {
        entry.Status.Should().Be(JournalEntryStatus.Posted);
        entry.Lines.Select(l => (l.AccountId, l.Debit, l.Credit)).Should().BeEquivalentTo(expected);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
    }

    /// <summary>Anticipo registrado junto con los eventos que emitió.</summary>
    public sealed record Registered(CustomerAdvance Advance, IReadOnlyList<object> Raised)
    {
        public Guid Id => Advance.Id;
        public DateOnly AdvanceDate => Advance.AdvanceDate;
        public IReadOnlyList<CustomerAdvanceMovement> Movements => Advance.Movements;
    }

    /// <summary>Una empresa aprovisionada con el bootstrap oficial y sus destinos propios.</summary>
    public sealed class CompanyScope
    {
        private readonly Fixture _f;

        public CompanyScope(Fixture fixture) => _f = fixture;

        public Guid TenantId => _f.TenantId;
        public Guid CompanyId { get; init; }
        public Guid BranchId { get; init; }
        public Guid CashRegisterId { get; init; }
        public Guid CashSessionId { get; init; }
        public Guid BankAccountId { get; init; }
        public Guid[] ReceivableIds { get; init; } = [];
        public Guid DefaultCashAccount { get; init; }
        public Guid CashAccount { get; init; }
        public Guid BankAccount { get; init; }
        public Guid Advances { get; init; }
        public Guid Receivables { get; init; }

        public Task RunAsync(Func<IServiceProvider, Task> action) => _f.RunAsync(CompanyId, action);

        /// <summary>Transacción operativa y, tras el commit, el intento inicial (orden de producción).</summary>
        public Task CommitAsync(Func<IServiceProvider, Task> action) =>
            RunAsync(async sp =>
            {
                var unit = sp.GetRequiredService<IUnitOfWork>();
                await unit.BeginTransactionAsync(default);
                try
                {
                    await action(sp);
                    await unit.SaveChangesAsync(default);
                    await unit.CommitAsync(default);
                }
                catch
                {
                    await unit.RollbackAsync(default);
                    throw;
                }
                await sp.GetRequiredService<IAccountingPostingDispatcher>()
                    .DispatchPendingAsync(default);
            });

        public Task<DateOnly> TodayAsync() =>
            _f.ReadAsync(
                CompanyId,
                sp =>
                    sp.GetRequiredService<ICompanyClock>().TodayAsync(CompanyId, TenantId, default)
            );

        public async Task<Registered> RegisterAsync(
            decimal amount,
            bool cash = false,
            ClientRequestKey? key = null,
            DateOnly? date = null,
            Guid? bankAccountId = null
        )
        {
            var advanceDate = date ?? await TodayAsync();
            Registered? registered = null;
            await CommitAsync(async sp =>
            {
                var advance = CustomerAdvance.Register(
                    TenantId,
                    CompanyId,
                    BranchId,
                    _f.CustomerId,
                    "USD",
                    amount,
                    advanceDate,
                    cash ? _f.CashMethodId : _f.TransferMethodId,
                    cash ? null : bankAccountId ?? BankAccountId,
                    cash ? CashRegisterId : null,
                    UserId,
                    key ?? Key(),
                    "TRX-1"
                );
                registered = new Registered(advance, [.. advance.DomainEvents.Cast<object>()]);
                if (cash)
                {
                    var received = advance.Movements[0];
                    var (session, movement) = await MoveCashAsync(
                        sp,
                        CashMovementType.ManualIncome,
                        amount,
                        received.Id
                    );
                    advance.LinkCashMovement(received.Id, session, movement);
                }
                await sp.GetRequiredService<ICustomerAdvanceRepository>().AddAsync(advance);
            });
            return registered!;
        }

        public Task OperateAsync(
            Guid advanceId,
            Func<CustomerAdvance, IServiceProvider, Task> action
        ) =>
            CommitAsync(async sp =>
            {
                var advance = await sp.GetRequiredService<ICustomerAdvanceRepository>()
                    .GetByIdForUpdateAsync(TenantId, advanceId);
                await action(advance!, sp);
            });

        public async Task<(Guid[] Movements, Guid ClientRequestId)> ApplyAsync(
            Guid advanceId,
            params decimal[] amounts
        )
        {
            var today = await TodayAsync();
            var key = Key();
            Guid[] movements = [];
            await OperateAsync(
                advanceId,
                async (a, sp) =>
                {
                    var db = sp.GetRequiredService<ErpDbContext>();
                    var lines = new List<CustomerAdvanceApplicationLine>();
                    for (var i = 0; i < amounts.Length; i++)
                    {
                        var id = ReceivableIds[i];
                        var receivable = await db
                            .SalesReceivables.AsNoTracking()
                            .SingleAsync(x => x.Id == id);
                        lines.Add(new(receivable, null, amounts[i]));
                    }
                    movements = a.Apply(lines, today, BranchId, UserId, key)
                        .Select(m => m.Id)
                        .ToArray();
                }
            );
            return (movements, key.Id);
        }

        public async Task<(Guid MovementId, Guid TransactionId)> RefundAsync(
            Guid advanceId,
            decimal amount,
            bool cash
        )
        {
            var ids = (Guid.Empty, Guid.Empty);
            await OperateAsync(
                advanceId,
                async (a, sp) =>
                {
                    var movement = a.RegisterRefund(amount, Undo(), BranchId, UserId, Key());
                    var transaction = CustomerAdvanceRefundTransaction.CreateRefund(
                        a,
                        movement,
                        cash ? _f.CashMethodId : _f.TransferMethodId,
                        cash ? null : BankAccountId,
                        cash ? CashRegisterId : null
                    );
                    if (cash)
                    {
                        var (session, cashMovement) = await MoveCashAsync(
                            sp,
                            CashMovementType.ManualExpense,
                            amount,
                            transaction.Id
                        );
                        transaction.LinkCashMovement(session, cashMovement);
                    }
                    await sp.GetRequiredService<ICustomerAdvanceRefundTransactionRepository>()
                        .AddAsync(transaction);
                    ids = (movement.Id, transaction.Id);
                }
            );
            return ids;
        }

        public Task ReverseRefundAsync(Guid advanceId, Guid transactionId) =>
            OperateAsync(
                advanceId,
                async (a, sp) =>
                {
                    var refunds =
                        sp.GetRequiredService<ICustomerAdvanceRefundTransactionRepository>();
                    var original = (await refunds.GetByIdAsync(TenantId, transactionId))!;
                    var movement = a.ReverseRefund(
                        original.CustomerAdvanceMovementId,
                        Undo(),
                        BranchId,
                        UserId,
                        Key()
                    );
                    var reversal = CustomerAdvanceRefundTransaction.CreateReversal(
                        original,
                        movement
                    );
                    if (reversal.CashRegisterId is not null)
                    {
                        var (session, cashMovement) = await MoveCashAsync(
                            sp,
                            CashMovementType.ManualIncome,
                            reversal.Amount,
                            original.Id
                        );
                        reversal.LinkCashMovement(session, cashMovement);
                    }
                    await refunds.AddAsync(reversal);
                }
            );

        public Task ReverseIncomeAsync(Guid advanceId) =>
            OperateAsync(
                advanceId,
                async (a, sp) =>
                {
                    var movement = a.ReverseIncome(Undo(), BranchId, UserId, Key());
                    if (a.CashRegisterId is not null)
                    {
                        var (session, cashMovement) = await MoveCashAsync(
                            sp,
                            CashMovementType.ManualExpense,
                            movement.Amount,
                            a.Movements[0].Id
                        );
                        a.LinkCashMovement(movement.Id, session, cashMovement);
                    }
                }
            );

        public Task CancelAsync(Guid advanceId) =>
            OperateAsync(
                advanceId,
                (a, _) =>
                {
                    a.CancelAdministratively(true, Undo(), BranchId, UserId, Key());
                    return Task.CompletedTask;
                }
            );

        private async Task<(Guid SessionId, Guid MovementId)> MoveCashAsync(
            IServiceProvider sp,
            CashMovementType type,
            decimal amount,
            Guid referenceId
        )
        {
            var db = sp.GetRequiredService<ErpDbContext>();
            var session = await db
                .CashSessions.Include(x => x.Movements)
                .SingleAsync(x => x.Id == CashSessionId);
            var movement = session.RecordMovement(
                type,
                amount,
                "Anticipo de cliente",
                UserId,
                referenceId: referenceId
            );
            return (session.Id, movement.Id);
        }

        public Task ReprocessAsync(Guid postingId) =>
            RunAsync(async sp =>
            {
                await sp.GetRequiredService<AccountingPostingExecutor>()
                    .ExecuteAsync(
                        postingId,
                        AccountingPostingAttemptTrigger.Manual,
                        Reason,
                        default
                    );
            });

        public Task<List<AccountingPosting>> PostingsAsync() =>
            QueryAsync(db =>
                db.AccountingPostings.AsNoTracking()
                    .Where(p => p.CompanyId == CompanyId)
                    .ToListAsync()
            );

        public async Task<AccountingPosting> PostingAsync(string fact, Guid sourceId) =>
            (await PostingsAsync()).Single(p =>
                p.SourceModule == Module && p.FactType == fact && p.SourceEventId == sourceId
            );

        public Task<List<JournalEntry>> EntriesAsync() =>
            QueryAsync(db =>
                db.JournalEntries.AsNoTracking()
                    .Include(j => j.Lines)
                    .Where(j => j.CompanyId == CompanyId)
                    .ToListAsync()
            );

        public async Task<JournalEntry> SingleEntryAsync(Guid sourceId) =>
            (await EntriesAsync()).Single(j => j.SourceEventId == sourceId);

        public async Task<JournalEntry?> MirrorOfAsync(Guid originalEntryId) =>
            (await EntriesAsync()).SingleOrDefault(j =>
                j.OriginalJournalEntryId == originalEntryId
            );

        public Task<int> AdvanceCountAsync() =>
            QueryAsync(db => db.CustomerAdvances.CountAsync(a => a.CompanyId == CompanyId));

        public Task<(CustomerAdvanceStatus Status, int Movements)> AdvanceStateAsync(
            Guid advanceId
        ) =>
            QueryAsync(async db =>
            {
                var advance = await db
                    .CustomerAdvances.AsNoTracking()
                    .Include(a => a.Movements)
                    .SingleAsync(a => a.Id == advanceId);
                return (advance.Status, advance.Movements.Count);
            });

        public Task<bool> RelevantAsync(DateOnly date) =>
            _f.ReadAsync(
                CompanyId,
                sp =>
                    sp.GetRequiredService<IAccountingRelevantFactsReader>()
                        .ExistsOnOrAfterAsync(TenantId, CompanyId, date, default)
            );

        public Task<int> MissingAsync(DateOnly from, DateOnly to, Guid? tenantId = null) =>
            _f.ReadAsync(
                CompanyId,
                sp =>
                    sp.GetRequiredService<IAccountingRelevantFactsReader>()
                        .CountWithoutAccountingAsync(
                            tenantId ?? TenantId,
                            CompanyId,
                            from,
                            to,
                            default
                        )
            );

        public Task DeleteAdvanceRulesAsync() =>
            MutateAsync(async db =>
            {
                var rules = await db
                    .PostingRules.Include(r => r.Lines)
                    .Where(r =>
                        r.CompanyId == CompanyId && r.FactType.StartsWith("CustomerAdvance")
                    )
                    .ToListAsync();
                db.PostingRules.RemoveRange(rules);
            });

        public Task SetCashRegisterAccountAsync(Guid? accountId) =>
            MutateAsync(async db =>
            {
                var register = await db.CashRegisters.SingleAsync(r => r.Id == CashRegisterId);
                db.Entry(register).Property(r => r.AccountingAccountId).CurrentValue = accountId;
            });

        public Task DeactivateAccountAsync(Guid accountId) =>
            MutateAsync(async db =>
            {
                var account = await db.Accounts.SingleAsync(a => a.Id == accountId);
                db.Entry(account).Property(a => a.IsActive).CurrentValue = false;
            });

        public Task AddClosedPeriodAsync(DateOnly date) =>
            MutateAsync(db =>
            {
                var period = AccountingPeriod.Create(
                    TenantId,
                    CompanyId,
                    date.Year,
                    1,
                    new DateOnly(date.Year, 1, 1),
                    new DateOnly(date.Year, 12, 31),
                    UserId
                );
                period.Close(UserId, JournalEntryClosureReadiness.Ready);
                db.AccountingPeriods.Add(period);
                return Task.CompletedTask;
            });

        public async Task CloseCurrentPeriodAsync()
        {
            var today = await TodayAsync();
            await MutateAsync(async db =>
            {
                var period = await db.AccountingPeriods.SingleAsync(p =>
                    p.CompanyId == CompanyId && p.StartDate <= today && p.EndDate >= today
                );
                period.Close(UserId, JournalEntryClosureReadiness.Ready);
            });
        }

        private Task MutateAsync(Func<ErpDbContext, Task> action) =>
            RunAsync(async sp =>
            {
                var db = sp.GetRequiredService<ErpDbContext>();
                await action(db);
                await db.SaveChangesAsync();
            });

        private Task<T> QueryAsync<T>(Func<ErpDbContext, Task<T>> query) =>
            _f.ReadAsync(CompanyId, sp => query(sp.GetRequiredService<ErpDbContext>()));
    }

    /// <summary>Un PostgreSQL desechable por clase, migrado desde cero y con el DI oficial.</summary>
    public sealed class Fixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("erp_customer_advance_posting_test")
            .WithUsername("erp")
            .WithPassword("erp_test_secret")
            .Build();

        private ServiceProvider _provider = null!;
        private int _companies;
        private Guid _bankId;

        public Guid TenantId { get; private set; }
        public Guid CustomerId { get; private set; }
        public Guid CashMethodId { get; private set; }
        public Guid TransferMethodId { get; private set; }

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                        ["InstallData:Enabled"] = "false",
                        ["Snowflake:WorkerId"] = "24",
                    }
                )
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging(logging => logging.ClearProviders());
            services.AddDistributedMemoryCache();
            services.AddSingleton<IHostApplicationLifetime, TestLifetime>();
            services.AddInfrastructure(config);
            services.AddApplication();
            services.AddSingleton(Mock.Of<ICurrentUser>(u => u.UserId == UserId));
            services.AddScoped<IAuditContext>(sp => new FixedAuditContext(
                () => sp.GetRequiredService<ICurrentTenant>().TenantId,
                () => sp.GetRequiredService<ICurrentCompany>().CompanyId,
                UserId
            ));
            _provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true }
            );

            await RunAsync(
                Guid.Empty,
                async sp =>
                {
                    var db = sp.GetRequiredService<ErpDbContext>();
                    await db.Database.MigrateAsync();
                    var tenant = Tenant.Create("Tenant A", $"a-{Guid.NewGuid():N}"[..16], UserId);
                    db.Tenants.Add(tenant);
                    await db.SaveChangesAsync();
                    TenantId = tenant.Id;
                }
            );
            await RunAsync(
                Guid.Empty,
                async sp =>
                {
                    var db = sp.GetRequiredService<ErpDbContext>();
                    var customer = BusinessPartner.Create(
                        TenantId,
                        "05",
                        "1710034065",
                        1,
                        "Cliente",
                        UserId
                    );
                    var cash = PaymentMethod.Create(
                        TenantId,
                        "EFECTIVO",
                        "Efectivo",
                        false,
                        false,
                        1,
                        UserId,
                        affectsPhysicalCash: true
                    );
                    var transfer = PaymentMethod.Create(
                        TenantId,
                        "TRANSFERENCIA",
                        "Transferencia",
                        true,
                        false,
                        2,
                        UserId
                    );
                    var bank = Bank.Create(
                        TenantId,
                        "PICHINCHA",
                        "Banco Pichincha",
                        "Pichincha",
                        UserId
                    );
                    db.BusinessPartners.Add(customer);
                    db.Set<PaymentMethod>().AddRange(cash, transfer);
                    db.Set<Bank>().Add(bank);
                    await db.SaveChangesAsync();
                    (CustomerId, CashMethodId, TransferMethodId, _bankId) = (
                        customer.Id,
                        cash.Id,
                        transfer.Id,
                        bank.Id
                    );
                }
            );
        }

        public async Task DisposeAsync()
        {
            await _provider.DisposeAsync();
            await _postgres.DisposeAsync();
        }

        public async Task RunAsync(Guid companyId, Func<IServiceProvider, Task> action) =>
            await ReadAsync(
                companyId,
                async sp =>
                {
                    await action(sp);
                    return true;
                }
            );

        public async Task<T> ReadAsync<T>(Guid companyId, Func<IServiceProvider, Task<T>> action)
        {
            var previous = (JobTenantContext.Current, JobCompanyContext.Current);
            try
            {
                JobTenantContext.Current = TenantId;
                JobCompanyContext.Current = companyId;
                await using var scope = _provider.CreateAsyncScope();
                return await action(scope.ServiceProvider);
            }
            finally
            {
                (JobTenantContext.Current, JobCompanyContext.Current) = previous;
            }
        }

        public async Task<CompanyScope> CompanyAsync(
            AccountingOperationalMode mode = AccountingOperationalMode.Enabled
        )
        {
            var n = Interlocked.Increment(ref _companies);
            var companyId = Guid.Empty;
            await RunAsync(
                Guid.Empty,
                async sp =>
                {
                    var db = sp.GetRequiredService<ErpDbContext>();
                    var company = Company.CreateManaged(
                        TenantId,
                        $"17900{n:D5}001",
                        $"Empresa {n}",
                        createdBy: UserId
                    );
                    db.Companies.Add(company);
                    db.AccountingOperationalPolicies.Add(
                        AccountingOperationalPolicy.Create(
                            TenantId,
                            company.Id,
                            mode,
                            new DateOnly(1900, 1, 1),
                            "Test",
                            UserId
                        )
                    );
                    await db.SaveChangesAsync();
                    companyId = company.Id;
                }
            );

            return await ReadAsync(
                companyId,
                async sp =>
                {
                    var db = sp.GetRequiredService<ErpDbContext>();
                    await new AccountingBootstrapStep(
                        db,
                        sp.GetRequiredService<ICompanyClock>(),
                        NullLogger<AccountingBootstrapStep>.Instance
                    ).ExecuteAsync(new CompanyBootstrapContext(TenantId, companyId, UserId));
                    var accounts = (
                        await db.Accounts.Where(a => a.CompanyId == companyId).ToListAsync()
                    ).ToDictionary(a => a.Code.Value, a => a.Id);

                    var branch = Branch.Create(
                        TenantId,
                        "Matriz",
                        "Av. 1",
                        "001",
                        description: null,
                        reference: null,
                        postalCode: null,
                        phone: null,
                        secondaryPhone: null,
                        email: null,
                        website: null,
                        managerName: null,
                        managerPosition: null,
                        managerEmail: null,
                        managerPhone: null,
                        countryId: null,
                        provinceId: null,
                        cantonId: null,
                        parishId: null,
                        latitude: null,
                        longitude: null,
                        openingDate: null,
                        internalNotes: null,
                        isMainBranch: true,
                        createdBy: UserId,
                        companyId: companyId
                    );
                    db.Branches.Add(branch);
                    var bankAccount = CompanyBankAccount.Create(
                        TenantId,
                        companyId,
                        _bankId,
                        BankAccountType.Checking,
                        $"22001{n:D5}",
                        "Cuenta corriente",
                        accounts["1.1.02.001"],
                        UserId
                    );
                    db.Set<CompanyBankAccount>().Add(bankAccount);
                    var establishment = Establishment.Create(
                        TenantId,
                        branch.Id,
                        companyId,
                        "001",
                        "Matriz",
                        "Av. 1",
                        null,
                        isMain: true,
                        UserId
                    );
                    db.Set<Establishment>().Add(establishment);
                    await db.SaveChangesAsync();
                    var emissionPoint = EmissionPoint.Create(
                        TenantId,
                        companyId,
                        establishment.Id,
                        "001",
                        "Punto 1",
                        ERP.Domain.Modules.Company.Enums.EmissionType.Electronic,
                        isDefault: true,
                        UserId
                    );
                    db.Set<EmissionPoint>().Add(emissionPoint);
                    var register = CashRegister.Create(
                        TenantId,
                        companyId,
                        branch.Id,
                        "CAJA-01",
                        "Caja",
                        UserId
                    );
                    register.SetAccountingAccount(accounts["1.1.01.002"], UserId);
                    db.Set<CashRegister>().Add(register);
                    await db.SaveChangesAsync();
                    var session = CashSession.Open(
                        TenantId,
                        companyId,
                        branch.Id,
                        UserId,
                        register.Id,
                        "CAJA-01",
                        "Caja",
                        emissionPoint.Id,
                        "001",
                        1000m,
                        UserId
                    );
                    db.CashSessions.Add(session);
                    var batch = ImportBatch.Create(
                        TenantId,
                        companyId,
                        ImportType.InitialReceivables,
                        UserId
                    );
                    db.Add(batch);
                    await db.SaveChangesAsync();
                    var today = await sp.GetRequiredService<ICompanyClock>()
                        .TodayAsync(companyId, TenantId, default);
                    var receivables = new[] { "CXC-1", "CXC-2" }
                        .Select(number =>
                            SalesReceivable.CreateInitialBalance(
                                TenantId,
                                companyId,
                                branch.Id,
                                CustomerId,
                                number,
                                today,
                                today,
                                500m,
                                batch.Id,
                                UserId
                            )
                        )
                        .ToList();
                    db.SalesReceivables.AddRange(receivables);
                    await db.SaveChangesAsync();

                    return new CompanyScope(this)
                    {
                        CompanyId = companyId,
                        BranchId = branch.Id,
                        CashRegisterId = register.Id,
                        CashSessionId = session.Id,
                        BankAccountId = bankAccount.Id,
                        ReceivableIds = receivables.Select(r => r.Id).ToArray(),
                        DefaultCashAccount = accounts["1.1.01.001"],
                        CashAccount = accounts["1.1.01.002"],
                        BankAccount = accounts["1.1.02.001"],
                        Advances = accounts["2.1.01.002"],
                        Receivables = accounts["1.1.03.001"],
                    };
                }
            );
        }

        private sealed class TestLifetime : IHostApplicationLifetime
        {
            public CancellationToken ApplicationStarted => CancellationToken.None;
            public CancellationToken ApplicationStopping => CancellationToken.None;
            public CancellationToken ApplicationStopped => CancellationToken.None;

            public void StopApplication() { }
        }
    }
}
