using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// Cubre <see cref="ElectronicDocumentIssuer.RetryAsync"/> — reintento manual/automático de
/// documentos varados en Signed/Received, exhaución hacia DeadLetter y reactivación manual desde
/// DeadLetter. Las etapas previas del pipeline (provider/builder/validador/firma) no se ejercitan
/// aquí — ya están cubiertas por <c>ElectronicDocumentIssuerReceptionTests</c>.
/// </summary>
public sealed class ElectronicDocumentIssuerRetryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string ValidAccessKey = new('1', 49);

    private static ElectronicDocument NewSignedDocument()
    {
        var document = ElectronicDocument.Create(
            TenantId,
            CompanyId,
            ElectronicDocumentType.Invoice,
            "Sales",
            Guid.NewGuid(),
            UserId
        );
        document.MarkXmlGenerated("draft/path.xml", "1.1.0", "1.1.0", UserId);
        document.MarkSigned("signed/path.xml", AccessKey.Create(ValidAccessKey), UserId);
        return document;
    }

    private static ElectronicDocument NewReceivedDocument()
    {
        var document = NewSignedDocument();
        document.MarkSent(UserId);
        document.MarkReceived(UserId);
        return document;
    }

    private static ElectronicDocumentIssuer BuildIssuer(
        Mock<IElectronicDocumentRepository> repository,
        Mock<IElectronicDocumentReceptionService>? reception = null,
        Mock<IElectronicDocumentAuthorizationService>? authorization = null,
        Mock<IElectronicDocumentXmlStorageService>? xmlStorage = null
    )
    {
        repository
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        xmlStorage ??= new Mock<IElectronicDocumentXmlStorageService>();
        reception ??= UnreachableReception();
        authorization ??= UnreachableAuthorization();

        var dbEx = new Mock<IDatabaseExceptionTranslator>();
        DatabaseUniqueViolationInfo? none = null;
        dbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out none)).Returns(false);

        return new ElectronicDocumentIssuer(
            repository.Object,
            new Mock<IElectronicDocumentXmlSupplierResolver>().Object,
            new Mock<IElectronicDocumentSchemaValidatorResolver>().Object,
            new Mock<IElectronicDocumentSigningService>().Object,
            xmlStorage.Object,
            reception.Object,
            authorization.Object,
            dbEx.Object,
            ERP.Application.Tests.TestSupport.RetentionElectronicTestDoubles.NoGuards(),
            Mock.Of<IUnitOfWork>(),
            NullLogger<ElectronicDocumentIssuer>.Instance
        );
    }

    private static Mock<IElectronicDocumentReceptionService> UnreachableReception()
    {
        var mock = new Mock<IElectronicDocumentReceptionService>();
        mock.Setup(r => r.SendAsync(CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriReceptionResult>.Failure(
                    "No se pudo contactar al servicio de recepción del SRI."
                )
            );
        return mock;
    }

    private static Mock<IElectronicDocumentAuthorizationService> UnreachableAuthorization()
    {
        var mock = new Mock<IElectronicDocumentAuthorizationService>();
        mock.Setup(a => a.CheckAsync(CompanyId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriAuthorizationResult>.Failure(
                    "No se pudo contactar al servicio de autorización del SRI."
                )
            );
        return mock;
    }

    private static Mock<IElectronicDocumentRepository> RepositoryReturning(
        ElectronicDocument? document,
        DateTime? receivedAtUtc = null
    )
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        repository
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        repository
            .Setup(r => r.GetFirstReceivedAtUtcAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(receivedAtUtc);
        return repository;
    }

    /// <summary>R11: RECIBIDA más antigua que el plazo de procesamiento del SRI (ventana cerrada).</summary>
    private static DateTime ReceivedBeforeSriWindow() =>
        DateTime.UtcNow - ElectronicDocumentRetryPolicy.SriProcessingWindow - TimeSpan.FromMinutes(1);

    [Fact]
    public async Task RetryAsync_when_document_does_not_exist_returns_not_found()
    {
        var repository = RepositoryReturning(null);
        var issuer = BuildIssuer(repository);

        var result = await issuer.RetryAsync(TenantId, Guid.NewGuid(), UserId);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RetryAsync_when_document_is_authorized_returns_validation_failure()
    {
        var document = NewReceivedDocument();
        document.MarkAuthorized(
            AuthorizationNumber.Create(ValidAccessKey),
            DateTime.UtcNow,
            null,
            UserId
        );
        var repository = RepositoryReturning(document);
        var issuer = BuildIssuer(repository);

        var result = await issuer.RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RetryAsync_from_signed_queries_authorization_and_keeps_it_without_resending()
    {
        // Caso #32: el SRI no respondió al envío; antes de cualquier reenvío se consulta, y un
        // AUTORIZADO se conserva.
        var document = NewSignedDocument();
        var repository = RepositoryReturning(document);
        var reception = new Mock<IElectronicDocumentReceptionService>();
        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization
            .Setup(a => a.CheckAsync(CompanyId, ValidAccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriAuthorizationResult>.Success(
                    new SriAuthorizationResult
                    {
                        Status = "AUTORIZADO",
                        AuthorizationNumber = ValidAccessKey,
                        AuthorizationDate = DateTime.UtcNow,
                    }
                )
            );
        var issuer = BuildIssuer(repository, reception: reception, authorization: authorization);

        var result = await issuer.RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.RetryCount.Should().Be(1);
        document.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        reception.Verify(
            r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task RetryAsync_from_signed_with_inconclusive_query_stays_signed_pending_reconciliation_without_resending()
    {
        var document = NewSignedDocument();
        var repository = RepositoryReturning(document);
        var reception = new Mock<IElectronicDocumentReceptionService>();
        var issuer = BuildIssuer(repository, reception: reception); // autorización inalcanzable

        var result = await issuer.RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.CurrentState.Should().Be(ElectronicDocumentState.Signed);
        document.RequiresSriReconciliation.Should().BeTrue();
        reception.Verify(
            r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task RetryAsync_from_received_retries_authorization_only_without_resending()
    {
        var document = NewReceivedDocument();
        var repository = RepositoryReturning(document);
        var reception = new Mock<IElectronicDocumentReceptionService>();
        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization
            .Setup(a => a.CheckAsync(CompanyId, ValidAccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriAuthorizationResult>.Success(
                    new SriAuthorizationResult
                    {
                        Status = "AUTORIZADO",
                        AuthorizationNumber = ValidAccessKey,
                        AuthorizationDate = DateTime.UtcNow,
                    }
                )
            );
        var issuer = BuildIssuer(repository, reception: reception, authorization: authorization);

        var result = await issuer.RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        reception.Verify(
            r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task RetryAsync_when_max_attempts_reached_without_resolution_moves_to_dead_letter()
    {
        var document = NewReceivedDocument();
        for (var i = 0; i < ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default - 1; i++)
            document.MarkRetryAttempted(UserId);
        var repository = RepositoryReturning(document, ReceivedBeforeSriWindow());
        var issuer = BuildIssuer(repository); // autorización inalcanzable por defecto

        var result = await issuer.RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.RetryCount.Should().Be(ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default);
        document.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
        document.PreDeadLetterState.Should().Be(ElectronicDocumentState.Received);
    }

    [Fact]
    public async Task RetryAsync_on_timeout_stays_received_without_reaching_max_attempts()
    {
        // Un único TIMEOUT nunca deadlettera — solo agotar ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default lo hace.
        var document = NewReceivedDocument();
        var repository = RepositoryReturning(document);
        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization
            .Setup(a => a.CheckAsync(CompanyId, ValidAccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriAuthorizationResult>.Success(
                    new SriAuthorizationResult
                    {
                        Status = "TIMEOUT",
                        ErrorMessage = "El SRI no respondió tras varios reintentos.",
                    }
                )
            );
        var issuer = BuildIssuer(repository, authorization: authorization);

        var result = await issuer.RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.RetryCount.Should().Be(1);
        document.CurrentState.Should().Be(ElectronicDocumentState.Received);
    }

    [Fact]
    public async Task RetryAsync_on_repeated_timeout_moves_to_dead_letter_only_after_max_attempts()
    {
        var document = NewReceivedDocument();
        for (var i = 0; i < ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default - 1; i++)
            document.MarkRetryAttempted(UserId);
        var repository = RepositoryReturning(document, ReceivedBeforeSriWindow());
        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization
            .Setup(a => a.CheckAsync(CompanyId, ValidAccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriAuthorizationResult>.Success(
                    new SriAuthorizationResult
                    {
                        Status = "TIMEOUT",
                        ErrorMessage = "El SRI no respondió tras varios reintentos.",
                    }
                )
            );
        var issuer = BuildIssuer(repository, authorization: authorization);

        var result = await issuer.RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.RetryCount.Should().Be(ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default);
        document.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
        document.PreDeadLetterState.Should().Be(ElectronicDocumentState.Received);
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("7", 7)]
    [InlineData("invalid", 5)]
    public async Task RetryAsync_dead_letter_uses_company_maximum(string raw, int maximum)
    {
        var document = NewReceivedDocument();
        for (var i = 0; i < maximum - 1; i++) document.MarkRetryAttempted(UserId);
        var repository = RepositoryReturning(document, ReceivedBeforeSriWindow());
        repository.Setup(r => r.GetRetryMaximumSettingAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raw);
        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization.Setup(a => a.CheckAsync(CompanyId, ValidAccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SriAuthorizationResult>.Success(new SriAuthorizationResult { Status = "TIMEOUT" }));
        var result = await BuildIssuer(repository, authorization: authorization).RetryAsync(TenantId, document.Id, UserId);
        result.IsSuccess.Should().BeTrue(result.Error);
        document.RetryCount.Should().Be(maximum);
        document.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
        document.LastError.Should().Contain($"{maximum} intentos");
    }

    // ── R11 (auditoría SRI 2.34): consulta hasta el plazo de 24 h del SRI ──

    [Theory]
    [InlineData(ElectronicDocumentState.Received)]
    [InlineData(ElectronicDocumentState.Signed)]
    public async Task RetryAsync_with_sri_outcome_pending_inside_the_24h_window_keeps_querying_past_the_company_maximum(
        ElectronicDocumentState state
    )
    {
        var document = state == ElectronicDocumentState.Received ? NewReceivedDocument() : NewSignedDocument();
        for (var i = 0; i < ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default + 3; i++)
            document.MarkRetryAttempted(UserId);
        var repository = RepositoryReturning(
            document,
            DateTime.UtcNow - ElectronicDocumentRetryPolicy.SriProcessingWindow + TimeSpan.FromMinutes(5)
        );
        var reception = new Mock<IElectronicDocumentReceptionService>(MockBehavior.Strict);
        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization
            .Setup(a => a.CheckAsync(CompanyId, ValidAccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SriAuthorizationResult>.Success(new SriAuthorizationResult { Status = "TIMEOUT" }));

        var result = await BuildIssuer(repository, reception, authorization).RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.CurrentState.Should().Be(state, "un TIMEOUT nunca es rechazo y la ventana sigue abierta");
        document.AccessKey!.Value.Should().Be(ValidAccessKey);
        reception.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RetryAsync_with_sri_outcome_pending_after_the_24h_window_moves_to_dead_letter_requiring_reconciliation()
    {
        var document = NewReceivedDocument();
        for (var i = 0; i < ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default + 3; i++)
            document.MarkRetryAttempted(UserId);
        var repository = RepositoryReturning(document, ReceivedBeforeSriWindow());

        var result = await BuildIssuer(repository).RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
        document.PreDeadLetterState.Should().Be(ElectronicDocumentState.Received);
        document.RequiresSriReconciliation.Should().BeTrue("estado operativo explícito, recuperable por el reintento/conciliación existentes");
        document.LastError.Should().Contain("24 h").And.Contain("conciliación");
        document.AuthorizationNumber.Should().BeNull();
    }

    [Theory]
    [InlineData(ElectronicDocumentState.Signed)]
    [InlineData(ElectronicDocumentState.Received)]
    public async Task RetryAsync_without_reception_evidence_never_dead_letters_by_window(ElectronicDocumentState state)
    {
        // §7.5 cuenta las 24 h desde RECIBIDA. Sin esa evidencia en el timeline (Signed tras un fallo de
        // transporte, o un Received sin registro) el plazo no se puede calcular: la consulta sigue.
        var document = state == ElectronicDocumentState.Received ? NewReceivedDocument() : NewSignedDocument();
        for (var i = 0; i < ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default + 20; i++)
            document.MarkRetryAttempted(UserId);
        var repository = RepositoryReturning(document, receivedAtUtc: null);
        var reception = new Mock<IElectronicDocumentReceptionService>(MockBehavior.Strict);

        await BuildIssuer(repository, reception).RetryAsync(TenantId, document.Id, UserId);

        document.CurrentState.Should().Be(state);
        document.RequiresSriReconciliation.Should().BeTrue();
        reception.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RetryAsync_window_is_counted_from_reception_not_signing()
    {
        // Firmado hace 30 h pero RECIBIDA hace 2 h (p. ej. 70/43/45 tras un reintento manual): ventana abierta.
        var document = NewReceivedDocument();
        for (var i = 0; i < ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default + 3; i++)
            document.MarkRetryAttempted(UserId);
        var repository = RepositoryReturning(document, receivedAtUtc: DateTime.UtcNow.AddHours(-2));

        await BuildIssuer(repository).RetryAsync(TenantId, document.Id, UserId);

        document.CurrentState.Should().Be(ElectronicDocumentState.Received);
    }

    [Theory]
    [InlineData("AUTORIZADO", ElectronicDocumentState.Authorized)]
    [InlineData("NO AUTORIZADO", ElectronicDocumentState.Rejected)]
    public async Task RetryAsync_past_the_company_maximum_inside_the_window_applies_the_sri_decision(
        string sriStatus,
        ElectronicDocumentState expected
    )
    {
        var document = NewReceivedDocument();
        for (var i = 0; i < ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default + 3; i++)
            document.MarkRetryAttempted(UserId);
        var repository = RepositoryReturning(document, DateTime.UtcNow.AddHours(-2));
        var reception = new Mock<IElectronicDocumentReceptionService>(MockBehavior.Strict);
        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization
            .Setup(a => a.CheckAsync(CompanyId, ValidAccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SriAuthorizationResult>.Success(new SriAuthorizationResult
            {
                Status = sriStatus,
                AuthorizationNumber = ValidAccessKey,
                AuthorizationDate = DateTime.UtcNow,
            }));

        var result = await BuildIssuer(repository, reception, authorization).RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.CurrentState.Should().Be(expected);
        document.AccessKey!.Value.Should().Be(ValidAccessKey);
        reception.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RetryAsync_from_dead_letter_reactivates_and_retries()
    {
        var document = NewReceivedDocument();
        document.MarkDeadLetter("Timeout de autorización.", UserId);
        var repository = RepositoryReturning(document);
        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization
            .Setup(a => a.CheckAsync(CompanyId, ValidAccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriAuthorizationResult>.Success(
                    new SriAuthorizationResult
                    {
                        Status = "AUTORIZADO",
                        AuthorizationNumber = ValidAccessKey,
                        AuthorizationDate = DateTime.UtcNow,
                    }
                )
            );
        var issuer = BuildIssuer(repository, authorization: authorization);

        var result = await issuer.RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
    }
}
