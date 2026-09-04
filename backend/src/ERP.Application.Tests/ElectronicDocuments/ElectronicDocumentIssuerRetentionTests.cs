using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Xml.Schema;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// RETENTIONS-SRI-AUTHORIZATION-WIRING-04D — confirma que <see cref="ElectronicDocumentIssuer"/>
/// puede procesar <see cref="ElectronicDocumentType.Retention"/> de punta a punta (hasta donde
/// llegan los mocks de firma/envío/autorización, ya cubiertos por sus propios tests) usando
/// <see cref="RetentionElectronicDocumentXmlSupplier"/> + <see cref="ElectronicDocumentXmlSupplierResolver"/>
/// + <see cref="RetentionXmlSchemaValidator"/> real — sin que <c>ElectronicDocumentData</c> ni
/// <c>IElectronicDocumentDataProvider</c>/<c>IElectronicDocumentXmlBuilder</c> comerciales
/// participen en ningún momento.
/// </summary>
public sealed class ElectronicDocumentIssuerRetentionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SourceEntityId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string ValidAccessKey = new('1', 49);

    private static XmlSchemaSet BuildTrivialRetentionSchemaSet()
    {
        const string xsd = """
            <?xml version="1.0" encoding="UTF-8"?>
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <xs:element name="comprobanteRetencion" type="xs:anyType"/>
            </xs:schema>
            """;
        using var reader = System.Xml.XmlReader.Create(new StringReader(xsd));
        var schema = XmlSchema.Read(
            reader,
            (_, e) => throw new InvalidOperationException(e.Message)
        )!;
        var set = new XmlSchemaSet();
        set.Add(schema);
        set.Compile();
        return set;
    }

    private static (
        ElectronicDocumentIssuer Issuer,
        Mock<IRetentionElectronicDocumentXmlService> RetentionXmlService,
        Mock<IElectronicDocumentDataProviderResolver> DataProviderResolver,
        Mock<IElectronicDocumentXmlBuilderResolver> XmlBuilderResolver
    ) BuildIssuer(Mock<IElectronicDocumentAuthorizationService>? authorization = null)
    {
        var xml = new ElectronicDocumentXml(
            Xml: "<comprobanteRetencion/>",
            Encoding: "UTF-8",
            Version: "1.0.0",
            DocumentType: ElectronicDocumentType.Retention,
            Environment: "1",
            AccessKey: ValidAccessKey,
            GeneratedAtUtc: DateTime.UtcNow
        );

        var retentionXmlService = new Mock<IRetentionElectronicDocumentXmlService>();
        retentionXmlService
            .Setup(s =>
                s.GenerateXmlAsync(
                    It.IsAny<ElectronicDocumentSourceReference>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<ElectronicDocumentXml>.Success(xml));

        // Wiring real (no mockeado) para el tramo que esta fase agrega: el resolver de suppliers
        // resuelve Retención por el supplier explícito, nunca por el fallback comercial — los dos
        // resolutores comerciales se mockean devolviendo null para Retention, confirmando que
        // nunca se consultan realmente para ese tipo (ver aserción de Verify más abajo).
        var retentionSupplier = new RetentionElectronicDocumentXmlSupplier(
            retentionXmlService.Object
        );
        var dataProviderResolver = new Mock<IElectronicDocumentDataProviderResolver>();
        var xmlBuilderResolver = new Mock<IElectronicDocumentXmlBuilderResolver>();
        var supplierResolver = new ElectronicDocumentXmlSupplierResolver(
            [retentionSupplier],
            dataProviderResolver.Object,
            xmlBuilderResolver.Object
        );

        // RetentionXmlSchemaValidator real (no mockeado) — confirma que el pipeline realmente lo
        // usa para Retención, con un XSD trivial que solo declara el elemento raíz.
        var schemaProvider = new Mock<IXmlSchemaProvider>();
        schemaProvider
            .Setup(p =>
                p.GetSchemaSetAsync(
                    ElectronicDocumentType.Retention,
                    "1.0.0",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(BuildTrivialRetentionSchemaSet());
        var retentionValidator = new RetentionXmlSchemaValidator(schemaProvider.Object);
        var validatorResolver = new Mock<IElectronicDocumentSchemaValidatorResolver>();
        validatorResolver
            .Setup(r => r.Resolve(ElectronicDocumentType.Retention))
            .Returns(retentionValidator);

        var signedXml = new SignedElectronicDocumentXml(
            "<comprobanteRetencion><ds:Signature/></comprobanteRetencion>",
            "UTF-8",
            "1.0.0",
            ElectronicDocumentType.Retention,
            ValidAccessKey,
            DateTime.UtcNow
        );
        var signingService = new Mock<IElectronicDocumentSigningService>();
        signingService
            .Setup(s =>
                s.SignAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<ElectronicDocumentXml>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<SignedElectronicDocumentXml>.Success(signedXml));

        var storageService = new Mock<IElectronicDocumentXmlStorageService>();
        storageService
            .Setup(s =>
                s.StoreAsync(
                    TenantId,
                    ElectronicDocumentType.Retention,
                    It.IsAny<Guid>(),
                    It.IsAny<ElectronicDocumentXml>(),
                    It.IsAny<SignedElectronicDocumentXml>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<ElectronicDocumentStoredXmlPaths>.Success(
                    new ElectronicDocumentStoredXmlPaths("draft/retention.xml", "signed/retention.xml")
                )
            );

        var repository = new Mock<IElectronicDocumentRepository>();
        repository
            .Setup(r =>
                r.GetBySourceAsync(
                    TenantId,
                    "Retentions",
                    SourceEntityId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ElectronicDocument?)null);
        repository
            .Setup(r => r.AddAsync(It.IsAny<ElectronicDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var reception = new Mock<IElectronicDocumentReceptionService>();
        reception
            .Setup(r => r.SendAsync(CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriReceptionResult>.Success(new SriReceptionResult { Status = "RECIBIDA" })
            );

        authorization ??= UnreachableAuthorization();

        var dbEx = new Mock<IDatabaseExceptionTranslator>();
        DatabaseUniqueViolationInfo? none = null;
        dbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out none)).Returns(false);

        var issuer = new ElectronicDocumentIssuer(
            repository.Object,
            supplierResolver,
            validatorResolver.Object,
            signingService.Object,
            storageService.Object,
            reception.Object,
            authorization.Object,
            new Mock<IFileStorage>().Object,
            dbEx.Object,
            NullLogger<ElectronicDocumentIssuer>.Instance
        );

        return (issuer, retentionXmlService, dataProviderResolver, xmlBuilderResolver);
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

    private static RegisterElectronicDocumentRequest SampleRequest() =>
        new(TenantId, CompanyId, ElectronicDocumentType.Retention, "Retentions", SourceEntityId, UserId);

    [Fact]
    public async Task RegisterAsync_processes_retention_through_the_supplier_without_touching_the_commercial_path()
    {
        var (issuer, retentionXmlService, dataProviderResolver, xmlBuilderResolver) = BuildIssuer();

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Received.ToString());
        retentionXmlService.Verify(
            s =>
                s.GenerateXmlAsync(
                    It.IsAny<ElectronicDocumentSourceReference>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        // El camino comercial (IElectronicDocumentDataProviderResolver/IElectronicDocumentXmlBuilderResolver)
        // nunca se consulta para Retention — el supplier explícito tiene prioridad.
        dataProviderResolver.Verify(
            r => r.Resolve(It.IsAny<ElectronicDocumentType>()),
            Times.Never
        );
        xmlBuilderResolver.Verify(r => r.Resolve(It.IsAny<ElectronicDocumentType>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_can_reach_authorized_for_retention()
    {
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
        var (issuer, _, _, _) = BuildIssuer(authorization);

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Authorized.ToString());
        result.Value.AuthorizationNumber.Should().Be(ValidAccessKey);
    }

    [Fact]
    public async Task RegisterAsync_fails_clearly_when_the_retention_xml_service_fails()
    {
        var (issuer, retentionXmlService, _, _) = BuildIssuer();
        retentionXmlService
            .Setup(s =>
                s.GenerateXmlAsync(
                    It.IsAny<ElectronicDocumentSourceReference>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<ElectronicDocumentXml>.ValidationFailure(
                    "La retención debe estar emitida para generar el documento electrónico."
                )
            );

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("emitida");
    }
}
