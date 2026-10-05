using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Retentions;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Services.Sri;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;

namespace ERP.Infrastructure.Tests.TestData;

/// <summary>
/// ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A — frontera SRI simulada para tests de integración del ciclo
/// electrónico de la retención. Todo lo demás es real (PostgreSQL, gate de ciclo de vida, reclamo
/// Dispatching, anulación del origen). Cuenta las llamadas externas reales (Recepción/Autorización)
/// y permite pausar el pipeline en la firma (antes del reclamo) o en el envío (después del reclamo)
/// para orquestar carreras deterministas.
/// </summary>
public sealed class SriBoundaryDouble
{
    public const string SignedPath = "retentions/signed.xml";

    public Mock<IElectronicDocumentReceptionService> Reception { get; } = new();
    public Mock<IElectronicDocumentAuthorizationService> Authorization { get; } = new();
    public Mock<IElectronicDocumentSigningService> Signing { get; } = new();
    public Mock<IElectronicDocumentXmlSupplierResolver> Suppliers { get; } = new();
    public Mock<IElectronicDocumentSchemaValidatorResolver> Validators { get; } = new();
    public Mock<IElectronicDocumentXmlStorageService> Storage { get; } = new();
    public Mock<IFileStorage> Files { get; } = new();

    /// <summary>Si se fija, SendAsync avisa que entró y espera esta señal (ventana después del reclamo).</summary>
    public TaskCompletionSource? HoldSend { get; set; }
    public TaskCompletionSource SendEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Si se fija, la firma avisa que entró y espera esta señal (ventana ANTES del reclamo).</summary>
    public TaskCompletionSource? HoldSign { get; set; }
    public TaskCompletionSource SignEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Estado que responde la consulta de autorización ("AUTORIZADO", "NO AUTORIZADO", "TIMEOUT").</summary>
    public string AuthorizationStatus { get; set; } = "AUTORIZADO";

    /// <summary>Si true, la recepción falla por transporte (no se sabe si el SRI recibió).</summary>
    public bool ReceptionTransportFailure { get; set; }

    public SriBoundaryDouble(Guid companyId)
    {
        Files
            .Setup(f => f.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
                new MemoryStream(Encoding.UTF8.GetBytes("<comprobanteRetencion><ds:Signature/></comprobanteRetencion>"))
            );

        var supplier = new Mock<IElectronicDocumentXmlSupplier>();
        supplier.SetupGet(s => s.DocumentType).Returns(ElectronicDocumentType.Retention);
        supplier
            .Setup(s => s.BuildXmlAsync(It.IsAny<ElectronicDocumentSourceReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
                Result<ElectronicDocumentXml>.Success(
                    new ElectronicDocumentXml(
                        "<comprobanteRetencion/>",
                        "UTF-8",
                        "2.0.0",
                        ElectronicDocumentType.Retention,
                        "1",
                        NewAccessKey(),
                        DateTime.UtcNow
                    )
                )
            );
        Suppliers.Setup(r => r.Resolve(ElectronicDocumentType.Retention)).Returns(supplier.Object);

        var validator = new Mock<IElectronicDocumentSchemaValidator>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ElectronicDocumentSchemaValidationResult(
                    true,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    "2.0.0",
                    ElectronicDocumentType.Retention
                )
            );
        Validators.Setup(r => r.Resolve(ElectronicDocumentType.Retention)).Returns(validator.Object);

        Signing
            .Setup(s =>
                s.SignAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                async (Guid _, Guid _, ElectronicDocumentXml xml, CancellationToken _) =>
                {
                    SignEntered.TrySetResult();
                    if (HoldSign is not null)
                        await HoldSign.Task;
                    return Result<SignedElectronicDocumentXml>.Success(
                        new SignedElectronicDocumentXml(
                            "<comprobanteRetencion><ds:Signature/></comprobanteRetencion>",
                            "UTF-8",
                            xml.Version,
                            xml.DocumentType,
                            xml.AccessKey,
                            DateTime.UtcNow
                        )
                    );
                }
            );

        Storage
            .Setup(s =>
                s.StoreAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<ElectronicDocumentType>(),
                    It.IsAny<Guid>(),
                    It.IsAny<ElectronicDocumentXml>(),
                    It.IsAny<SignedElectronicDocumentXml>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<ElectronicDocumentStoredXmlPaths>.Success(
                    new ElectronicDocumentStoredXmlPaths("retentions/draft.xml", SignedPath)
                )
            );

        Reception
            .Setup(r => r.SendAsync(companyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                SendEntered.TrySetResult();
                if (HoldSend is not null)
                    await HoldSend.Task;
                return ReceptionTransportFailure
                    ? Result<SriReceptionResult>.Failure("No se pudo contactar al servicio de recepción del SRI.")
                    : Result<SriReceptionResult>.Success(new SriReceptionResult { Status = "RECIBIDA" });
            });

        Authorization
            .Setup(a => a.CheckAsync(companyId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (Guid _, string key, CancellationToken _) =>
                    AuthorizationStatus == "TIMEOUT"
                        ? Result<SriAuthorizationResult>.Failure("El SRI no respondió tras varios reintentos.")
                        : Result<SriAuthorizationResult>.Success(
                            new SriAuthorizationResult
                            {
                                Status = AuthorizationStatus,
                                AuthorizationNumber = key,
                                AuthorizationDate = DateTime.UtcNow,
                                ErrorMessage = AuthorizationStatus == "AUTORIZADO" ? null : "Rechazado (test)",
                            }
                        )
            );
    }

    public int SendCalls => Reception.Invocations.Count(i => i.Method.Name == nameof(IElectronicDocumentReceptionService.SendAsync));
    public int AuthorizationCalls => Authorization.Invocations.Count(i => i.Method.Name == nameof(IElectronicDocumentAuthorizationService.CheckAsync));
    public int SignCalls => Signing.Invocations.Count(i => i.Method.Name == nameof(IElectronicDocumentSigningService.SignAsync));

    public static string NewAccessKey() =>
        string.Concat(Guid.NewGuid().ToByteArray().Select(b => (b % 10).ToString()))
            .PadRight(49, '7')[..49];
}

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01B — ConsultaComprobante simulado a nivel de contrato tipado: responde el
/// estado fiscal configurado (o un fallo de consulta) y cuenta las consultas. Para ejercitar el parser
/// SOAP real usar <see cref="RetentionElectronicTestWiring.SoapStatusQuery"/>.
/// </summary>
public sealed class SriStatusQueryDouble : ISriDocumentStatusQuery
{
    private int _calls;

    public SriStatusQueryOutcome Outcome { get; set; } = SriStatusQueryOutcome.Success;
    public SriFiscalStatus FiscalStatus { get; set; } = SriFiscalStatus.Authorized;
    public int Calls => Volatile.Read(ref _calls);

    public static string Literal(SriFiscalStatus status) =>
        status switch
        {
            SriFiscalStatus.Authorized => "AUTORIZADO",
            SriFiscalStatus.NotAuthorized => "NO AUTORIZADO",
            SriFiscalStatus.PendingAnnulment => "PENDIENTE DE ANULAR",
            SriFiscalStatus.Annulled => "ANULADO",
            _ => "",
        };

    public Task<SriDocumentStatusResult> QueryAsync(string accessKey, string wsdlUrl, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _calls);
        var success = Outcome == SriStatusQueryOutcome.Success;
        return Task.FromResult(
            new SriDocumentStatusResult
            {
                Outcome = Outcome,
                FiscalStatus = success ? FiscalStatus : SriFiscalStatus.Unknown,
                RawAuthorizationStatus = success ? Literal(FiscalStatus) : null,
                RawQueryStatus = Outcome == SriStatusQueryOutcome.Rejected ? "RECHAZADA" : null,
                AccessKey = accessKey,
                Messages = Outcome == SriStatusQueryOutcome.Rejected
                    ? [new SriMessage("99", "ERROR", "ERROR AL CONSULTAR DATOS DEL SERVICIO WEB", "No existen datos para los parámetros ingresados")]
                    : [],
                ErrorMessage = success ? null : $"Consulta fallida (test): {Outcome}",
                RawResponse = success
                    ? $"<EstadoAutorizacionComprobante><claveAcceso>{accessKey}</claveAcceso><estadoAutorizacion>{Literal(FiscalStatus)}</estadoAutorizacion></EstadoAutorizacionComprobante>"
                    : null,
            }
        );
    }
}

/// <summary>HTTP simulado para el <see cref="SriSoapClient"/> REAL (respuesta SOAP literal o excepción de transporte).</summary>
public sealed class SriHttpDouble(Func<HttpResponseMessage> respond) : HttpMessageHandler, IHttpClientFactory
{
    public int Calls { get; private set; }

    public static SriHttpDouble Soap(string body) =>
        new(() => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/xml"),
        });

    public static SriHttpDouble Throwing(Func<Exception> failure) => new(() => throw failure());

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(respond());
    }
}

/// <summary>Cableado real (gate, anulación, emisor, transmisión) sobre un <see cref="ErpDbContext"/> de test.</summary>
public static class RetentionElectronicTestWiring
{
    public static IElectronicDocumentSourceLifecycleGuardResolver Guards(ErpDbContext db, ICurrentCompany company) =>
        new ElectronicDocumentSourceLifecycleGuardResolver(
            new IElectronicDocumentSourceLifecycleGuard[]
            {
                new RetentionElectronicSourceLifecycleGuard(new RetentionDocumentRepository(db, company)),
            }
        );

    public static IElectronicDocumentSourceCancellation Cancellation(ErpDbContext db, ICurrentCompany company) =>
        new ElectronicDocumentSourceCancellation(
            new ElectronicDocumentRepository(db, new CompanyClock(db)),
            Guards(db, company),
            new UnitOfWork(db)
        );

    public static ElectronicDocumentIssuer Issuer(ErpDbContext db, ICurrentCompany company, SriBoundaryDouble sri) =>
        new(
            new ElectronicDocumentRepository(db, new CompanyClock(db)),
            sri.Suppliers.Object,
            sri.Validators.Object,
            sri.Signing.Object,
            sri.Storage.Object,
            sri.Reception.Object,
            sri.Authorization.Object,
            sri.Files.Object,
            new PostgresDatabaseExceptionTranslator(),
            Guards(db, company),
            new UnitOfWork(db),
            NullLogger<ElectronicDocumentIssuer>.Instance
        );

    public static IRetentionElectronicTransmission Transmission(ErpDbContext db, ICurrentCompany company, SriBoundaryDouble sri) =>
        new RetentionElectronicTransmission(
            Issuer(db, company, sri),
            NullLogger<RetentionElectronicTransmission>.Instance
        );

    /// <summary>ZH-RETENTION-SRI-ANNULMENT-01 — registro real de la solicitud (lo usa la anulación del origen).</summary>
    public static IRetentionAnnulmentRequester Requester(ErpDbContext db, ICurrentCompany company) =>
        new RetentionAnnulmentRequester(
            new RetentionAnnulmentRequestRepository(db),
            SriAnnulment(db, company),
            new AccountsPayableRepository(db),
            new BusinessPartnerRepository(db),
            NullLogger<RetentionAnnulmentRequester>.Instance
        );

    public static IElectronicDocumentSriAnnulment SriAnnulment(ErpDbContext db, ICurrentCompany company) =>
        new ElectronicDocumentSriAnnulment(
            new ElectronicDocumentRepository(db, new CompanyClock(db)),
            Guards(db, company),
            new UnitOfWork(db)
        );

    public const string TestWsdlUrl =
        "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl";

    /// <summary>
    /// ZH-RETENTION-SRI-ANNULMENT-01/01B — ciclo real (presentación, verificación en ConsultaComprobante,
    /// finalización por el flujo del origen). Solo la consulta SRI es un doble (<paramref name="sriStatus"/>).
    /// </summary>
    public static IRetentionAnnulmentService AnnulmentService(
        ErpDbContext db,
        ICurrentCompany company,
        ISriDocumentStatusQuery sriStatus,
        params IRetentionOriginCancellation[] origins
    ) =>
        new RetentionAnnulmentService(
            new RetentionAnnulmentRequestRepository(db),
            new RetentionDocumentRepository(db, company),
            SriAnnulment(db, company),
            new AccountsPayableRepository(db),
            new UnitOfWork(db),
            origins,
            sriStatus,
            SriSettings(company.CompanyId),
            new CompanyClock(db),
            NullLogger<RetentionAnnulmentService>.Instance
        );

    /// <summary>ConsultaComprobante por el <see cref="SriSoapClient"/> REAL (envelope, transporte, parser) sobre HTTP simulado.</summary>
    public static ISriDocumentStatusQuery SoapStatusQuery(SriHttpDouble http) =>
        new SriDocumentStatusQuery(new SriSoapClient(http, NullLogger<SriSoapClient>.Instance));

    private static ISriSettingsRepository SriSettings(Guid companyId)
    {
        var settings = ERP.Domain.Configuration.Entities.SriSettings.Create(
            Guid.NewGuid(), companyId, 1, 1, TestWsdlUrl, Guid.NewGuid());
        var mock = new Mock<ISriSettingsRepository>();
        mock.Setup(r => r.GetByCompanyIdAsync(companyId, It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        return mock.Object;
    }

    /// <summary>Transmisión inerte, para tests que no tratan sobre el ciclo electrónico.</summary>
    public static IRetentionElectronicTransmission NoOpTransmission()
    {
        var mock = new Mock<IRetentionElectronicTransmission>();
        mock.Setup(m =>
                m.StartAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result<ElectronicDocumentDto>.Failure("no-op (test)"));
        return mock.Object;
    }

    /// <summary>Autorizador que concede exactamente los permisos indicados.</summary>
    public static IRuntimePermissionAuthorizer Granting(params string[] permissions)
    {
        var mock = new Mock<IRuntimePermissionAuthorizer>();
        mock.Setup(a =>
                a.IsAuthorizedAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((string key, Guid _, string _, CancellationToken _) => permissions.Contains(key));
        return mock.Object;
    }
}
