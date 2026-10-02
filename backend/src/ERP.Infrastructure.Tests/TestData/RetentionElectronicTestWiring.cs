using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Persistence.Repositories.Retentions;
using ERP.Infrastructure.Persistence.Services;
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
