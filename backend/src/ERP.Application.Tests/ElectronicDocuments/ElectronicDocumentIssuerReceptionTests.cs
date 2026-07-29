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
/// Fases 8-9 — cubre exclusivamente la extensión de <see cref="ElectronicDocumentIssuer.RegisterAsync"/>
/// que envía el documento ya firmado a Recepción del SRI (Fase 8: Sent→Received/Rejected) y,
/// una vez Received, consulta su autorización (Fase 9: Received→Authorized/Rejected/DeadLetter).
/// Las etapas anteriores (provider, builder, validador, firma, storage) se mockean con
/// comportamiento "éxito trivial" — ya están cubiertas por sus propios tests unitarios
/// (InvoiceXmlBuilderTests, InvoiceXmlSchemaValidatorTests, ElectronicDocumentSigningServiceTests,
/// ElectronicDocumentXmlStorageServiceTests).
/// </summary>
public sealed class ElectronicDocumentIssuerReceptionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SourceEntityId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string ValidAccessKey = new('1', 49);

    /// <summary>Autorización que nunca obtiene una respuesta definitiva — usada por los tests
    /// centrados en recepción (Fase 8) para que el documento se quede en Received sin más
    /// transiciones, exactamente como antes de que existiera la consulta de autorización.</summary>
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

    private static ElectronicDocumentIssuer BuildIssuer(
        Mock<IElectronicDocumentRepository> repository,
        Mock<IElectronicDocumentReceptionService> reception,
        Mock<IElectronicDocumentAuthorizationService> authorization,
        Mock<IElectronicDocumentXmlStorageService>? storageServiceOverride = null
    )
    {
        var providerMock = new Mock<IElectronicDocumentDataProvider>();
        providerMock.Setup(p => p.DocumentType).Returns(ElectronicDocumentType.Invoice);
        providerMock
            .Setup(p =>
                p.GetDataAsync(
                    It.IsAny<ElectronicDocumentSourceReference>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<ElectronicDocumentData>.Success(SampleData()));

        var providerResolver = new Mock<IElectronicDocumentDataProviderResolver>();
        providerResolver
            .Setup(r => r.Resolve(ElectronicDocumentType.Invoice))
            .Returns(providerMock.Object);

        var xml = new ElectronicDocumentXml(
            "<factura/>",
            "UTF-8",
            "1.1.0",
            ElectronicDocumentType.Invoice,
            ValidAccessKey,
            DateTime.UtcNow
        );
        var builderMock = new Mock<IElectronicDocumentXmlBuilder>();
        builderMock.Setup(b => b.DocumentType).Returns(ElectronicDocumentType.Invoice);
        builderMock
            .Setup(b => b.Build(It.IsAny<ElectronicDocumentData>()))
            .Returns(Result<ElectronicDocumentXml>.Success(xml));

        var builderResolver = new Mock<IElectronicDocumentXmlBuilderResolver>();
        builderResolver
            .Setup(r => r.Resolve(ElectronicDocumentType.Invoice))
            .Returns(builderMock.Object);

        var validatorMock = new Mock<IElectronicDocumentSchemaValidator>();
        validatorMock.Setup(v => v.DocumentType).Returns(ElectronicDocumentType.Invoice);
        validatorMock
            .Setup(v =>
                v.ValidateAsync(It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new ElectronicDocumentSchemaValidationResult(
                    true,
                    [],
                    [],
                    "1.1.0",
                    ElectronicDocumentType.Invoice
                )
            );

        var validatorResolver = new Mock<IElectronicDocumentSchemaValidatorResolver>();
        validatorResolver
            .Setup(r => r.Resolve(ElectronicDocumentType.Invoice))
            .Returns(validatorMock.Object);

        var signedXml = new SignedElectronicDocumentXml(
            "<factura><ds:Signature/></factura>",
            "UTF-8",
            "1.1.0",
            ElectronicDocumentType.Invoice,
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

        var storageService =
            storageServiceOverride ?? new Mock<IElectronicDocumentXmlStorageService>();
        storageService
            .Setup(s =>
                s.StoreAsync(
                    TenantId,
                    ElectronicDocumentType.Invoice,
                    It.IsAny<Guid>(),
                    It.IsAny<ElectronicDocumentXml>(),
                    It.IsAny<SignedElectronicDocumentXml>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<ElectronicDocumentStoredXmlPaths>.Success(
                    new ElectronicDocumentStoredXmlPaths("draft/path.xml", "signed/path.xml")
                )
            );

        repository
            .Setup(r =>
                r.GetBySourceAsync(TenantId, "Sales", SourceEntityId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((ElectronicDocument?)null);
        repository
            .Setup(r => r.AddAsync(It.IsAny<ElectronicDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dbEx = new Mock<IDatabaseExceptionTranslator>();
        DatabaseUniqueViolationInfo? none = null;
        dbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out none)).Returns(false);

        return new ElectronicDocumentIssuer(
            repository.Object,
            providerResolver.Object,
            builderResolver.Object,
            validatorResolver.Object,
            signingService.Object,
            storageService.Object,
            reception.Object,
            authorization.Object,
            new Mock<IFileStorage>().Object,
            dbEx.Object,
            NullLogger<ElectronicDocumentIssuer>.Instance
        );
    }

    private static ElectronicDocumentData SampleData() =>
        new(
            Emission: new ElectronicDocumentEmissionContext(
                "2",
                "1",
                "01",
                "001",
                "Dirección",
                "001",
                "000000001",
                DateTime.UtcNow
            ),
            Issuer: new ElectronicDocumentIssuerData(
                "1792146739001",
                "Empresa Prueba",
                null,
                "Matriz",
                null,
                false
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                "05",
                "1713328506",
                "Cliente Prueba",
                null,
                null
            ),
            Details: [],
            TaxSummary: [],
            Totals: new ElectronicDocumentTotals(0, 0, 0, 0, "USD"),
            Payments: [],
            AdditionalInfo: []
        );

    private static RegisterElectronicDocumentRequest SampleRequest() =>
        new(TenantId, CompanyId, ElectronicDocumentType.Invoice, "Sales", SourceEntityId, UserId);

    private static Mock<IElectronicDocumentReceptionService> ReceptionReturning(
        string status,
        params string[] errors
    )
    {
        var reception = new Mock<IElectronicDocumentReceptionService>();
        reception
            .Setup(r => r.SendAsync(CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriReceptionResult>.Success(
                    new SriReceptionResult { Status = status, Errors = errors }
                )
            );
        return reception;
    }

    // ── Fase 8: recepción ────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_when_sri_responds_recibida_transitions_to_received()
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        var reception = ReceptionReturning("RECIBIDA");
        var issuer = BuildIssuer(repository, reception, UnreachableAuthorization());

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Received.ToString());
        // Draft (persistido antes del pipeline) + Signed + Received — la consulta de autorización
        // falla (transporte) y no agrega un cuarto checkpoint.
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task RegisterAsync_when_sri_responds_devuelta_transitions_to_rejected_with_reason()
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        var reception = ReceptionReturning(
            "DEVUELTA",
            "[35] DOCUMENTO INVÁLIDO: estructura incorrecta."
        );
        var issuer = BuildIssuer(repository, reception, UnreachableAuthorization());

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Rejected.ToString());
    }

    [Fact]
    public async Task RegisterAsync_when_sri_reports_processing_code_70_does_not_reject_and_proceeds_to_authorization()
    {
        // SOAP-02 (auditoría SRI, Fase 2): código 70 "Clave acceso en procesamiento" no es un
        // rechazo — el SRI ya tiene un envío anterior de este comprobante en cola. La ficha
        // técnica exige no reenviar y esperar la autorización/rechazo real (máx. 24h); el
        // comportamiento correcto es tratarlo como recibido y seguir el pipeline normal hacia
        // la consulta de autorización, no marcarlo Rejected.
        var repository = new Mock<IElectronicDocumentRepository>();
        var reception = ReceptionReturning(
            "DEVUELTA",
            "[70] Clave de acceso en procesamiento: comprobante ya enviado."
        );

        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization
            .Setup(a => a.CheckAsync(CompanyId, ValidAccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriAuthorizationResult>.Success(
                    new SriAuthorizationResult { Status = "AUTORIZADO" }
                )
            );

        var issuer = BuildIssuer(repository, reception, authorization);

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Authorized.ToString());
    }

    [Theory]
    [InlineData("[43] Clave acceso registrada: comprobante ya existe.")]
    [InlineData("[45] Secuencial registrado: comprobante ya existe.")]
    public async Task RegisterAsync_when_sri_reports_already_registered_codes_does_not_reject_and_proceeds_to_authorization(
        string alreadyExistsError
    )
    {
        // RESP-01 (cierre ElectronicDocuments v1.0): igual que el código 70, los códigos 43
        // ("Clave acceso registrada") y 45 ("Secuencial registrado") indican que el SRI ya tiene
        // un envío anterior de esta clave de acceso — nunca un rechazo real. Antes de este fix
        // solo el 70 evitaba el MarkRejected automático; 43/45 caían en el rechazo genérico.
        var repository = new Mock<IElectronicDocumentRepository>();
        var reception = ReceptionReturning("DEVUELTA", alreadyExistsError);

        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization
            .Setup(a => a.CheckAsync(CompanyId, ValidAccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriAuthorizationResult>.Success(
                    new SriAuthorizationResult { Status = "AUTORIZADO" }
                )
            );

        var issuer = BuildIssuer(repository, reception, authorization);

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Authorized.ToString());
    }

    [Fact]
    public async Task RegisterAsync_when_sri_connection_fails_document_stays_signed()
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        var reception = new Mock<IElectronicDocumentReceptionService>();
        reception
            .Setup(r => r.SendAsync(CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriReceptionResult>.Failure(
                    "No se pudo contactar al servicio de recepción del SRI."
                )
            );
        var issuer = BuildIssuer(repository, reception, UnreachableAuthorization());

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Signed.ToString());
        // Draft (persistido antes del pipeline) + checkpoint de persistencia tras la firma —
        // nunca un tercer SaveChanges si el envío ni siquiera pudo completarse (no hay
        // transición que persistir).
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RegisterAsync_passes_signed_xml_bytes_to_reception_service()
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        var reception = new Mock<IElectronicDocumentReceptionService>();
        byte[]? capturedBytes = null;
        reception
            .Setup(r => r.SendAsync(CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, byte[], CancellationToken>((_, bytes, _) => capturedBytes = bytes)
            .ReturnsAsync(
                Result<SriReceptionResult>.Success(new SriReceptionResult { Status = "RECIBIDA" })
            );

        var issuer = BuildIssuer(repository, reception, UnreachableAuthorization());
        await issuer.RegisterAsync(SampleRequest());

        capturedBytes.Should().NotBeNull();
        System.Text.Encoding.UTF8.GetString(capturedBytes!).Should().Contain("ds:Signature");
    }

    // ── Fase 9: autorización ─────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_when_sri_authorizes_transitions_to_authorized_and_stores_xml()
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        var reception = ReceptionReturning("RECIBIDA");

        var authDate = DateTime.UtcNow;
        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization
            .Setup(a => a.CheckAsync(CompanyId, ValidAccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriAuthorizationResult>.Success(
                    new SriAuthorizationResult
                    {
                        Status = "AUTORIZADO",
                        AuthorizationNumber = ValidAccessKey,
                        AuthorizationDate = authDate,
                        DocumentXml = "<factura><ds:Signature/></factura>",
                    }
                )
            );

        var storageService = new Mock<IElectronicDocumentXmlStorageService>();
        storageService
            .Setup(s =>
                s.StoreAuthorizedAsync(
                    TenantId,
                    ElectronicDocumentType.Invoice,
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<string>.Success("authorized/path.xml"));

        var issuer = BuildIssuer(repository, reception, authorization, storageService);

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Authorized.ToString());
        result.Value.AuthorizationNumber.Should().Be(ValidAccessKey);
        result.Value.AuthorizationDate.Should().Be(authDate);
        // Draft (persistido antes del pipeline) + Signed + Received + Authorized.
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(4));
        storageService.Verify(
            s =>
                s.StoreAuthorizedAsync(
                    TenantId,
                    ElectronicDocumentType.Invoice,
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RegisterAsync_when_sri_reports_a_mismatched_authorization_number_still_authorizes_using_the_access_key()
    {
        // AUTH-01 (auditoría SRI, Fase 2): en offline el número de autorización ES la clave de
        // acceso (ficha técnica sección 5.9) — si el SOAP llegara a reportar un valor distinto,
        // no se confía ciegamente en él: el documento se autoriza igual (la autorización ocurrió
        // realmente en el SRI) pero el valor persistido es siempre la clave de acceso del propio
        // documento, la única fuente de verdad definida por la ficha técnica para este campo.
        var repository = new Mock<IElectronicDocumentRepository>();
        var reception = ReceptionReturning("RECIBIDA");

        var mismatchedNumber = new string('9', 49);
        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization
            .Setup(a => a.CheckAsync(CompanyId, ValidAccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriAuthorizationResult>.Success(
                    new SriAuthorizationResult
                    {
                        Status = "AUTORIZADO",
                        AuthorizationNumber = mismatchedNumber,
                    }
                )
            );

        var issuer = BuildIssuer(repository, reception, authorization);

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Authorized.ToString());
        result
            .Value.AuthorizationNumber.Should()
            .Be(
                ValidAccessKey,
                "la clave de acceso es la única fuente de verdad del número de autorización en offline, nunca el valor reportado por el SOAP si difiere"
            );
    }

    [Fact]
    public async Task RegisterAsync_when_sri_authorizes_without_document_xml_still_authorizes_without_storing()
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        var reception = ReceptionReturning("RECIBIDA");

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
                        DocumentXml = null,
                    }
                )
            );

        var storageService = new Mock<IElectronicDocumentXmlStorageService>();
        var issuer = BuildIssuer(repository, reception, authorization, storageService);

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Authorized.ToString());
        storageService.Verify(
            s =>
                s.StoreAuthorizedAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<ElectronicDocumentType>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RegisterAsync_when_sri_does_not_authorize_transitions_to_rejected()
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        var reception = ReceptionReturning("RECIBIDA");

        var authorization = new Mock<IElectronicDocumentAuthorizationService>();
        authorization
            .Setup(a => a.CheckAsync(CompanyId, ValidAccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<SriAuthorizationResult>.Success(
                    new SriAuthorizationResult
                    {
                        Status = "NO AUTORIZADO",
                        ErrorMessage = "[46] RUC no existe.",
                    }
                )
            );

        var issuer = BuildIssuer(repository, reception, authorization);

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Rejected.ToString());
        // Draft (persistido antes del pipeline) + Signed + Received + Rejected.
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public async Task RegisterAsync_when_sri_authorization_times_out_stays_received_for_retry()
    {
        // TIMEOUT no es una decisión del SRI (el WS sigue procesando) — nunca debe deadletterar
        // en el primer intento. Solo ApplyDeadLetterIfExhaustedAsync puede escalar a DeadLetter,
        // y solo tras agotar ElectronicDocumentRetryPolicy.MaxAttempts.
        var repository = new Mock<IElectronicDocumentRepository>();
        var reception = ReceptionReturning("RECIBIDA");

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

        var issuer = BuildIssuer(repository, reception, authorization);

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Received.ToString());
    }

    [Fact]
    public async Task RegisterAsync_when_authorization_check_fails_document_stays_received()
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        var reception = ReceptionReturning("RECIBIDA");
        var issuer = BuildIssuer(repository, reception, UnreachableAuthorization());

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Received.ToString());
    }

    [Fact]
    public async Task RegisterAsync_when_authorization_storage_fails_still_authorizes_without_xml_path()
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        var reception = ReceptionReturning("RECIBIDA");

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
                        DocumentXml = "<factura/>",
                    }
                )
            );

        var storageService = new Mock<IElectronicDocumentXmlStorageService>();
        storageService
            .Setup(s =>
                s.StoreAuthorizedAsync(
                    TenantId,
                    ElectronicDocumentType.Invoice,
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<string>.Failure("Disco lleno."));

        var issuer = BuildIssuer(repository, reception, authorization, storageService);

        var result = await issuer.RegisterAsync(SampleRequest());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentState.Should().Be(ElectronicDocumentState.Authorized.ToString());
    }
}
