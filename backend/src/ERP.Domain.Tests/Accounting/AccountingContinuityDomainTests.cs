using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — estados de la obligación contable durable e historial de intentos, y
/// versionado con vigencia de la política operativa por Company.
/// </summary>
public sealed class AccountingContinuityDomainTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 16, 15, 0, 0, DateTimeKind.Utc);

    private static AccountingPosting Pending() =>
        AccountingPosting.CreatePending(
            TenantId,
            CompanyId,
            "Purchases",
            "InvoiceReceived",
            Guid.NewGuid(),
            "001-001-000000001",
            new DateOnly(2026, 7, 25),
            new DateOnly(2026, 7, 25),
            1,
            "{}",
            UserId,
            "corr"
        );

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Manual_reason_is_required_before_state_changes(string? reason)
    {
        var posting = Pending();
        var attempt = () => posting.RecordFailed("X", "Y", AccountingPostingAttemptTrigger.Manual,
            UserId, "correlation", reason, Now, Now);
        attempt.Should().Throw<ArgumentException>();
        posting.Status.Should().Be(AccountingPostingStatus.Pending);
        posting.Attempts.Should().Be(0);
    }

    [Fact]
    public void Nace_Pending_sin_intentos_ni_asiento()
    {
        var posting = Pending();

        posting.Status.Should().Be(AccountingPostingStatus.Pending);
        posting.Attempts.Should().Be(0);
        posting.JournalEntryId.Should().BeNull();
        posting.LastAttemptAtUtc.Should().BeNull();
    }

    [Fact]
    public void Intento_fallido_y_luego_exitoso_registra_historial_numerado_y_limpia_el_error()
    {
        var posting = Pending();
        var reprocessor = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var first = posting.RecordFailed("RULE_NOT_FOUND", "Sin regla", AccountingPostingAttemptTrigger.Initial, UserId, "c1", null, Now, Now);
        var second = posting.RecordPosted(entryId, AccountingPostingAttemptTrigger.Manual, reprocessor, "c2", "Regla corregida", Now, Now.AddSeconds(1));

        first.AttemptNumber.Should().Be(1);
        first.Outcome.Should().Be(AccountingPostingStatus.Failed);
        first.ErrorCode.Should().Be("RULE_NOT_FOUND");
        second.AttemptNumber.Should().Be(2);
        second.Outcome.Should().Be(AccountingPostingStatus.Posted);
        second.TriggeredBy.Should().Be(reprocessor);
        second.JournalEntryId.Should().Be(entryId);
        posting.Status.Should().Be(AccountingPostingStatus.Posted);
        posting.Attempts.Should().Be(2);
        posting.ErrorCode.Should().BeNull();
        posting.ErrorMessage.Should().BeNull();
        posting.LastAttemptAtUtc.Should().Be(Now.AddSeconds(1));
    }

    [Fact]
    public void Posted_no_admite_nuevos_intentos_ni_cancelacion()
    {
        var posting = Pending();
        posting.RecordPosted(Guid.NewGuid(), AccountingPostingAttemptTrigger.Initial, UserId, null, null, Now, Now);

        var retry = () => posting.RecordFailed("X", "Y", AccountingPostingAttemptTrigger.Manual, UserId, null, "Reproceso", Now, Now);
        var cancel = () => posting.Cancel(UserId);

        retry.Should().Throw<DomainRuleViolationException>();
        cancel.Should().Throw<DomainRuleViolationException>().WithMessage("*reverso*");
    }

    [Fact]
    public void Failed_se_cancela_y_luego_no_admite_intentos()
    {
        var posting = Pending();
        posting.RecordFailed("RULE_NOT_FOUND", null, AccountingPostingAttemptTrigger.Initial, UserId, null, null, Now, Now);

        posting.Cancel(UserId);
        var retry = () => posting.RecordPosted(Guid.NewGuid(), AccountingPostingAttemptTrigger.Manual, UserId, null, "Reproceso", Now, Now);

        posting.Status.Should().Be(AccountingPostingStatus.Canceled);
        posting.IsResolved.Should().BeTrue();
        retry.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Error_sin_codigo_usa_codigo_generico_y_trunca_mensajes_largos()
    {
        var posting = Pending();

        var attempt = posting.RecordFailed(null, new string('x', 5000), AccountingPostingAttemptTrigger.Initial, UserId, null, null, Now, Now);

        posting.ErrorCode.Should().Be("POSTING_FAILED");
        posting.ErrorMessage!.Length.Should().Be(AccountingPosting.ErrorMessageMaxLength);
        attempt.ErrorMessage!.Length.Should().Be(AccountingPostingAttempt.ErrorMessageMaxLength);
    }

    [Fact]
    public void Politica_nueva_version_cierra_la_anterior_el_dia_previo_sin_solapar()
    {
        var first = AccountingOperationalPolicy.Create(TenantId, CompanyId, AccountingOperationalMode.Enabled, new DateOnly(2026, 1, 1), null, UserId);

        first.EndBefore(new DateOnly(2026, 8, 1), UserId);

        first.EffectiveTo.Should().Be(new DateOnly(2026, 7, 31));
        first.Covers(new DateOnly(2026, 7, 31)).Should().BeTrue();
        first.Covers(new DateOnly(2026, 8, 1)).Should().BeFalse();
        first.Covers(new DateOnly(2025, 12, 31)).Should().BeFalse();
    }

    [Fact]
    public void Politica_no_puede_cerrarse_antes_de_su_inicio_ni_dos_veces()
    {
        var policy = AccountingOperationalPolicy.Create(TenantId, CompanyId, AccountingOperationalMode.Enabled, new DateOnly(2026, 8, 1), null, UserId);

        var before = () => policy.EndBefore(new DateOnly(2026, 8, 1), UserId);
        before.Should().Throw<DomainRuleViolationException>();

        policy.EndBefore(new DateOnly(2026, 9, 1), UserId);
        var twice = () => policy.EndBefore(new DateOnly(2026, 10, 1), UserId);
        twice.Should().Throw<DomainRuleViolationException>();
    }
}
