using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — dobles comunes para probar traductores que registran obligaciones
/// contables durables (<see cref="AccountingPostingRecorder"/>) sin base de datos.
/// </summary>
internal sealed class AccountingContinuityTestHarness
{
    public static readonly Guid UserId = Guid.NewGuid();
    public const string CorrelationId = "corr-continuity-01";

    public Mock<IAccountingOperationalPolicyRepository> Policies { get; } = new();
    public Mock<IAccountingPostingRepository> Postings { get; } = new();

    /// <summary>Sin setup no hay período para la fecha: el gate Closed/Locked no aplica.</summary>
    public Mock<IAccountingPeriodRepository> Periods { get; } = new();
    public AccountingPostingDispatchQueue Queue { get; } = new();
    public List<AccountingPosting> Added { get; } = [];

    public AccountingContinuityTestHarness(
        Guid tenantId,
        Guid companyId,
        AccountingOperationalMode? mode = AccountingOperationalMode.Enabled,
        DateOnly? effectiveFrom = null
    )
    {
        IReadOnlyList<AccountingOperationalPolicy> versions = mode is { } m
            ? [AccountingOperationalPolicy.Create(tenantId, companyId, m, effectiveFrom ?? new DateOnly(2000, 1, 1), null, UserId)]
            : [];
        Policies
            .Setup(p => p.ListAsync(tenantId, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions);
        Postings
            .Setup(p => p.AddAsync(It.IsAny<AccountingPosting>(), It.IsAny<CancellationToken>()))
            .Callback<AccountingPosting, CancellationToken>((p, _) => Added.Add(p))
            .Returns(Task.CompletedTask);
    }

    public AccountingPostingRecorder Recorder =>
        new(
            Policies.Object,
            new AccountingOperationalDecision(Policies.Object),
            Postings.Object,
            Mock.Of<IJournalEntryRepository>(),
            Periods.Object,
            Queue,
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            AuditContext()
        );

    public AccountingPostingPayload SinglePayload() =>
        AccountingPostingPayload.Deserialize(Added.Single().PayloadVersion, Added.Single().Payload)!;

    private static IAuditContext AuditContext()
    {
        var mock = new Mock<IAuditContext>();
        mock.SetupGet(c => c.Actor)
            .Returns(
                new AuditActor(
                    Guid.Empty,
                    UserId,
                    "tester",
                    null,
                    null,
                    null,
                    CorrelationId,
                    null,
                    default
                )
            );
        return mock.Object;
    }
}
