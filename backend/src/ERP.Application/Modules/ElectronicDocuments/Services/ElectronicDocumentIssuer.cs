using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

public sealed partial class ElectronicDocumentIssuer : IElectronicDocumentIssuer
{
    private readonly IElectronicDocumentRepository _repository;
    private readonly IElectronicDocumentDataProviderResolver _providerResolver;
    private readonly IElectronicDocumentXmlBuilderResolver _xmlBuilderResolver;
    private readonly IElectronicDocumentSchemaValidatorResolver _schemaValidatorResolver;
    private readonly IElectronicDocumentSigningService _signingService;
    private readonly IElectronicDocumentXmlStorageService _xmlStorageService;
    private readonly IElectronicDocumentReceptionService _receptionService;
    private readonly IElectronicDocumentAuthorizationService _authorizationService;
    private readonly IFileStorage _fileStorage;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ILogger<ElectronicDocumentIssuer> _logger;

    public ElectronicDocumentIssuer(
        IElectronicDocumentRepository repository,
        IElectronicDocumentDataProviderResolver providerResolver,
        IElectronicDocumentXmlBuilderResolver xmlBuilderResolver,
        IElectronicDocumentSchemaValidatorResolver schemaValidatorResolver,
        IElectronicDocumentSigningService signingService,
        IElectronicDocumentXmlStorageService xmlStorageService,
        IElectronicDocumentReceptionService receptionService,
        IElectronicDocumentAuthorizationService authorizationService,
        IFileStorage fileStorage,
        IDatabaseExceptionTranslator dbEx,
        ILogger<ElectronicDocumentIssuer> logger
    )
    {
        _repository = repository;
        _providerResolver = providerResolver;
        _xmlBuilderResolver = xmlBuilderResolver;
        _schemaValidatorResolver = schemaValidatorResolver;
        _signingService = signingService;
        _xmlStorageService = xmlStorageService;
        _receptionService = receptionService;
        _authorizationService = authorizationService;
        _fileStorage = fileStorage;
        _dbEx = dbEx;
        _logger = logger;
    }

    public async Task<Result<ElectronicDocumentDto>> RegisterAsync(
        RegisterElectronicDocumentRequest request,
        CancellationToken ct = default
    )
    {
        LogRegisterStarted(
            request.TenantId,
            request.CompanyId,
            request.DocumentType,
            request.SourceModule,
            request.SourceEntityId
        );

        var existing = await _repository.GetBySourceAsync(
            request.TenantId,
            request.SourceModule,
            request.SourceEntityId,
            ct
        );
        if (existing is not null)
        {
            // Draft/Failed nunca llegaron a ningún estado terminal — se reanuda el pipeline
            // sobre esa misma fila en vez de bloquear con "ya existe" (ver invariante en
            // ElectronicDocument: la fila se crea ANTES de correr el pipeline).
            if (
                existing.CurrentState
                is ElectronicDocumentState.Draft
                    or ElectronicDocumentState.Failed
            )
                return await RunPipelineAsync(existing, request, ct);

            LogRegisterConflict(request.SourceModule, request.SourceEntityId, existing.Id);
            return Result<ElectronicDocumentDto>.Conflict(
                "Ya existe un documento electrónico registrado para este documento de origen."
            );
        }

        // El documento se persiste en Draft ANTES de correr el pipeline riesgoso — así ninguna
        // factura de punto de emisión electrónico queda invisible en el Monitor si algo falla.
        var document = ElectronicDocument.Create(
            tenantId: request.TenantId,
            companyId: request.CompanyId,
            documentType: request.DocumentType,
            sourceModule: request.SourceModule,
            sourceEntityId: request.SourceEntityId,
            createdBy: request.UserId
        );

        await _repository.AddAsync(document, ct);
        try
        {
            await _repository.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            // A3 (auditoría de robustez): dos RegisterAsync concurrentes para el mismo origen
            // (SourceModule/SourceEntityId) pueden pasar ambos el chequeo GetBySourceAsync — el
            // índice único uq_electronic_document_source ya actúa como barrera atómica real (el
            // perdedor nunca llega a RunPipelineAsync, nunca consume secuencia SRI ni firma XML).
            // Sin este catch, la excepción se propagaba sin control y ExceptionMiddleware la
            // mapeaba a 503 "DatabaseUnavailable" — engañoso para una carrera de negocio legítima.
            LogRegisterConflict(request.SourceModule, request.SourceEntityId, document.Id);
            return Result<ElectronicDocumentDto>.Conflict(
                "Ya existe un documento electrónico registrado para este documento de origen."
            );
        }

        return await RunPipelineAsync(document, request, ct);
    }

    /// <summary>
    /// Corre el pipeline completo (Provider→XML→XSD→Firma→Storage→Recepción→Autorización) sobre
    /// un <see cref="ElectronicDocument"/> ya persistido en Draft o Failed. Cualquier fallo antes
    /// de la firma transiciona el documento a Failed (con el motivo real) en vez de abortar sin
    /// dejar rastro — reutilizado por <see cref="RegisterAsync"/> y por <see cref="RetryAsync"/>
    /// para no duplicar la lógica del pipeline.
    /// </summary>
    private async Task<Result<ElectronicDocumentDto>> RunPipelineAsync(
        ElectronicDocument document,
        RegisterElectronicDocumentRequest request,
        CancellationToken ct
    )
    {
        var provider = _providerResolver.Resolve(request.DocumentType);
        if (provider is null)
            return await FailAsync(
                document,
                request.UserId,
                ct,
                $"No hay un proveedor de datos registrado para el tipo de documento '{request.DocumentType}'."
            );

        var reference = new ElectronicDocumentSourceReference(
            request.TenantId,
            request.CompanyId,
            request.SourceEntityId
        );

        // A4 (auditoría de robustez): las etapas previas a la firma solo modifican el documento
        // via FailAsync (Draft/Failed→Failed) o al final via MarkXmlGenerated/MarkSigned — el
        // documento sigue siendo Draft/Failed en todo este tramo, así que cualquier excepción real
        // (no un Result.Failure esperado — p.ej. una excepción de firma/criptografía) puede
        // manejarse de forma segura con el mismo FailAsync que ya usan los fallos esperados, sin
        // violar el guard de estado de MarkFailed.
        SignedElectronicDocumentXml signedXml;
        try
        {
            var dataResult = await provider.GetDataAsync(reference, ct);
            if (!dataResult.IsSuccess)
            {
                var reason =
                    dataResult.Error
                    ?? "No se pudo obtener el modelo común del documento de origen.";
                LogStageFailed("Provider", request.SourceModule, request.SourceEntityId, reason);
                return await FailAsync(document, request.UserId, ct, reason, dataResult.Code);
            }

            // El ambiente (Pruebas/Producción) recién se conoce aquí — se captura oportunistamente,
            // sea cual sea el resultado de las etapas siguientes (persiste igual en un fallo posterior,
            // porque toda salida de este método hace SaveChangesAsync).
            document.SetEnvironment(dataResult.Value!.Emission.Environment);

            var xmlBuilder = _xmlBuilderResolver.Resolve(request.DocumentType);
            if (xmlBuilder is null)
                return await FailAsync(
                    document,
                    request.UserId,
                    ct,
                    $"No hay un generador de XML registrado para el tipo de documento '{request.DocumentType}'."
                );

            // El XML solo se construye para validar que el modelo común es traducible a un
            // comprobante bien formado — esta fase no lo guarda a disco (eso es de la fase de
            // firma/almacenamiento).
            var xmlResult = xmlBuilder.Build(dataResult.Value!);
            if (!xmlResult.IsSuccess)
            {
                var reason =
                    xmlResult.Error ?? "No se pudo generar el XML del documento electrónico.";
                LogStageFailed("XmlBuilder", request.SourceModule, request.SourceEntityId, reason);
                return await FailAsync(document, request.UserId, ct, reason, xmlResult.Code);
            }

            var schemaValidator = _schemaValidatorResolver.Resolve(request.DocumentType);
            if (schemaValidator is null)
                return await FailAsync(
                    document,
                    request.UserId,
                    ct,
                    $"No hay un validador de esquema registrado para el tipo de documento '{request.DocumentType}'."
                );

            // El XML nunca avanza a la siguiente etapa (firma electrónica) sin pasar por esta
            // validación — incluida la ausencia del XSD oficial, que el validador reporta como
            // IsValid = false en vez de dejar pasar un documento no verificado.
            var validation = await schemaValidator.ValidateAsync(xmlResult.Value!, ct);
            if (!validation.IsValid)
            {
                var reason = string.Join(" ", validation.Errors);
                LogStageFailed(
                    "SchemaValidator",
                    request.SourceModule,
                    request.SourceEntityId,
                    reason
                );
                return await FailAsync(
                    document,
                    request.UserId,
                    ct,
                    reason,
                    isValidationFailure: true
                );
            }

            // Si falla cualquier prerrequisito de firma (configuración SRI, certificado,
            // contraseña) o la firma en sí, el flujo se detiene aquí.
            var signResult = await _signingService.SignAsync(
                request.TenantId,
                request.CompanyId,
                xmlResult.Value!,
                ct
            );
            if (!signResult.IsSuccess)
            {
                var reason = signResult.Error ?? "No se pudo firmar el documento electrónico.";
                LogStageFailed("Signing", request.SourceModule, request.SourceEntityId, reason);
                return await FailAsync(document, request.UserId, ct, reason, signResult.Code);
            }

            // El documento ya existe (creado en Draft por RegisterAsync) — su Id se reutiliza como
            // parte de la ruta de IFileStorage, en vez de generar uno nuevo.
            var storageResult = await _xmlStorageService.StoreAsync(
                request.TenantId,
                request.DocumentType,
                document.Id,
                xmlResult.Value!,
                signResult.Value!,
                ct
            );
            if (!storageResult.IsSuccess)
            {
                var reason =
                    storageResult.Error ?? "No se pudo almacenar el XML del documento electrónico.";
                LogStageFailed("Storage", request.SourceModule, request.SourceEntityId, reason);
                return await FailAsync(document, request.UserId, ct, reason, storageResult.Code);
            }

            document.MarkXmlGenerated(
                storageResult.Value!.DraftXmlPath,
                xmlResult.Value!.Version,
                validation.SchemaVersion ?? string.Empty,
                request.UserId
            );
            document.MarkSigned(
                storageResult.Value.SignedXmlPath,
                AccessKey.Create(signResult.Value!.AccessKey),
                request.UserId
            );

            await _repository.SaveChangesAsync(ct);
            LogDocumentSigned(
                document.Id,
                request.SourceModule,
                request.SourceEntityId,
                document.AccessKey!.Value
            );

            signedXml = signResult.Value!;
        }
        catch (Exception ex)
        {
            LogPipelineStageThrew(document.Id, "PreSignature", ex);
            return await FailAsync(
                document,
                request.UserId,
                ct,
                $"Error inesperado en el pipeline: {ex.Message}"
            );
        }

        // Checkpoint intermedio: el documento firmado ya quedó persistido en BD antes de
        // intentar la llamada de red al SRI. Si el proceso falla entre este punto y el envío,
        // el documento sigue existiendo en Signed — nunca se pierde el registro de un XML que
        // eventualmente sí podría haber llegado al SRI, y nunca se reintenta ciegamente sin
        // saber si ya fue enviado.
        //
        // A partir de aquí el documento ya es Signed (no Draft/Failed) — FailAsync/MarkFailed no
        // aplica. Una excepción real en este tramo se trata igual que cualquier otro fallo de red
        // no resuelto (ver TrySendToReceptionAsync/AuthorizeAsync): el documento permanece en su
        // estado actual y el job de reintentos vuelve a intentarlo, sin dejar una excepción sin
        // capturar propagándose fuera del pipeline.
        try
        {
            var (reachedReceived, _) = await TrySendToReceptionAsync(
                document,
                request.CompanyId,
                request.UserId,
                Encoding.UTF8.GetBytes(signedXml.SignedXml),
                ct
            );
            if (reachedReceived)
                await AuthorizeAsync(
                    document,
                    request.TenantId,
                    request.CompanyId,
                    request.UserId,
                    ct
                );
        }
        catch (Exception ex)
        {
            LogPipelineStageThrew(document.Id, "ReceptionOrAuthorization", ex);
        }

        return Result<ElectronicDocumentDto>.Success(ElectronicDocumentMapper.ToDto(document));
    }

    /// <summary>Marca el documento como Failed y persiste antes de devolver el Result de fallo correspondiente.</summary>
    private async Task<Result<ElectronicDocumentDto>> FailAsync(
        ElectronicDocument document,
        Guid userId,
        CancellationToken ct,
        string reason,
        string? code = null,
        bool isValidationFailure = false
    )
    {
        document.MarkFailed(reason, userId);
        await _repository.SaveChangesAsync(ct);
        return isValidationFailure
            ? Result<ElectronicDocumentDto>.ValidationFailure(reason)
            : Result<ElectronicDocumentDto>.Failure(reason, code);
    }

    public async Task<Result<ElectronicDocumentDto>> RetryAsync(
        Guid tenantId,
        Guid electronicDocumentId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var document = await _repository.GetByIdAsync(tenantId, electronicDocumentId, ct);
        if (document is null)
            return Result<ElectronicDocumentDto>.NotFound("El documento electrónico no existe.");

        if (document.CurrentState == ElectronicDocumentState.DeadLetter)
        {
            document.Reactivate(userId);
            await _repository.SaveChangesAsync(ct);
            LogDocumentReactivated(document.Id, document.CurrentState);
        }

        // Reactivate() pudo haber restaurado el documento a Draft/Failed (si el DeadLetter vino
        // de un fallo temprano del pipeline, no de la comunicación con el SRI) — se reanuda el
        // pipeline completo en vez del camino de reintento de red de más abajo.
        if (
            document.CurrentState is ElectronicDocumentState.Draft or ElectronicDocumentState.Failed
        )
        {
            var request = new RegisterElectronicDocumentRequest(
                tenantId,
                document.CompanyId,
                document.DocumentType,
                document.SourceModule,
                document.SourceEntityId,
                userId
            );
            await RunPipelineAsync(document, request, ct);
            await ApplyDeadLetterIfExhaustedAsync(document, userId, document.LastError, ct);
            return Result<ElectronicDocumentDto>.Success(ElectronicDocumentMapper.ToDto(document));
        }

        if (
            document.CurrentState
            is not (ElectronicDocumentState.Signed or ElectronicDocumentState.Received)
        )
            return Result<ElectronicDocumentDto>.ValidationFailure(
                $"El documento no está en un estado reintentable (estado actual: {document.CurrentState})."
            );

        document.MarkRetryAttempted(userId);
        await _repository.SaveChangesAsync(ct);
        LogRetryAttempted(document.Id, document.CurrentState, document.RetryCount);

        // A4: MarkRetryAttempted ya persistió arriba, así que RetryCount ya avanzó antes de esta
        // llamada de red — una excepción real aquí no deja el conteo de reintentos desincronizado
        // (a diferencia del tramo previo a la firma, cubierto por el try/catch de RunPipelineAsync).
        string? lastFailureReason;
        try
        {
            if (document.CurrentState == ElectronicDocumentState.Signed)
            {
                var signedXmlBytes = await ReadSignedXmlAsync(document, ct);
                if (signedXmlBytes is null)
                {
                    lastFailureReason = "No se pudo releer el XML firmado ya almacenado.";
                    LogSignedXmlUnavailable(document.Id);
                }
                else
                {
                    var (reachedReceived, receptionFailureReason) = await TrySendToReceptionAsync(
                        document,
                        document.CompanyId,
                        userId,
                        signedXmlBytes,
                        ct
                    );
                    lastFailureReason = receptionFailureReason;
                    if (reachedReceived)
                        lastFailureReason = await AuthorizeAsync(
                            document,
                            tenantId,
                            document.CompanyId,
                            userId,
                            ct
                        );
                }
            }
            else
            {
                lastFailureReason = await AuthorizeAsync(
                    document,
                    tenantId,
                    document.CompanyId,
                    userId,
                    ct
                );
            }
        }
        catch (Exception ex)
        {
            LogPipelineStageThrew(document.Id, "RetryReceptionOrAuthorization", ex);
            lastFailureReason = ex.Message;
        }

        await ApplyDeadLetterIfExhaustedAsync(document, userId, lastFailureReason, ct);

        return Result<ElectronicDocumentDto>.Success(ElectronicDocumentMapper.ToDto(document));
    }

    /// <summary>
    /// Si el documento sigue sin resolverse (Failed/Signed/Received) y ya se agotaron los
    /// intentos permitidos, pasa a DeadLetter — única exhaución que dispara esta transición
    /// fuera del rechazo/timeout explícito del SRI (ver AuthorizeAsync). El motivo real del
    /// último intento (p.ej. un SOAP Fault del SRI, o el motivo del último Failed) queda visible
    /// en el Monitor — nunca solo el texto genérico de exhaución. Compartido por el camino de
    /// reintento de pipeline (Draft/Failed) y el de red (Signed/Received).
    /// </summary>
    private async Task ApplyDeadLetterIfExhaustedAsync(
        ElectronicDocument document,
        Guid userId,
        string? lastFailureReason,
        CancellationToken ct
    )
    {
        if (
            document.CurrentState
            is not (
                ElectronicDocumentState.Failed
                or ElectronicDocumentState.Signed
                or ElectronicDocumentState.Received
            )
        )
            return;
        if (document.RetryCount < ElectronicDocumentRetryPolicy.MaxAttempts)
            return;

        var reason = lastFailureReason is null
            ? $"Reintentos agotados tras {ElectronicDocumentRetryPolicy.MaxAttempts} intentos."
            : $"Reintentos agotados tras {ElectronicDocumentRetryPolicy.MaxAttempts} intentos. Último error: {lastFailureReason}";
        document.MarkDeadLetter(reason, userId);
        await _repository.SaveChangesAsync(ct);
        LogDocumentDeadLettered(document.Id, reason);
    }

    /// <summary>Relee el XML firmado ya almacenado — necesario para reenviar desde Signed sin volver a firmar.</summary>
    private async Task<byte[]?> ReadSignedXmlAsync(
        ElectronicDocument document,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(document.SignedXmlPath))
            return null;

        await using var stream = await _fileStorage.GetAsync(document.SignedXmlPath, ct);
        if (stream is null)
            return null;

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    // SOAP-02 (auditoría SRI, Fase 2): código de mensaje oficial del SRI para "Clave acceso en
    // procesamiento" (tabla de errores de la ficha técnica, validación RECEPCIÓN). Nota 2 de esa
    // misma tabla: "no se deberá reenviar el comprobante o generar el comprobante con otra clave
    // de acceso y secuencial hasta recibir una respuesta de autorización o rechazo del mismo, en
    // un tiempo máximo de 24 horas".
    //
    // RESP-01 (cierre ElectronicDocuments v1.0, robustez de reenvío): "45 Secuencial registrado"
    // y "43 Clave acceso registrada" son la misma tabla oficial de errores de RECEPCIÓN — ya
    // documentados como motivo del índice único uq_electronic_document_access_key (ver BD-01 en
    // ElectronicDocumentConfiguration.cs) — y tienen idéntico significado funcional que el 70: el
    // SRI ya tiene un envío anterior de esta clave de acceso, no es un rechazo real. Solo pueden
    // aparecer en un reenvío (Signed→TrySendToReceptionAsync vía RetryAsync, que siempre reutiliza
    // el mismo XML ya firmado, nunca genera uno nuevo ni vuelve a firmar), y la ficha técnica exige
    // en ese caso consultar autorización en vez de asumir rechazo. Antes solo el 70 activaba esta
    // rama; el 43/45 caían en el rechazo genérico de más abajo, marcando Rejected un documento que
    // en realidad seguía vivo (o ya autorizado) en el SRI.
    //
    // Cada mensaje de <see cref="SriReceptionResult.Errors"/> se construye como
    // "[identificador] mensaje: informacionAdicional" (ver SriSoapClient.ParseRecepcionResponse)
    // — se detecta por ese prefijo literal.
    private static readonly string[] AlreadyExistsErrorPrefixes = ["[70]", "[43]", "[45]"];

    /// <summary>
    /// Envía el XML firmado a Recepción del SRI y aplica la transición que corresponda
    /// (Sent→Received, Sent→Received si el SRI reporta que ya existe un envío anterior de esta
    /// clave de acceso — códigos 70/43/45, ver <see cref="AlreadyExistsErrorPrefixes"/> — o
    /// Sent→Rejected solo cuando el SRI devuelve evidencia de rechazo definitivo). Devuelve
    /// <c>ReachedReceived=true</c> si el documento llegó a Received (listo para consultar
    /// autorización); si no, <c>FailureReason</c> trae el motivo real (nunca genérico) para que el
    /// llamador lo use si el documento termina en DeadLetter por exhaución de reintentos.
    /// </summary>
    private async Task<(bool ReachedReceived, string? FailureReason)> TrySendToReceptionAsync(
        ElectronicDocument document,
        Guid companyId,
        Guid userId,
        byte[] signedXmlBytes,
        CancellationToken ct
    )
    {
        var receptionResult = await _receptionService.SendAsync(companyId, signedXmlBytes, ct);
        if (!receptionResult.IsSuccess)
        {
            // Si receptionResult falla (sin configuración/URL SRI, sin conectividad, SOAP Fault,
            // respuesta no interpretable), el documento permanece en Signed: no hubo envío real,
            // por lo que MarkSent no aplica.
            LogReceptionNotSent(document.Id, receptionResult.Error);
            return (false, receptionResult.Error);
        }

        document.MarkSent(userId);

        var recepcion = receptionResult.Value!;
        if (recepcion.Received)
        {
            document.MarkReceived(userId);
            await _repository.SaveChangesAsync(ct);
            LogDocumentReceived(document.Id, recepcion.Status);
            return (true, null);
        }

        // Códigos 70/43/45 — "Clave acceso en procesamiento" / "Clave acceso registrada" /
        // "Secuencial registrado": el SRI ya tiene un envío anterior de este mismo comprobante.
        // NO es un rechazo: el comprobante sigue vivo (o ya autorizado) en el SRI y debe tratarse
        // como si hubiera llegado a Received, para que el siguiente paso del pipeline consulte
        // autorización directamente, sin reenviar ni generar una nueva clave de acceso.
        var matchedPrefix = AlreadyExistsErrorPrefixes.FirstOrDefault(prefix =>
            recepcion.Errors.Any(e => e.StartsWith(prefix, StringComparison.Ordinal))
        );
        if (matchedPrefix is not null)
        {
            document.MarkReceived(userId);
            await _repository.SaveChangesAsync(ct);
            LogDocumentAlreadyProcessing(document.Id, matchedPrefix, recepcion.Status);
            return (true, null);
        }

        // Cualquier otra respuesta oficial del SRI que no sea RECIBIDA (p.ej. DEVUELTA con un
        // error real de validación) se interpreta como rechazo — sin reintento posible.
        var reason =
            recepcion.Errors.Count > 0
                ? string.Join(" ", recepcion.Errors)
                : $"El SRI devolvió el comprobante con estado '{recepcion.Status}'.";
        document.MarkRejected(reason, userId, recepcion.StructuredMessages);
        await _repository.SaveChangesAsync(ct);
        LogDocumentRejectedAtReception(document.Id, recepcion.Status, reason);
        return (false, null);
    }

    /// <summary>
    /// Consulta la autorización de un documento ya en Received y aplica la única transición
    /// que corresponda (Authorized/Rejected) — o ninguna, si la consulta en sí no obtuvo una
    /// respuesta definitiva del SRI (fallo de transporte, o TIMEOUT del polling interno de
    /// <see cref="Common.Interfaces.SRI.ISriAuthorizationClient"/> — el WS sigue procesando, no
    /// es una decisión del SRI), dejando el documento en Received para un reintento posterior.
    /// Nunca decide el DeadLetter aquí: eso es responsabilidad exclusiva de
    /// <see cref="ApplyDeadLetterIfExhaustedAsync"/>, la única autoridad sobre cuándo se agotaron
    /// los reintentos. Devuelve el motivo real cuando no resuelve, para que el llamador lo use si
    /// el documento termina en DeadLetter.
    /// </summary>
    private async Task<string?> AuthorizeAsync(
        ElectronicDocument document,
        Guid tenantId,
        Guid companyId,
        Guid userId,
        CancellationToken ct
    )
    {
        var authResult = await _authorizationService.CheckAsync(
            companyId,
            document.AccessKey!.Value,
            ct
        );
        if (!authResult.IsSuccess)
        {
            LogAuthorizationNotResolved(document.Id, authResult.Error);
            return authResult.Error;
        }

        var auth = authResult.Value!;
        if (auth.Authorized)
        {
            string? authorizedXmlPath = null;
            if (!string.IsNullOrWhiteSpace(auth.DocumentXml))
            {
                var storeResult = await _xmlStorageService.StoreAuthorizedAsync(
                    tenantId,
                    document.DocumentType,
                    document.Id,
                    auth.DocumentXml,
                    ct
                );
                // Si el almacenamiento del XML autorizado falla, el documento se autoriza
                // igual: el número y la fecha de autorización son el dato oficial mínimo exigido
                // por el SRI, y el archivo puede recuperarse más adelante con una consulta nueva.
                if (storeResult.IsSuccess)
                    authorizedXmlPath = storeResult.Value;
                else
                    LogAuthorizedXmlNotStored(document.Id, storeResult.Error);
            }

            // AUTH-01 (auditoría SRI, Fase 2): en el esquema offline el número de autorización
            // ES la clave de acceso — nunca un valor generado independientemente por el SRI
            // (ficha técnica, sección 5.9, confirmado también por el ejemplo oficial de la
            // sección 7.2.3: <numeroAutorizacion> y <claveAccesoConsultada> son idénticos). No se
            // confía ciegamente en lo que el SOAP reporte: si el SRI llegara a declarar un
            // numeroAutorizacion distinto de la clave de acceso del propio documento, es una
            // anomalía real que se registra explícitamente — pero el documento SÍ se autoriza
            // igual (auth.Authorized ya es true: la autorización ocurrió realmente en el SRI), y
            // el valor persistido es siempre la clave de acceso, la única fuente de verdad
            // definida por la ficha técnica para este campo en offline.
            if (
                !string.IsNullOrWhiteSpace(auth.AuthorizationNumber)
                && !string.Equals(
                    auth.AuthorizationNumber,
                    document.AccessKey!.Value,
                    StringComparison.Ordinal
                )
            )
            {
                LogAuthorizationNumberMismatch(
                    document.Id,
                    auth.AuthorizationNumber,
                    document.AccessKey.Value
                );
            }

            document.MarkAuthorized(
                AuthorizationNumber.Create(document.AccessKey!.Value),
                auth.AuthorizationDate ?? DateTime.UtcNow,
                authorizedXmlPath,
                userId
            );
            LogDocumentAuthorized(document.Id, document.AuthorizationNumber!.Value);
        }
        else if (auth.Status.Equals("NO AUTORIZADO", StringComparison.OrdinalIgnoreCase))
        {
            var reason = auth.ErrorMessage ?? "El SRI rechazó el comprobante.";
            document.MarkRejected(reason, userId, auth.StructuredMessages);
            LogDocumentRejectedAtAuthorization(document.Id, reason);
        }
        else if (auth.Status.Equals("TIMEOUT", StringComparison.OrdinalIgnoreCase))
        {
            // TIMEOUT no es una decisión del SRI: el WS de autorización sigue sin resolver tras
            // el polling interno del cliente SOAP (SriSoapClient.CheckAuthorizationAsync agota
            // sus propios 5 intentos con backoff 2s/4s/8s/16s). Se trata igual que cualquier otra
            // consulta sin respuesta definitiva (rama !authResult.IsSuccess más arriba): el
            // documento permanece en Received, y solo ApplyDeadLetterIfExhaustedAsync decide el
            // DeadLetter, tras agotar ElectronicDocumentRetryPolicy.MaxAttempts.
            var reason =
                auth.ErrorMessage
                ?? "El SRI no respondió a la consulta de autorización tras varios reintentos.";
            LogAuthorizationNotResolved(document.Id, reason);
            return reason;
        }
        else
        {
            return null; // No debería ocurrir: ElectronicDocumentAuthorizationService ya filtra estos casos como Failure.
        }

        await _repository.SaveChangesAsync(ct);
        return null;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Iniciando registro — tenant {TenantId}, empresa {CompanyId}, tipo {DocumentType}, origen {SourceModule}/{SourceEntityId}"
    )]
    private partial void LogRegisterStarted(
        Guid tenantId,
        Guid companyId,
        ElectronicDocumentType documentType,
        string sourceModule,
        Guid sourceEntityId
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Registro omitido: ya existe el documento {ExistingId} para origen {SourceModule}/{SourceEntityId}"
    )]
    private partial void LogRegisterConflict(
        string sourceModule,
        Guid sourceEntityId,
        Guid existingId
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Etapa '{Stage}' falló para origen {SourceModule}/{SourceEntityId}: {Reason}"
    )]
    private partial void LogStageFailed(
        string stage,
        string sourceModule,
        Guid sourceEntityId,
        string? reason
    );

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Documento {ElectronicDocumentId} (origen {SourceModule}/{SourceEntityId}) firmado y persistido — claveAcceso {AccessKey}"
    )]
    private partial void LogDocumentSigned(
        Guid electronicDocumentId,
        string sourceModule,
        Guid sourceEntityId,
        string accessKey
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Documento {ElectronicDocumentId} no pudo enviarse a recepción SRI, permanece en Signed: {Reason}"
    )]
    private partial void LogReceptionNotSent(Guid electronicDocumentId, string? reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Documento {ElectronicDocumentId} recibido por el SRI (estado {Status})"
    )]
    private partial void LogDocumentReceived(Guid electronicDocumentId, string status);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Documento {ElectronicDocumentId} rechazado en recepción (estado {Status}): {Reason}"
    )]
    private partial void LogDocumentRejectedAtReception(
        Guid electronicDocumentId,
        string status,
        string reason
    );

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Documento {ElectronicDocumentId} ya tenía un envío anterior registrado en el SRI ({Code}, estado {Status}) — tratado como recibido, sin reenviar"
    )]
    private partial void LogDocumentAlreadyProcessing(
        Guid electronicDocumentId,
        string code,
        string status
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Consulta de autorización sin respuesta definitiva para {ElectronicDocumentId}, permanece en Received: {Reason}"
    )]
    private partial void LogAuthorizationNotResolved(Guid electronicDocumentId, string? reason);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] No se pudo almacenar el XML autorizado de {ElectronicDocumentId}: {Reason}"
    )]
    private partial void LogAuthorizedXmlNotStored(Guid electronicDocumentId, string? reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Documento {ElectronicDocumentId} AUTORIZADO por el SRI — número de autorización {AuthorizationNumber}"
    )]
    private partial void LogDocumentAuthorized(
        Guid electronicDocumentId,
        string authorizationNumber
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Documento {ElectronicDocumentId}: el SRI reportó numeroAutorizacion '{ReportedAuthorizationNumber}' distinto de la clave de acceso '{AccessKey}' — se persiste la clave de acceso (fuente de verdad en el esquema offline, ficha técnica sección 5.9)"
    )]
    private partial void LogAuthorizationNumberMismatch(
        Guid electronicDocumentId,
        string reportedAuthorizationNumber,
        string accessKey
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Documento {ElectronicDocumentId} rechazado en autorización: {Reason}"
    )]
    private partial void LogDocumentRejectedAtAuthorization(
        Guid electronicDocumentId,
        string reason
    );

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "[ElectronicDocuments] Documento {ElectronicDocumentId} pasó a DeadLetter: {Reason}"
    )]
    private partial void LogDocumentDeadLettered(Guid electronicDocumentId, string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Documento {ElectronicDocumentId} reactivado desde DeadLetter — estado restaurado {RestoredState}"
    )]
    private partial void LogDocumentReactivated(
        Guid electronicDocumentId,
        ElectronicDocumentState restoredState
    );

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Reintento #{RetryCount} para documento {ElectronicDocumentId} (estado {State})"
    )]
    private partial void LogRetryAttempted(
        Guid electronicDocumentId,
        ElectronicDocumentState state,
        int retryCount
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] No se pudo releer el XML firmado del documento {ElectronicDocumentId} para reintentar el envío"
    )]
    private partial void LogSignedXmlUnavailable(Guid electronicDocumentId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "[ElectronicDocuments] Excepción no controlada en la etapa '{Stage}' del pipeline para el documento {ElectronicDocumentId}"
    )]
    private partial void LogPipelineStageThrew(
        Guid electronicDocumentId,
        string stage,
        Exception ex
    );
}
