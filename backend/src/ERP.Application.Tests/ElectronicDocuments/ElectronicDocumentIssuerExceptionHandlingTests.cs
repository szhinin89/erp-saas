using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// A4 (auditoría de robustez): una excepción real (no un Result.Failure esperado) lanzada por
/// cualquier dependencia del pipeline no debe propagarse sin control — debe manejarse igual que
/// cualquier otro fallo de esa misma etapa, sin dejar el documento en un estado sin rastro.
/// A3 (auditoría de robustez): una violación de unicidad real (dos RegisterAsync concurrentes
/// para el mismo origen) debe traducirse a Result.Conflict, nunca propagarse como excepción sin
/// control (que ExceptionMiddleware mapearía a 503 en vez de 409).
/// </summary>
public sealed class ElectronicDocumentIssuerExceptionHandlingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SourceEntityId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string ValidAccessKey = new('1', 49);

    private static ElectronicDocumentData SampleData() => new(
        Emission: new ElectronicDocumentEmissionContext("2", "1", "01", "001", "Dirección", "001", "000000001", DateTime.UtcNow),
        Issuer: new ElectronicDocumentIssuerData("1792146739001", "Empresa Prueba", null, "Matriz", null, false),
        Counterparty: new ElectronicDocumentCounterpartyData("05", "1713328506", "Cliente Prueba", null, null),
        Details: [],
        TaxSummary: [],
        Totals: new ElectronicDocumentTotals(0, 0, 0, 0, "USD"),
        Payments: [],
        AdditionalInfo: []);

    private static RegisterElectronicDocumentRequest SampleRequest() => new(
        TenantId, CompanyId, ElectronicDocumentType.Invoice, "Sales", SourceEntityId, UserId);

    private static (ElectronicDocumentIssuer Issuer, Mock<IElectronicDocumentRepository> Repository, Mock<IElectronicDocumentSigningService> Signing, Mock<IElectronicDocumentReceptionService> Reception, Mock<IDatabaseExceptionTranslator> DbEx)
        BuildIssuer()
    {
        var providerMock = new Mock<IElectronicDocumentDataProvider>();
        providerMock.Setup(p => p.DocumentType).Returns(ElectronicDocumentType.Invoice);
        providerMock.Setup(p => p.GetDataAsync(It.IsAny<ElectronicDocumentSourceReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ElectronicDocumentData>.Success(SampleData()));

        var providerResolver = new Mock<IElectronicDocumentDataProviderResolver>();
        providerResolver.Setup(r => r.Resolve(ElectronicDocumentType.Invoice)).Returns(providerMock.Object);

        var xml = new ElectronicDocumentXml("<factura/>", "UTF-8", "1.1.0", ElectronicDocumentType.Invoice, ValidAccessKey, DateTime.UtcNow);
        var builderMock = new Mock<IElectronicDocumentXmlBuilder>();
        builderMock.Setup(b => b.DocumentType).Returns(ElectronicDocumentType.Invoice);
        builderMock.Setup(b => b.Build(It.IsAny<ElectronicDocumentData>())).Returns(Result<ElectronicDocumentXml>.Success(xml));

        var builderResolver = new Mock<IElectronicDocumentXmlBuilderResolver>();
        builderResolver.Setup(r => r.Resolve(ElectronicDocumentType.Invoice)).Returns(builderMock.Object);

        var validatorMock = new Mock<IElectronicDocumentSchemaValidator>();
        validatorMock.Setup(v => v.DocumentType).Returns(ElectronicDocumentType.Invoice);
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ElectronicDocumentSchemaValidationResult(true, [], [], "1.1.0", ElectronicDocumentType.Invoice));

        var validatorResolver = new Mock<IElectronicDocumentSchemaValidatorResolver>();
        validatorResolver.Setup(r => r.Resolve(ElectronicDocumentType.Invoice)).Returns(validatorMock.Object);

        var signedXml = new SignedElectronicDocumentXml("<factura><ds:Signature/></factura>", "UTF-8", "1.1.0", ElectronicDocumentType.Invoice, ValidAccessKey, DateTime.UtcNow);
        var signingService = new Mock<IElectronicDocumentSigningService>();
        signingService.Setup(s => s.SignAsync(TenantId, CompanyId, It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SignedElectronicDocumentXml>.Success(signedXml));

        var storageService = new Mock<IElectronicDocumentXmlStorageService>();
        storageService.Setup(s => s.StoreAsync(
                TenantId, ElectronicDocumentType.Invoice, It.IsAny<Guid>(),
                It.IsAny<ElectronicDocumentXml>(), It.IsAny<SignedElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ElectronicDocumentStoredXmlPaths>.Success(
                new ElectronicDocumentStoredXmlPaths("draft/path.xml", "signed/path.xml")));

        var repository = new Mock<IElectronicDocumentRepository>();
        repository.Setup(r => r.GetBySourceAsync(TenantId, "Sales", SourceEntityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ElectronicDocument?)null);
        repository.Setup(r => r.AddAsync(It.IsAny<ElectronicDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var reception = new Mock<IElectronicDocumentReceptionService>();
        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization.Setup(a => a.CheckAsync(CompanyId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SriAuthorizationResult>.Failure("No se pudo contactar al servicio de autorización del SRI."));

        var dbEx = new Mock<IDatabaseExceptionTranslator>();
        DatabaseUniqueViolationInfo? none = null;
        dbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out none)).Returns(false);

        var issuer = new ElectronicDocumentIssuer(
            repository.Object, providerResolver.Object, builderResolver.Object,
            validatorResolver.Object, signingService.Object, storageService.Object,
            reception.Object, authorization.Object, new Mock<IFileStorage>().Object,
            dbEx.Object, NullLogger<ElectronicDocumentIssuer>.Instance);

        return (issuer, repository, signingService, reception, dbEx);
    }

    [Fact]
    public async Task RegisterAsync_when_signing_throws_marks_document_failed_instead_of_propagating()
    {
        var (issuer, _, signing, _, _) = BuildIssuer();
        signing.Setup(s => s.SignAsync(TenantId, CompanyId, It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("El certificado no contiene clave privada RSA."));

        var act = async () => await issuer.RegisterAsync(SampleRequest());

        var result = await act.Should().NotThrowAsync();
        result.Subject.IsSuccess.Should().BeFalse();
        result.Subject.Value?.CurrentState.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_when_signing_throws_persists_failed_state_with_reason_and_increments_retry_count()
    {
        var (issuer, repository, signing, _, _) = BuildIssuer();
        signing.Setup(s => s.SignAsync(TenantId, CompanyId, It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("El certificado no contiene clave privada RSA."));

        ElectronicDocument? added = null;
        repository.Setup(r => r.AddAsync(It.IsAny<ElectronicDocument>(), It.IsAny<CancellationToken>()))
            .Callback<ElectronicDocument, CancellationToken>((d, _) => added = d)
            .Returns(Task.CompletedTask);

        await issuer.RegisterAsync(SampleRequest());

        added.Should().NotBeNull();
        added!.CurrentState.Should().Be(ElectronicDocumentState.Failed);
        added.RetryCount.Should().Be(1);
        added.LastError.Should().Contain("El certificado no contiene clave privada RSA");
    }

    [Fact]
    public async Task RegisterAsync_when_reception_throws_after_signing_does_not_propagate_and_stays_signed()
    {
        var (issuer, repository, _, reception, _) = BuildIssuer();
        reception.Setup(r => r.SendAsync(CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Conexión rechazada por el servidor SRI."));

        ElectronicDocument? added = null;
        repository.Setup(r => r.AddAsync(It.IsAny<ElectronicDocument>(), It.IsAny<CancellationToken>()))
            .Callback<ElectronicDocument, CancellationToken>((d, _) => added = d)
            .Returns(Task.CompletedTask);

        var act = async () => await issuer.RegisterAsync(SampleRequest());

        var result = await act.Should().NotThrowAsync();
        result.Subject.IsSuccess.Should().BeTrue(result.Subject.Error);
        added.Should().NotBeNull();
        added!.CurrentState.Should().Be(ElectronicDocumentState.Signed);
    }

    [Fact]
    public async Task RegisterAsync_when_concurrent_insert_violates_unique_source_returns_conflict_instead_of_propagating()
    {
        // A3: dos RegisterAsync concurrentes para el mismo origen — ambos pasan GetBySourceAsync
        // (ven null), el índice único uq_electronic_document_source es la barrera real. El
        // perdedor debe recibir Result.Conflict, nunca una excepción sin control.
        var (issuer, repository, _, _, dbEx) = BuildIssuer();
        var violation = new DatabaseUniqueViolationInfo("23505", "uq_electronic_document_source", "electronic_documents", null);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated DbUpdateException"));
        dbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out violation)).Returns(true);

        var act = async () => await issuer.RegisterAsync(SampleRequest());

        var result = await act.Should().NotThrowAsync();
        result.Subject.IsSuccess.Should().BeFalse();
        result.Subject.Code.Should().Be("CONFLICT");
    }
}
