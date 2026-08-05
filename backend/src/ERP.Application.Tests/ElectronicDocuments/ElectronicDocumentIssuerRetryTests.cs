using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
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
        Mock<IFileStorage>? fileStorage = null,
        Mock<IElectronicDocumentXmlStorageService>? xmlStorage = null
    )
    {
        repository
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        fileStorage ??= DefaultFileStorage();
        xmlStorage ??= new Mock<IElectronicDocumentXmlStorageService>();
        reception ??= UnreachableReception();
        authorization ??= UnreachableAuthorization();

        var dbEx = new Mock<IDatabaseExceptionTranslator>();
        DatabaseUniqueViolationInfo? none = null;
        dbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out none)).Returns(false);

        return new ElectronicDocumentIssuer(
            repository.Object,
            new Mock<IElectronicDocumentDataProviderResolver>().Object,
            new Mock<IElectronicDocumentXmlBuilderResolver>().Object,
            new Mock<IElectronicDocumentSchemaValidatorResolver>().Object,
            new Mock<IElectronicDocumentSigningService>().Object,
            xmlStorage.Object,
            reception.Object,
            authorization.Object,
            fileStorage.Object,
            dbEx.Object,
            NullLogger<ElectronicDocumentIssuer>.Instance
        );
    }

    private static Mock<IFileStorage> DefaultFileStorage()
    {
        var mock = new Mock<IFileStorage>();
        mock.Setup(f => f.GetAsync("signed/path.xml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
                new MemoryStream(Encoding.UTF8.GetBytes("<factura><ds:Signature/></factura>"))
            );
        return mock;
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
        ElectronicDocument? document
    )
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        repository
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        return repository;
    }

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
    public async Task RetryAsync_from_signed_increments_retry_count_and_reads_stored_signed_xml()
    {
        var document = NewSignedDocument();
        var repository = RepositoryReturning(document);
        byte[]? sentBytes = null;
        var reception = new Mock<IElectronicDocumentReceptionService>();
        reception
            .Setup(r => r.SendAsync(CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, byte[], CancellationToken>((_, bytes, _) => sentBytes = bytes)
            .ReturnsAsync(
                Result<SriReceptionResult>.Success(new SriReceptionResult { Status = "RECIBIDA" })
            );
        var issuer = BuildIssuer(repository, reception: reception);

        var result = await issuer.RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.RetryCount.Should().Be(1);
        document.CurrentState.Should().Be(ElectronicDocumentState.Received);
        sentBytes.Should().NotBeNull();
        Encoding.UTF8.GetString(sentBytes!).Should().Contain("ds:Signature");
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
        for (var i = 0; i < ElectronicDocumentRetryPolicy.MaxAttempts - 1; i++)
            document.MarkRetryAttempted(UserId);
        var repository = RepositoryReturning(document);
        var issuer = BuildIssuer(repository); // autorización inalcanzable por defecto

        var result = await issuer.RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.RetryCount.Should().Be(ElectronicDocumentRetryPolicy.MaxAttempts);
        document.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
        document.PreDeadLetterState.Should().Be(ElectronicDocumentState.Received);
    }

    [Fact]
    public async Task RetryAsync_on_timeout_stays_received_without_reaching_max_attempts()
    {
        // Un único TIMEOUT nunca deadlettera — solo agotar ElectronicDocumentRetryPolicy.MaxAttempts lo hace.
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
        for (var i = 0; i < ElectronicDocumentRetryPolicy.MaxAttempts - 1; i++)
            document.MarkRetryAttempted(UserId);
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
        document.RetryCount.Should().Be(ElectronicDocumentRetryPolicy.MaxAttempts);
        document.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
        document.PreDeadLetterState.Should().Be(ElectronicDocumentState.Received);
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

    [Fact]
    public async Task RetryAsync_when_signed_xml_cannot_be_read_stays_signed_without_throwing()
    {
        var document = NewSignedDocument();
        var repository = RepositoryReturning(document);
        var fileStorage = new Mock<IFileStorage>();
        fileStorage
            .Setup(f => f.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);
        var issuer = BuildIssuer(repository, fileStorage: fileStorage);

        var result = await issuer.RetryAsync(TenantId, document.Id, UserId);

        result.IsSuccess.Should().BeTrue(result.Error);
        document.CurrentState.Should().Be(ElectronicDocumentState.Signed);
    }
}
