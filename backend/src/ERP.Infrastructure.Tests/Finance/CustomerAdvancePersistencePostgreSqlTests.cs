using ERP.Application;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Domain.Audit;
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
using ERP.Infrastructure;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Finance;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F1 — persistencia real de anticipos de clientes (ADR-052): baseline
/// desde cero, restricciones físicas, DI oficial con auditoría, locks, <c>xmin</c> y aislamiento.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CustomerAdvancePersistencePostgreSqlTests
    : IClassFixture<CustomerAdvancePersistencePostgreSqlTests.Fixture>
{
    private readonly Fixture _f;

    public CustomerAdvancePersistencePostgreSqlTests(Fixture fixture) => _f = fixture;

    // ── Esquema y DI ─────────────────────────────────────────────────────

    [Fact]
    public async Task The_single_baseline_builds_the_schema_and_matches_the_model()
    {
        await _f.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<ErpDbContext>();
            (await db.Database.GetAppliedMigrationsAsync())
                .Should()
                .Equal("20261101012214_InitialProductionBaseline");
            (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
            db.Database.HasPendingModelChanges().Should().BeFalse();
        });

        var tables = await _f.ScalarListAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_name LIKE 'customer_advance%' ORDER BY table_name"
        );
        tables
            .Should()
            .Equal(
                "customer_advance_audit",
                "customer_advance_movements",
                "customer_advance_refund_transactions",
                "customer_advances"
            );
        var money = await _f.ScalarListAsync(
            "SELECT DISTINCT numeric_precision || ',' || numeric_scale FROM information_schema.columns "
                + "WHERE table_name LIKE 'customer_advance%' AND data_type = 'numeric'"
        );
        money.Should().Equal("18,2");
    }

    [Fact]
    public async Task The_official_container_resolves_both_repositories()
    {
        await _f.RunAsync(sp =>
        {
            sp.GetRequiredService<ICustomerAdvanceRepository>()
                .Should()
                .BeOfType<CustomerAdvanceRepository>();
            sp.GetRequiredService<ICustomerAdvanceRefundTransactionRepository>()
                .Should()
                .BeOfType<CustomerAdvanceRefundTransactionRepository>();
            return Task.CompletedTask;
        });
    }

    // ── Ciclo completo, eventos y auditoría ──────────────────────────────

    [Fact]
    public async Task A_full_cash_lifecycle_persists_ledger_cash_links_outbox_and_audit()
    {
        var advanceId = await _f.RegisterAsync(300m, cash: true);
        var applicationKey = Fixture.Key();

        await _f.OperateAsync(
            advanceId,
            (a, _) =>
            {
                a.Apply(
                    [_f.Line(_f.ReceivableOne, 70m), _f.Line(_f.ReceivableTwo, 30m)],
                    Fixture.Today,
                    _f.OtherBranchId,
                    Fixture.UserId,
                    applicationKey
                );
                return Task.CompletedTask;
            }
        );
        await _f.OperateAsync(
            advanceId,
            (a, _) =>
            {
                a.ReverseApplication(
                    applicationKey.Id,
                    Fixture.Undo(),
                    _f.BranchId,
                    Fixture.UserId,
                    Fixture.Key()
                );
                return Task.CompletedTask;
            }
        );
        var refundId = await _f.RefundAsync(advanceId, 100m, cash: true);
        await _f.ReverseRefundAsync(advanceId, refundId);
        await _f.OperateAsync(
            advanceId,
            async (a, sp) =>
            {
                var reversal = a.ReverseIncome(
                    Fixture.Undo(),
                    _f.BranchId,
                    Fixture.UserId,
                    Fixture.Key()
                );
                var (session, cash) = await _f.MoveCashAsync(
                    sp,
                    CashMovementType.ManualExpense,
                    reversal.Amount,
                    a.Movements[0].Id
                );
                a.LinkCashMovement(reversal.Id, session, cash);
            }
        );

        await _f.RunAsync(async sp =>
        {
            var advance = await sp.GetRequiredService<ICustomerAdvanceRepository>()
                .GetByIdAsync(_f.TenantId, advanceId);
            advance!.Status.Should().Be(CustomerAdvanceStatus.Cancelled);
            advance.AvailableAmount.Should().Be(0m);
            advance.BranchId.Should().Be(_f.BranchId);
            advance.Movements.Should().HaveCount(8);
            advance
                .Movements.Where(m => m.MovementType == CustomerAdvanceMovementType.Application)
                .Should()
                .OnlyContain(m => m.BranchId == _f.OtherBranchId);
            advance
                .Movements.Where(m => m.CashMovementId is not null)
                .Select(m => m.MovementType)
                .Should()
                .BeEquivalentTo([
                    CustomerAdvanceMovementType.Received,
                    CustomerAdvanceMovementType.IncomeReversed,
                ]);

            var db = sp.GetRequiredService<ErpDbContext>();
            var audits = await db
                .CustomerAdvanceAudits.Where(x => x.EntityId == advanceId)
                .ToListAsync();
            audits.Should().HaveCount(8);
            audits
                .Select(x => x.MovementId)
                .Should()
                .BeEquivalentTo(advance.Movements.Select(m => m.Id));
            audits
                .Should()
                .OnlyContain(x =>
                    x.UserId == Fixture.UserId
                    && x.UserName == "Test User"
                    && x.CompanyId == _f.CompanyId
                    && x.OccurredAtUtc != default
                );
            audits
                .Where(x =>
                    x.MovementType != CustomerAdvanceMovementType.Application
                    && x.MovementType != CustomerAdvanceMovementType.ReversalOfApplication
                )
                .Should()
                .OnlyContain(x =>
                    x.CashRegisterId == _f.CashRegisterId && x.CashMovementId != null
                );

            var payloads = await db
                .OutboxMessages.Where(x =>
                    x.TenantId == _f.TenantId && x.EventName.StartsWith("CustomerAdvance")
                )
                .Select(x => new { x.EventName, x.Payload })
                .ToListAsync();
            var own = payloads.Where(x => x.Payload.Contains(advanceId.ToString())).ToList();
            own.Should().HaveCount(8);
            own.Select(x => x.EventName)
                .Should()
                .Contain(nameof(CustomerAdvanceIncomeReversedEvent));
        });
    }

    [Fact]
    public async Task A_bank_advance_can_be_cancelled_administratively()
    {
        var advanceId = await _f.RegisterAsync(80m);

        await _f.OperateAsync(
            advanceId,
            (a, _) =>
            {
                a.CancelAdministratively(
                    true,
                    Fixture.Undo(),
                    _f.BranchId,
                    Fixture.UserId,
                    Fixture.Key()
                );
                return Task.CompletedTask;
            }
        );

        (await _f.AvailableAsync(advanceId)).Should().Be(0m);
        (await _f.CountAsync("customer_advance_movements", advanceId)).Should().Be(2);
    }

    [Fact]
    public async Task A_failing_audit_leaves_no_advance_movement_or_outbox_row()
    {
        var key = Fixture.Key();
        await using var provider = _f.BuildProvider(services =>
            services.AddScoped<IAuditService, FailingAuditService>()
        );

        var act = () => _f.RunAsync(sp => _f.AddBankAdvanceAsync(sp, 10m, key), provider);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("auditoría caída");
        var rows = await _f.ScalarListAsync(
            $"SELECT count(*)::text FROM customer_advance_movements WHERE client_request_id = '{key.Id}' "
                + $"UNION ALL SELECT count(*)::text FROM \"OutboxMessages\" WHERE \"Payload\"::text LIKE '%{key.Id}%'"
        );
        rows.Should().Equal("0", "0");
    }

    // ── Idempotencia ─────────────────────────────────────────────────────

    [Fact]
    public async Task A_client_request_id_cannot_be_shared_by_two_operations()
    {
        var first = await _f.RegisterAsync(100m);
        var second = await _f.RegisterAsync(100m);
        var used = await _f.RunAsync(async sp =>
            (
                await sp.GetRequiredService<ICustomerAdvanceRepository>()
                    .GetByIdAsync(_f.TenantId, first)
            )!
                .Movements[0]
                .ClientRequestId
        );

        // Registro (línea 0) frente a una aplicación de otro anticipo (líneas 1..N) con la misma clave.
        var act = () =>
            _f.OperateAsync(
                second,
                (a, _) =>
                {
                    a.Apply(
                        [_f.Line(_f.ReceivableOne, 1m), _f.Line(_f.ReceivableTwo, 1m)],
                        Fixture.Today,
                        _f.BranchId,
                        Fixture.UserId,
                        Fixture.Key(used)
                    );
                    return Task.CompletedTask;
                }
            );

        var error = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        error
            .InnerException.Should()
            .BeOfType<PostgresException>()
            .Which.ConstraintName.Should()
            .Be("uq_customer_advance_movements_tenant_request_operation");
        (await _f.CountAsync("customer_advance_movements", second)).Should().Be(1);
        (await _f.AvailableAsync(second)).Should().Be(100m);

        var found = await _f.RunAsync(sp =>
            sp.GetRequiredService<ICustomerAdvanceRepository>()
                .GetByClientRequestIdAsync(_f.TenantId, used)
        );
        found!.Id.Should().Be(first);
    }

    // ── Restricciones físicas ────────────────────────────────────────────

    [Theory]
    [InlineData("available_amount = -1", "chk_customer_advances_available_amount_range")]
    [InlineData(
        "available_amount = original_amount + 0.01",
        "chk_customer_advances_available_amount_range"
    )]
    [InlineData(
        "original_amount = 0, available_amount = 0",
        "chk_customer_advances_original_amount_positive"
    )]
    [InlineData("cash_register_id = gen_random_uuid()", "chk_customer_advances_destination_xor")]
    [InlineData("company_bank_account_id = NULL", "chk_customer_advances_destination_xor")]
    [InlineData("status = 2", "chk_customer_advances_status")]
    [InlineData("status = 3", "chk_customer_advances_status")]
    public async Task Advance_checks_reject_invalid_rows(string assignment, string constraint)
    {
        var error = await _f.FailsAsync(
            $"UPDATE customer_advances SET {assignment} WHERE id = '{_f.Sample.Untouched}'"
        );

        error.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        error.ConstraintName.Should().Be(constraint);
    }

    [Theory]
    [InlineData(4, """{"movement_type": 8}""", "chk_customer_advance_movements_type")]
    [InlineData(4, """{"amount": 0}""", "chk_customer_advance_movements_amount_positive")]
    [InlineData(2, """{"line_number": 0}""", "chk_customer_advance_movements_line_number")]
    [InlineData(4, """{"line_number": 1}""", "chk_customer_advance_movements_line_number")]
    [InlineData(2, """{"receivable_id": null}""", "chk_customer_advance_movements_receivable")]
    [InlineData(
        4,
        """{"installment_id": "11111111-1111-1111-1111-111111111111"}""",
        "chk_customer_advance_movements_receivable"
    )]
    [InlineData(
        3,
        """{"reversal_of_movement_id": null}""",
        "chk_customer_advance_movements_reversal_ref"
    )]
    [InlineData(
        4,
        """{"reversal_of_movement_id": "11111111-1111-1111-1111-111111111111"}""",
        "chk_customer_advance_movements_reversal_ref"
    )]
    [InlineData(4, """{"reason": null}""", "chk_customer_advance_movements_compensation_data")]
    [InlineData(3, """{"reference": null}""", "chk_customer_advance_movements_compensation_data")]
    [InlineData(5, """{"evidence_date": null}""", "chk_customer_advance_movements_evidence_date")]
    [InlineData(
        4,
        """{"cash_session_id": "11111111-1111-1111-1111-111111111111", "cash_movement_id": "11111111-1111-1111-1111-111111111111"}""",
        "chk_customer_advance_movements_cash_link"
    )]
    [InlineData(
        1,
        """{"cash_session_id": "11111111-1111-1111-1111-111111111111"}""",
        "chk_customer_advance_movements_cash_link"
    )]
    public async Task Movement_checks_reject_invalid_rows(int type, string patch, string constraint)
    {
        var error = await _f.FailsAsync(
            Fixture.Clone(
                "customer_advance_movements",
                _f.Sample.Movement(type),
                NewIdentity(patch)
            )
        );

        error.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        error.ConstraintName.Should().Be(constraint);
    }

    [Theory]
    [InlineData(
        false,
        """{"amount": 0}""",
        "chk_customer_advance_refund_transactions_amount_positive"
    )]
    [InlineData(
        false,
        """{"cash_register_id": "11111111-1111-1111-1111-111111111111"}""",
        "chk_customer_advance_refund_transactions_destination_xor"
    )]
    [InlineData(
        false,
        """{"company_bank_account_id": null}""",
        "chk_customer_advance_refund_transactions_destination_xor"
    )]
    [InlineData(
        false,
        """{"original_transaction_id": "11111111-1111-1111-1111-111111111111"}""",
        "chk_customer_advance_refund_transactions_original"
    )]
    [InlineData(
        true,
        """{"original_transaction_id": null}""",
        "chk_customer_advance_refund_transactions_original"
    )]
    [InlineData(
        false,
        """{"cash_session_id": "11111111-1111-1111-1111-111111111111", "cash_movement_id": "11111111-1111-1111-1111-111111111111"}""",
        "chk_customer_advance_refund_transactions_cash_link"
    )]
    public async Task Refund_transaction_checks_reject_invalid_rows(
        bool reversal,
        string patch,
        string constraint
    )
    {
        var source = reversal ? _f.Sample.ReversalTransaction : _f.Sample.RefundTransaction;
        var error = await _f.FailsAsync(
            Fixture.Clone("customer_advance_refund_transactions", source, NewIdentity(patch))
        );

        error.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        error.ConstraintName.Should().Be(constraint);
    }

    [Fact]
    public async Task Movement_unique_indexes_protect_identity_reversals_income_and_termination()
    {
        const string table = "customer_advance_movements";

        await ExpectUniqueAsync(
            Fixture.Clone(
                table,
                _f.Sample.SecondApplicationLine,
                $$"""{"id": "{{Guid.NewGuid()}}"}"""
            ),
            "uq_customer_advance_movements_tenant_request_line"
        );
        await ExpectUniqueAsync(
            Fixture.Clone(
                table,
                _f.Sample.Movement(1),
                $$"""{"id": "{{Guid.NewGuid()}}", "movement_type": 2, "line_number": 1, "receivable_id": "{{_f.ReceivableOne}}"}"""
            ),
            "uq_customer_advance_movements_tenant_request_operation"
        );
        await ExpectUniqueAsync(
            Fixture.Clone(table, _f.Sample.Movement(3), NewIdentity("{}")),
            "uq_customer_advance_movements_reversal_of_movement"
        );
        await ExpectUniqueAsync(
            Fixture.Clone(table, _f.Sample.Movement(1), NewIdentity("{}")),
            "uq_customer_advance_movements_received"
        );
        await ExpectUniqueAsync(
            Fixture.Clone(
                table,
                _f.Sample.Termination,
                NewIdentity(
                    $$"""{"movement_type": 6, "reversal_of_movement_id": "{{_f.Sample.Termination}}"}"""
                )
            ),
            "uq_customer_advance_movements_termination"
        );
    }

    [Fact]
    public async Task Refund_transaction_unique_indexes_protect_movement_original_and_request()
    {
        const string table = "customer_advance_refund_transactions";
        var free = _f.Sample.Movement(1);

        await ExpectUniqueAsync(
            Fixture.Clone(table, _f.Sample.RefundTransaction, NewIdentity("{}")),
            "uq_customer_advance_refund_transactions_movement"
        );
        await ExpectUniqueAsync(
            Fixture.Clone(
                table,
                _f.Sample.ReversalTransaction,
                NewIdentity($$"""{"customer_advance_movement_id": "{{free}}"}""")
            ),
            "uq_customer_advance_refund_transactions_original"
        );
        await ExpectUniqueAsync(
            Fixture.Clone(
                table,
                _f.Sample.RefundTransaction,
                $$"""{"id": "{{Guid.NewGuid()}}", "customer_advance_movement_id": "{{free}}"}"""
            ),
            "uq_customer_advance_refund_transactions_tenant_client_request"
        );
    }

    [Fact]
    public async Task Foreign_keys_block_cross_scope_references_and_physical_deletes()
    {
        const string movements = "customer_advance_movements";

        // Control: una copia válida entra (se revierte), así los fallos siguientes son de la FK.
        await _f.SucceedsAndRollsBackAsync(
            Fixture.Clone(movements, _f.Sample.Movement(4), NewIdentity("{}"))
        );

        await ExpectForeignKeyAsync(
            Fixture.Clone(
                movements,
                _f.Sample.Movement(4),
                NewIdentity($$"""{"company_id": "{{_f.OtherCompanyId}}"}""")
            ),
            "fk_customer_advance_movements_advance_scope"
        );
        await ExpectForeignKeyAsync(
            Fixture.Clone(
                movements,
                _f.Sample.Movement(3),
                NewIdentity($$"""{"reversal_of_movement_id": "{{_f.Sample.ForeignMovement}}"}""")
            ),
            "fk_customer_advance_movements_reversal_scope"
        );
        await ExpectForeignKeyAsync(
            Fixture.Clone(
                movements,
                _f.Sample.Movement(2),
                NewIdentity($$"""{"receivable_id": "{{Guid.NewGuid()}}"}""")
            ),
            "FK_customer_advance_movements_sales_receivables_receivable_id"
        );
        await ExpectForeignKeyAsync(
            Fixture.Clone(
                "customer_advance_refund_transactions",
                _f.Sample.RefundTransaction,
                NewIdentity(
                    $$"""{"customer_advance_movement_id": "{{_f.Sample.ForeignMovement}}"}"""
                )
            ),
            "fk_customer_advance_refund_transactions_movement_scope"
        );
        foreach (
            var delete in new[]
            {
                $"DELETE FROM customer_advances WHERE id = '{_f.Sample.Used}'",
                $"DELETE FROM {movements} WHERE id = '{_f.Sample.Movement(2)}'",
                $"DELETE FROM sales_receivables WHERE id = '{_f.ReceivableOne}'",
            }
        )
            (await _f.FailsAsync(delete))
                .SqlState.Should()
                .Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task Installment_references_have_no_physical_foreign_key()
    {
        var keys = await _f.ScalarListAsync(
            "SELECT conname::text FROM pg_constraint WHERE contype = 'f' "
                + "AND conrelid = 'customer_advance_movements'::regclass "
                + "AND pg_get_constraintdef(oid) LIKE '%installment_id%'"
        );

        keys.Should().BeEmpty();
        await _f.SucceedsAndRollsBackAsync(
            Fixture.Clone(
                "customer_advance_movements",
                _f.Sample.Movement(2),
                NewIdentity($$"""{"installment_id": "{{Guid.NewGuid()}}"}""")
            )
        );
    }

    [Fact]
    public async Task Persisted_history_and_ownership_cannot_be_modified_through_the_context()
    {
        foreach (
            var mutate in new Action<ErpDbContext, CustomerAdvance>[]
            {
                (db, a) => db.Entry(a).Property(x => x.OriginalAmount).CurrentValue = 1m,
                (db, a) => db.Entry(a).Property(x => x.CompanyId).CurrentValue = _f.OtherCompanyId,
                (db, a) => db.Entry(a).Property(x => x.BranchId).CurrentValue = _f.OtherBranchId,
                (db, a) => db.Entry(a.Movements[0]).Property(x => x.Amount).CurrentValue = 1m,
            }
        )
        {
            var act = () =>
                _f.RunAsync(async sp =>
                {
                    var db = sp.GetRequiredService<ErpDbContext>();
                    var advance = await sp.GetRequiredService<ICustomerAdvanceRepository>()
                        .GetByIdAsync(_f.TenantId, _f.Sample.Untouched);
                    mutate(db, advance!);
                    await db.SaveChangesAsync();
                });

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    [Theory]
    [InlineData("removeApplication")]
    [InlineData("removeReceived")]
    [InlineData("severFromCollection")]
    [InlineData("removeAdvanceWithMovements")]
    [InlineData("removeAdvanceAlone")]
    [InlineData("removeRefundTransaction")]
    [InlineData("removeAudit")]
    [InlineData("eraseCashLink")]
    [InlineData("replaceCashLink")]
    [InlineData("editAudit")]
    public async Task Financial_history_cannot_be_erased_or_rewritten_through_the_context(
        string attempt
    )
    {
        var advanceId = await _f.RegisterAsync(100m, cash: true);
        await _f.OperateAsync(
            advanceId,
            (a, _) =>
            {
                a.Apply(
                    [_f.Line(_f.ReceivableOne, 10m)],
                    Fixture.Today,
                    _f.BranchId,
                    Fixture.UserId,
                    Fixture.Key()
                );
                return Task.CompletedTask;
            }
        );
        await _f.RefundAsync(advanceId, 5m, cash: false);
        var before = await _f.HistoryAsync(advanceId);

        var act = () =>
            _f.RunAsync(async sp =>
            {
                var db = sp.GetRequiredService<ErpDbContext>();
                var query = db.CustomerAdvances.AsQueryable();
                if (attempt != "removeAdvanceAlone")
                    query = query.Include(x => x.Movements);
                var advance = await query.SingleAsync(x => x.Id == advanceId);
                CustomerAdvanceMovement Movement(CustomerAdvanceMovementType type) =>
                    advance.Movements.Single(m => m.MovementType == type);

                switch (attempt)
                {
                    case "removeApplication":
                        db.Remove(Movement(CustomerAdvanceMovementType.Application));
                        break;
                    case "removeReceived":
                        db.Remove(Movement(CustomerAdvanceMovementType.Received));
                        break;
                    case "severFromCollection":
                        db.Entry(advance).Collection(x => x.Movements).CurrentValue = advance
                            .Movements.Where(m =>
                                m.MovementType != CustomerAdvanceMovementType.Application
                            )
                            .ToList();
                        break;
                    case "removeAdvanceWithMovements":
                    case "removeAdvanceAlone":
                        db.Remove(advance);
                        break;
                    case "removeRefundTransaction":
                        db.Remove(
                            await db.CustomerAdvanceRefundTransactions.SingleAsync(x =>
                                x.CustomerAdvanceId == advanceId
                            )
                        );
                        break;
                    case "removeAudit":
                        db.RemoveRange(
                            await db
                                .CustomerAdvanceAudits.Where(x => x.EntityId == advanceId)
                                .ToListAsync()
                        );
                        break;
                    case "eraseCashLink":
                        var linked = db.Entry(Movement(CustomerAdvanceMovementType.Received));
                        linked.Property(x => x.CashSessionId).CurrentValue = null;
                        linked.Property(x => x.CashMovementId).CurrentValue = null;
                        break;
                    case "replaceCashLink":
                        db.Entry(Movement(CustomerAdvanceMovementType.Received))
                            .Property(x => x.CashMovementId)
                            .CurrentValue = Guid.NewGuid();
                        break;
                    case "editAudit":
                        var audit = await db.CustomerAdvanceAudits.FirstAsync(x =>
                            x.EntityId == advanceId
                        );
                        db.Entry(audit).Property(x => x.Amount).CurrentValue = 999m;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(attempt));
                }
                await db.SaveChangesAsync();
            });

        // EF o la comprobación del contexto rechazan antes de escribir; la baja del anticipo sin
        // sus movimientos cargados también.
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await _f.HistoryAsync(advanceId)).Should().Equal(before);
    }

    // ── Concurrencia ─────────────────────────────────────────────────────

    [Fact]
    public async Task For_update_serializes_operations_and_the_second_one_sees_the_new_balance()
    {
        var advanceId = await _f.RegisterAsync(100m);
        var locked = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        var first = _f.OperateAsync(
            advanceId,
            async (a, _) =>
            {
                locked.SetResult();
                await release.Task;
                a.RegisterRefund(100m, Fixture.Undo(), _f.BranchId, Fixture.UserId, Fixture.Key());
            }
        );
        await locked.Task;

        var seen = -1m;
        var second = Task.Run(() =>
            _f.OperateAsync(
                advanceId,
                (a, _) =>
                {
                    seen = a.AvailableAmount;
                    a.RegisterRefund(
                        1m,
                        Fixture.Undo(),
                        _f.BranchId,
                        Fixture.UserId,
                        Fixture.Key()
                    );
                    return Task.CompletedTask;
                }
            )
        );

        // La segunda operación debe quedar esperando el lock, no decidir con el saldo anterior.
        await _f.WaitForBlockedBackendAsync();
        second.IsCompleted.Should().BeFalse();
        release.SetResult();
        await first;

        await FluentActions
            .Awaiting(() => second)
            .Should()
            .ThrowAsync<DomainRuleViolationException>();
        seen.Should().Be(0m);
        (await _f.AvailableAsync(advanceId)).Should().Be(0m);
        (await _f.CountAsync("customer_advance_movements", advanceId)).Should().Be(2);
    }

    [Fact]
    public async Task Xmin_rejects_a_stale_write_without_leaving_movements_or_audit()
    {
        var advanceId = await _f.RegisterAsync(100m);
        var loaded = new TaskCompletionSource();
        var winnerSaved = new TaskCompletionSource();

        var stale = _f.RunAsync(async sp =>
        {
            var repo = sp.GetRequiredService<ICustomerAdvanceRepository>();
            var advance = await repo.GetByIdAsync(_f.TenantId, advanceId);
            loaded.SetResult();
            await winnerSaved.Task;
            advance!.RegisterRefund(
                10m,
                Fixture.Undo(),
                _f.BranchId,
                Fixture.UserId,
                Fixture.Key()
            );
            await repo.SaveChangesAsync();
        });
        await loaded.Task;
        await _f.RefundAsync(advanceId, 20m, cash: false);
        winnerSaved.SetResult();

        await FluentActions
            .Awaiting(() => stale)
            .Should()
            .ThrowAsync<DbUpdateConcurrencyException>();
        (await _f.AvailableAsync(advanceId)).Should().Be(80m);
        (await _f.CountAsync("customer_advance_movements", advanceId)).Should().Be(2);
        (await _f.CountAsync("customer_advance_audit", advanceId, "entity_id")).Should().Be(2);
    }

    // ── Aislamiento ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("otherCompany")]
    [InlineData("otherTenant")]
    [InlineData("noCompany")]
    public async Task Reads_are_fail_closed_outside_the_tenant_and_company(string context)
    {
        var (tenant, company) = context switch
        {
            "otherCompany" => (_f.TenantId, _f.OtherCompanyId),
            "otherTenant" => (_f.OtherTenantId, _f.ForeignTenantCompanyId),
            _ => (_f.TenantId, Guid.Empty),
        };
        var key = await _f.RunAsync(async sp =>
            (
                await sp.GetRequiredService<ICustomerAdvanceRepository>()
                    .GetByIdAsync(_f.TenantId, _f.Sample.Used)
            )!
                .Movements[0]
                .ClientRequestId
        );

        await _f.RunAsync(
            async sp =>
            {
                var repo = sp.GetRequiredService<ICustomerAdvanceRepository>();
                var refunds = sp.GetRequiredService<ICustomerAdvanceRefundTransactionRepository>();
                var db = sp.GetRequiredService<ErpDbContext>();
                var uow = sp.GetRequiredService<IUnitOfWork>();

                // Aun pidiendo el tenant propietario, el contexto manda.
                (await repo.GetByIdAsync(_f.TenantId, _f.Sample.Used))
                    .Should()
                    .BeNull();
                (await repo.GetByClientRequestIdAsync(_f.TenantId, key)).Should().BeNull();
                await uow.BeginTransactionAsync(default);
                (await repo.GetByIdForUpdateAsync(_f.TenantId, _f.Sample.Used)).Should().BeNull();
                await uow.RollbackAsync(default);
                (await refunds.GetByIdAsync(_f.TenantId, _f.Sample.RefundTransaction))
                    .Should()
                    .BeNull();
                (await db.CustomerAdvances.CountAsync()).Should().Be(0);
                (await db.CustomerAdvanceMovements.CountAsync()).Should().Be(0);
                (await db.CustomerAdvanceRefundTransactions.CountAsync()).Should().Be(0);
                (await db.CustomerAdvanceAudits.CountAsync()).Should().Be(0);
            },
            tenantId: tenant,
            companyId: company
        );
    }

    [Fact]
    public async Task The_owner_company_reads_the_advance_and_its_refund_transaction()
    {
        await _f.RunAsync(async sp =>
        {
            var advance = await sp.GetRequiredService<ICustomerAdvanceRepository>()
                .GetByIdAsync(_f.TenantId, _f.Sample.Used);
            var refunds = sp.GetRequiredService<ICustomerAdvanceRefundTransactionRepository>();
            var transaction = await refunds.GetByIdAsync(_f.TenantId, _f.Sample.RefundTransaction);

            advance.Should().NotBeNull();
            (
                await refunds.GetByMovementIdAsync(
                    _f.TenantId,
                    transaction!.CustomerAdvanceMovementId
                )
            )!
                .Id.Should()
                .Be(transaction.Id);
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Agrega al parche un Id y una clave nuevos para aislar la restricción probada.</summary>
    private static string NewIdentity(string patch)
    {
        var identity = $"\"id\": \"{Guid.NewGuid()}\", \"client_request_id\": \"{Guid.NewGuid()}\"";
        var body = patch.Trim()[1..^1].Trim();
        return body.Length == 0 ? $"{{{identity}}}" : $"{{{identity}, {body}}}";
    }

    private async Task ExpectUniqueAsync(string sql, string constraint)
    {
        var error = await _f.FailsAsync(sql);
        error.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        error.ConstraintName.Should().Be(constraint);
    }

    private async Task ExpectForeignKeyAsync(string sql, string constraint)
    {
        var error = await _f.FailsAsync(sql);
        error.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        error.ConstraintName.Should().Be(constraint);
    }

    private sealed class FailingAuditService : IAuditService
    {
        public Task RecordAsync<TAudit>(TAudit record, CancellationToken ct = default)
            where TAudit : AuditRecordBase =>
            throw new InvalidOperationException("auditoría caída");
    }

    private sealed class TestLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() { }
    }

    /// <summary>Datos de referencia para las pruebas de restricciones.</summary>
    public sealed record SampleData(
        Guid Used,
        Guid Untouched,
        IReadOnlyDictionary<int, Guid> Movements,
        Guid SecondApplicationLine,
        Guid Termination,
        Guid ForeignMovement,
        Guid RefundTransaction,
        Guid ReversalTransaction
    )
    {
        public Guid Movement(int type) => Movements[type];
    }

    /// <summary>Un PostgreSQL desechable por clase, migrado desde cero y con el DI oficial.</summary>
    public sealed class Fixture : IAsyncLifetime
    {
        public static readonly Guid UserId = Guid.NewGuid();
        public static readonly DateOnly Today = new(2026, 11, 26);

        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("erp_customer_advances_test")
            .WithUsername("erp")
            .WithPassword("erp_test_secret")
            .Build();

        private ServiceProvider _provider = null!;

        public Guid TenantId { get; private set; }
        public Guid OtherTenantId { get; private set; }
        public Guid CompanyId { get; private set; }
        public Guid OtherCompanyId { get; private set; }
        public Guid ForeignTenantCompanyId { get; private set; }
        public Guid BranchId { get; private set; }
        public Guid OtherBranchId { get; private set; }
        public Guid CustomerId { get; private set; }
        public Guid ReceivableOne { get; private set; }
        public Guid ReceivableTwo { get; private set; }
        public Guid CashMethodId { get; private set; }
        public Guid TransferMethodId { get; private set; }
        public Guid BankAccountId { get; private set; }
        public Guid CashRegisterId { get; private set; }
        public Guid CashSessionId { get; private set; }
        public SampleData Sample { get; private set; } = null!;

        public static ClientRequestKey Key(Guid? id = null) =>
            new(id ?? Guid.NewGuid(), new string('A', ClientRequestKey.PayloadHashLength));

        public static CustomerAdvanceCompensation Undo() => new("Motivo", "REF-1", Today, Today);

        public CustomerAdvanceApplicationLine Line(Guid receivableId, decimal amount) =>
            new(_receivables[receivableId], null, amount);

        private readonly Dictionary<Guid, SalesReceivable> _receivables = new();

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();
            _provider = BuildProvider();
            await using (var db = PlainContext(Guid.Empty, Guid.Empty))
                await db.Database.MigrateAsync();
            await SeedAsync();
            Sample = await SeedSampleAsync();
        }

        public async Task DisposeAsync()
        {
            await _provider.DisposeAsync();
            await _postgres.DisposeAsync();
        }

        /// <summary>Mismo registro que el host; solo el actor de auditoría se fija (sin HTTP).</summary>
        public ServiceProvider BuildProvider(Action<IServiceCollection>? overrides = null)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                        ["InstallData:Enabled"] = "false",
                        ["Snowflake:WorkerId"] = "23",
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
            services.AddScoped<IAuditContext>(sp => new FixedAuditContext(
                () => sp.GetRequiredService<ICurrentTenant>().TenantId,
                () => sp.GetRequiredService<ICurrentCompany>().CompanyId,
                UserId
            ));
            overrides?.Invoke(services);
            return services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true }
            );
        }

        public async Task<T> RunAsync<T>(
            Func<IServiceProvider, Task<T>> action,
            ServiceProvider? provider = null,
            Guid? tenantId = null,
            Guid? companyId = null
        )
        {
            var previous = (JobTenantContext.Current, JobCompanyContext.Current);
            try
            {
                JobTenantContext.Current = tenantId ?? TenantId;
                JobCompanyContext.Current = companyId ?? CompanyId;
                await using var scope = (provider ?? _provider).CreateAsyncScope();
                return await action(scope.ServiceProvider);
            }
            finally
            {
                (JobTenantContext.Current, JobCompanyContext.Current) = previous;
            }
        }

        public Task RunAsync(
            Func<IServiceProvider, Task> action,
            ServiceProvider? provider = null,
            Guid? tenantId = null,
            Guid? companyId = null
        ) =>
            RunAsync(
                async sp =>
                {
                    await action(sp);
                    return true;
                },
                provider,
                tenantId,
                companyId
            );

        /// <summary>Patrón oficial: transacción → lock → decisión → persistencia.</summary>
        public Task OperateAsync(
            Guid advanceId,
            Func<CustomerAdvance, IServiceProvider, Task> action
        ) =>
            RunAsync(async sp =>
            {
                var uow = sp.GetRequiredService<IUnitOfWork>();
                var repo = sp.GetRequiredService<ICustomerAdvanceRepository>();
                await uow.BeginTransactionAsync(default);
                try
                {
                    var advance = await repo.GetByIdForUpdateAsync(TenantId, advanceId);
                    await action(advance!, sp);
                    await repo.SaveChangesAsync();
                    await uow.CommitAsync(default);
                }
                catch
                {
                    await uow.RollbackAsync(default);
                    throw;
                }
            });

        public async Task<Guid> AddBankAdvanceAsync(
            IServiceProvider sp,
            decimal amount,
            ClientRequestKey key
        )
        {
            var advance = CustomerAdvance.Register(
                TenantId,
                CompanyId,
                BranchId,
                CustomerId,
                "USD",
                amount,
                Today,
                TransferMethodId,
                BankAccountId,
                null,
                UserId,
                key,
                "TRX-1"
            );
            var repo = sp.GetRequiredService<ICustomerAdvanceRepository>();
            await repo.AddAsync(advance);
            await repo.SaveChangesAsync();
            return advance.Id;
        }

        public Task<Guid> RegisterAsync(decimal amount, bool cash = false) =>
            RunAsync(async sp =>
            {
                if (!cash)
                    return await AddBankAdvanceAsync(sp, amount, Key());

                var advance = CustomerAdvance.Register(
                    TenantId,
                    CompanyId,
                    BranchId,
                    CustomerId,
                    "USD",
                    amount,
                    Today,
                    CashMethodId,
                    null,
                    CashRegisterId,
                    UserId,
                    Key()
                );
                var received = advance.Movements[0];
                var (session, movement) = await MoveCashAsync(
                    sp,
                    CashMovementType.ManualIncome,
                    amount,
                    received.Id
                );
                advance.LinkCashMovement(received.Id, session, movement);
                var repo = sp.GetRequiredService<ICustomerAdvanceRepository>();
                await repo.AddAsync(advance);
                await repo.SaveChangesAsync();
                return advance.Id;
            });

        public async Task<(Guid SessionId, Guid MovementId)> MoveCashAsync(
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

        public async Task<Guid> RefundAsync(Guid advanceId, decimal amount, bool cash)
        {
            var transactionId = Guid.Empty;
            await OperateAsync(
                advanceId,
                async (a, sp) =>
                {
                    var movement = a.RegisterRefund(amount, Undo(), BranchId, UserId, Key());
                    var transaction = CustomerAdvanceRefundTransaction.CreateRefund(
                        a,
                        movement,
                        cash ? CashMethodId : TransferMethodId,
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
                    transactionId = transaction.Id;
                }
            );
            return transactionId;
        }

        public async Task<Guid> ReverseRefundAsync(Guid advanceId, Guid transactionId)
        {
            var reversalId = Guid.Empty;
            await OperateAsync(
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
                    reversalId = reversal.Id;
                }
            );
            return reversalId;
        }

        /// <summary>Foto de todas las filas del historial, leída directamente de la BD.</summary>
        public Task<List<string>> HistoryAsync(Guid advanceId) =>
            ScalarListAsync(
                $"SELECT 'a' || t::text FROM customer_advances t WHERE id = '{advanceId}' "
                    + $"UNION ALL SELECT 'm' || t::text FROM customer_advance_movements t WHERE customer_advance_id = '{advanceId}' "
                    + $"UNION ALL SELECT 'r' || t::text FROM customer_advance_refund_transactions t WHERE customer_advance_id = '{advanceId}' "
                    + $"UNION ALL SELECT 'u' || t::text FROM customer_advance_audit t WHERE entity_id = '{advanceId}' ORDER BY 1"
            );

        public async Task<decimal> AvailableAsync(Guid advanceId) =>
            decimal.Parse(
                (
                    await ScalarListAsync(
                        $"SELECT available_amount::text FROM customer_advances WHERE id = '{advanceId}'"
                    )
                ).Single(),
                System.Globalization.CultureInfo.InvariantCulture
            );

        public async Task<int> CountAsync(
            string table,
            Guid advanceId,
            string column = "customer_advance_id"
        ) =>
            int.Parse(
                (
                    await ScalarListAsync(
                        $"SELECT count(*)::text FROM {table} WHERE {column} = '{advanceId}'"
                    )
                ).Single(),
                System.Globalization.CultureInfo.InvariantCulture
            );

        public static string Clone(string table, Guid sourceId, string patch) =>
            $"INSERT INTO {table} SELECT (jsonb_populate_record(s, '{patch}'::jsonb)).* "
            + $"FROM {table} s WHERE s.id = '{sourceId}'";

        public async Task<List<string>> ScalarListAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            var values = new List<string>();
            while (await reader.ReadAsync())
                values.Add(reader.GetString(0));
            return values;
        }

        /// <summary>Ejecuta SQL directo dentro de una transacción que siempre se revierte.</summary>
        private async Task<int> ExecuteRolledBackAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            return await command.ExecuteNonQueryAsync();
        }

        public async Task SucceedsAndRollsBackAsync(string sql) =>
            (await ExecuteRolledBackAsync(sql)).Should().Be(1);

        public async Task<PostgresException> FailsAsync(string sql)
        {
            var act = () => ExecuteRolledBackAsync(sql);
            return (await act.Should().ThrowAsync<PostgresException>()).Which;
        }

        /// <summary>Espera a que PostgreSQL reporte una sesión bloqueada por un lock de fila.</summary>
        public async Task WaitForBlockedBackendAsync()
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var blocked = await ScalarListAsync(
                    "SELECT count(*)::text FROM pg_stat_activity WHERE wait_event_type = 'Lock' "
                        + "AND query LIKE '%customer_advances%FOR UPDATE%'"
                );
                if (blocked.Single() != "0")
                    return;
                await Task.Delay(50);
            }
            throw new TimeoutException("Ninguna sesión quedó esperando el lock del anticipo.");
        }

        private ErpDbContext PlainContext(Guid tenantId, Guid companyId) =>
            new(
                new DbContextOptionsBuilder<ErpDbContext>()
                    .UseNpgsql(_postgres.GetConnectionString())
                    .AddInterceptors(
                        new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
                    )
                    .Options,
                new FixedCurrentTenant(tenantId),
                new NoOpPublisher(),
                new FixedCurrentCompany(companyId)
            );

        private async Task SeedAsync()
        {
            await using var seed = PlainContext(Guid.Empty, Guid.Empty);
            var tenant = Tenant.Create("Tenant A", $"a-{Guid.NewGuid():N}"[..16], UserId);
            var foreignTenant = Tenant.Create("Tenant B", $"b-{Guid.NewGuid():N}"[..16], UserId);
            var company = Company.CreateManaged(
                tenant.Id,
                "1790012345001",
                "A1 S.A.",
                createdBy: UserId
            );
            var otherCompany = Company.CreateManaged(
                tenant.Id,
                "1790016919001",
                "A2 S.A.",
                createdBy: UserId
            );
            var foreignCompany = Company.CreateManaged(
                foreignTenant.Id,
                "1790085783001",
                "B1 S.A.",
                createdBy: UserId
            );
            seed.Tenants.AddRange(tenant, foreignTenant);
            seed.Companies.AddRange(company, otherCompany, foreignCompany);
            await seed.SaveChangesAsync();
            (TenantId, OtherTenantId) = (tenant.Id, foreignTenant.Id);
            (CompanyId, OtherCompanyId, ForeignTenantCompanyId) = (
                company.Id,
                otherCompany.Id,
                foreignCompany.Id
            );

            await using var db = PlainContext(TenantId, CompanyId);
            var branch = NewBranch("B01", true);
            var otherBranch = NewBranch("B02", false);
            db.Branches.AddRange(branch, otherBranch);
            var customer = BusinessPartner.Create(
                TenantId,
                "05",
                "1710034065",
                1,
                "Cliente",
                UserId
            );
            db.BusinessPartners.Add(customer);
            var cashMethod = PaymentMethod.Create(
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
            db.Set<PaymentMethod>().AddRange(cashMethod, transfer);
            var account = Account.Create(
                TenantId,
                CompanyId,
                AccountCode.Create("1.1.01.001"),
                "Caja",
                null,
                AccountType.Asset,
                AccountNature.Debit,
                allowsPosting: true,
                createdBy: UserId
            );
            db.Accounts.Add(account);
            var bank = Bank.Create(TenantId, "PICHINCHA", "Banco Pichincha", "Pichincha", UserId);
            db.Set<Bank>().Add(bank);
            await db.SaveChangesAsync();
            (BranchId, OtherBranchId, CustomerId) = (branch.Id, otherBranch.Id, customer.Id);
            (CashMethodId, TransferMethodId) = (cashMethod.Id, transfer.Id);

            var bankAccount = CompanyBankAccount.Create(
                TenantId,
                CompanyId,
                bank.Id,
                BankAccountType.Checking,
                "1234567890",
                "Cuenta corriente",
                account.Id,
                UserId
            );
            db.Set<CompanyBankAccount>().Add(bankAccount);
            var establishment = Establishment.Create(
                TenantId,
                BranchId,
                CompanyId,
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
                CompanyId,
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
                CompanyId,
                BranchId,
                "CAJA-01",
                "Caja",
                UserId
            );
            db.Set<CashRegister>().Add(register);
            await db.SaveChangesAsync();
            var session = CashSession.Open(
                TenantId,
                CompanyId,
                BranchId,
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
                CompanyId,
                ImportType.InitialReceivables,
                UserId
            );
            db.Add(batch);
            await db.SaveChangesAsync();
            (BankAccountId, CashRegisterId, CashSessionId) = (
                bankAccount.Id,
                register.Id,
                session.Id
            );

            var receivables = new[] { "CXC-1", "CXC-2" }
                .Select(number =>
                    SalesReceivable.CreateInitialBalance(
                        TenantId,
                        CompanyId,
                        OtherBranchId,
                        CustomerId,
                        number,
                        Today,
                        Today,
                        500m,
                        batch.Id,
                        UserId
                    )
                )
                .ToList();
            db.SalesReceivables.AddRange(receivables);
            await db.SaveChangesAsync();
            (ReceivableOne, ReceivableTwo) = (receivables[0].Id, receivables[1].Id);
            foreach (var receivable in receivables)
                _receivables[receivable.Id] = receivable;
        }

        private Branch NewBranch(string code, bool main) =>
            Branch.Create(
                tenantId: TenantId,
                name: code,
                address: "Av. Principal",
                code: code,
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
                isMainBranch: main,
                createdBy: UserId,
                companyId: CompanyId
            );

        private async Task<SampleData> SeedSampleAsync()
        {
            var used = await RegisterAsync(500m);
            var applicationKey = Key();
            await OperateAsync(
                used,
                (a, _) =>
                {
                    a.Apply(
                        [Line(ReceivableOne, 50m), Line(ReceivableTwo, 60m)],
                        Today,
                        BranchId,
                        UserId,
                        applicationKey
                    );
                    return Task.CompletedTask;
                }
            );
            await OperateAsync(
                used,
                (a, _) =>
                {
                    a.ReverseApplication(applicationKey.Id, Undo(), BranchId, UserId, Key());
                    return Task.CompletedTask;
                }
            );
            var refund = await RefundAsync(used, 40m, cash: false);
            var reversal = await ReverseRefundAsync(used, refund);

            var untouched = await RegisterAsync(100m);
            var terminated = await RegisterAsync(100m);
            await OperateAsync(
                terminated,
                (a, _) =>
                {
                    a.ReverseIncome(Undo(), BranchId, UserId, Key());
                    return Task.CompletedTask;
                }
            );

            return await RunAsync(async sp =>
            {
                var repo = sp.GetRequiredService<ICustomerAdvanceRepository>();
                var main = (await repo.GetByIdAsync(TenantId, used))!;
                var other = (await repo.GetByIdAsync(TenantId, untouched))!;
                var ended = (await repo.GetByIdAsync(TenantId, terminated))!;
                var byType = main
                    .Movements.GroupBy(m => (int)m.MovementType)
                    .ToDictionary(g => g.Key, g => g.OrderBy(m => m.LineNumber).First().Id);
                return new SampleData(
                    used,
                    untouched,
                    byType,
                    main.Movements.Single(m =>
                        m.MovementType == CustomerAdvanceMovementType.Application
                        && m.LineNumber == 2
                    ).Id,
                    ended
                        .Movements.Single(m =>
                            m.MovementType == CustomerAdvanceMovementType.IncomeReversed
                        )
                        .Id,
                    other.Movements[0].Id,
                    refund,
                    reversal
                );
            });
        }

        private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
        {
            public Guid TenantId => tenantId;
            public string? Slug => null;
        }

        private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
        {
            public Guid CompanyId => companyId;
            public bool IsAuthenticated => true;
            public bool HasCompanyContext => companyId != Guid.Empty;
        }

        private sealed class NoOpPublisher : IPublisher
        {
            public Task Publish(
                object notification,
                CancellationToken cancellationToken = default
            ) => Task.CompletedTask;

            public Task Publish<TNotification>(
                TNotification notification,
                CancellationToken cancellationToken = default
            )
                where TNotification : INotification => Task.CompletedTask;
        }
    }
}
