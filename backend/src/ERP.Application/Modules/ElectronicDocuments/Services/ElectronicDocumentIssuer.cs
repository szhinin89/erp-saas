using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

public sealed partial class ElectronicDocumentIssuer
    : IElectronicDocumentIssuer,
        IElectronicDocumentRegistration,
        IElectronicDocumentCorrectedRetransmission
{
    private readonly IElectronicDocumentRepository _repository;
    private readonly IElectronicDocumentXmlSupplierResolver _xmlSupplierResolver;
    private readonly IElectronicDocumentSchemaValidatorResolver _schemaValidatorResolver;
    private readonly IElectronicDocumentSigningService _signingService;
    private readonly IElectronicDocumentXmlStorageService _xmlStorageService;
    private readonly IElectronicDocumentReceptionService _receptionService;
    private readonly IElectronicDocumentAuthorizationService _authorizationService;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly IElectronicDocumentSourceLifecycleGuardResolver _sourceGuards;
    private readonly IElectronicEmissionSwitch _emissionSwitch;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ElectronicDocumentIssuer> _logger;

    public ElectronicDocumentIssuer(
        IElectronicDocumentRepository repository,
        IElectronicDocumentXmlSupplierResolver xmlSupplierResolver,
        IElectronicDocumentSchemaValidatorResolver schemaValidatorResolver,
        IElectronicDocumentSigningService signingService,
        IElectronicDocumentXmlStorageService xmlStorageService,
        IElectronicDocumentReceptionService receptionService,
        IElectronicDocumentAuthorizationService authorizationService,
        IDatabaseExceptionTranslator dbEx,
        IElectronicDocumentSourceLifecycleGuardResolver sourceGuards,
        IElectronicEmissionSwitch emissionSwitch,
        IUnitOfWork unitOfWork,
        ILogger<ElectronicDocumentIssuer> logger
    )
    {
        _repository = repository;
        _xmlSupplierResolver = xmlSupplierResolver;
        _schemaValidatorResolver = schemaValidatorResolver;
        _signingService = signingService;
        _xmlStorageService = xmlStorageService;
        _receptionService = receptionService;
        _authorizationService = authorizationService;
        _dbEx = dbEx;
        _sourceGuards = sourceGuards;
        _emissionSwitch = emissionSwitch;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public Task<Result<ElectronicDocumentDto>> RegisterAsync(
        RegisterElectronicDocumentRequest request,
        CancellationToken ct = default
    ) => RegisterCoreAsync(request, createOnly: false, ct);

    public Task<Result<ElectronicDocumentDto>> RegisterMissingAsync(
        RegisterElectronicDocumentRequest request,
        CancellationToken ct = default
    ) => RegisterCoreAsync(request, createOnly: true, ct);

    private async Task<Result<ElectronicDocumentDto>> RegisterCoreAsync(
        RegisterElectronicDocumentRequest request,
        bool createOnly,
        CancellationToken ct
    )
    {
        LogRegisterStarted(
            request.TenantId,
            request.CompanyId,
            request.DocumentType,
            request.SourceModule,
            request.SourceEntityId
        );

        // ADR-036 (I-1) — un origen que ya no permite procesamiento electrónico nunca registra
        // (ni reanuda) su comprobante. Solo aplica a orígenes con guard (Retenciones y Ventas).
        var guard = _sourceGuards.Resolve(request.SourceModule);
        if (guard is not null)
        {
            var lifecycle = await guard.EvaluateAsync(
                request.TenantId,
                request.CompanyId,
                request.SourceEntityId,
                lockForUpdate: false,
                ct
            );
            if (!lifecycle.AllowsProcessing)
                return SourceNotProcessable(
                    request.SourceModule,
                    request.SourceEntityId,
                    lifecycle
                );
        }

        var existing = await _repository.GetBySourceAsync(
            request.TenantId,
            request.SourceModule,
            request.SourceEntityId,
            ct
        );
        if (existing is not null)
        {
            // Recovery must never resume even Draft/Failed. This check also covers a
            // request that inserted between candidate selection and registration.
            if (createOnly)
                return Result<ElectronicDocumentDto>.Success(ElectronicDocumentMapper.ToDto(existing));

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

        return createOnly
            ? Result<ElectronicDocumentDto>.Success(ElectronicDocumentMapper.ToDto(document))
            : await RunPipelineAsync(document, request, ct);
    }

    /// <summary>
    /// Corre el pipeline completo (Provider→XML→XSD→Firma→Storage→Recepción→Autorización) sobre
    /// un <see cref="ElectronicDocument"/> ya persistido en Draft o Failed. Cualquier fallo antes
    /// de la firma transiciona el documento a Failed (con el motivo real) en vez de abortar sin
    /// dejar rastro — reutilizado por <see cref="RegisterAsync"/> y por <see cref="RetryAsync"/>
    /// para no duplicar la lógica del pipeline.
    /// </summary>
    /// <summary>
    /// Ficha Técnica Offline §7.5 — "320 Kb" por comprobante en envío individual. La ficha no define la unidad:
    /// 320 × 1024 bytes es un límite provisional (la mayor lectura en bytes), única definición (R16).
    /// </summary>
    private const int MaxIndividualSignedXmlBytes = 320 * 1024;

    private async Task<Result<ElectronicDocumentDto>> RunPipelineAsync(
        ElectronicDocument document,
        RegisterElectronicDocumentRequest request,
        CancellationToken ct
    )
    {
        // ADR-036 (I-1) — antes de generar XML: el origen debe seguir permitiendo procesamiento.
        // Sin escrituras si no lo permite (no es un fallo del pipeline: el documento no pasa a Failed).
        var guard = _sourceGuards.Resolve(document.SourceModule);
        if (guard is not null)
        {
            var lifecycle = await guard.EvaluateAsync(
                document.TenantId,
                document.CompanyId,
                document.SourceEntityId,
                lockForUpdate: false,
                ct
            );
            if (!lifecycle.AllowsProcessing)
                return SourceNotProcessable(
                    document.SourceModule,
                    document.SourceEntityId,
                    lifecycle
                );
        }

        // SRI-TOGGLE-02 — sin emisión habilitada (o pendiente sin reanudación explícita) no se genera,
        // firma ni transmite nada: sin escrituras, el documento conserva Draft/Failed y sus intentos.
        if (
            await EmissionBlockedAsync(document, request.UserId, lockForShare: false, ct)
            is { } emissionBlocked
        )
            return emissionBlocked;

        var supplier = _xmlSupplierResolver.Resolve(request.DocumentType);
        if (supplier is null)
            return await FailAsync(
                document,
                request.UserId,
                ct,
                $"No hay un generador de XML registrado para el tipo de documento '{request.DocumentType}'."
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
            var prepared = await PrepareSignedXmlAsync(supplier, request.DocumentType, reference, ct);

            // El ambiente (Pruebas/Producción) recién se conoce al generar el XML — se captura
            // oportunistamente, sea cual sea el resultado de las etapas siguientes (persiste igual en
            // un fallo posterior, porque toda salida de este método hace SaveChangesAsync). Viaja en el
            // propio ElectronicDocumentXml (ver DTOs/ElectronicDocumentXml.cs).
            if (prepared.Xml is not null)
                document.SetEnvironment(prepared.Xml.Environment);

            if (prepared.Failure is { } failure)
            {
                if (failure.Stage is not null)
                    LogStageFailed(
                        failure.Stage,
                        request.SourceModule,
                        request.SourceEntityId,
                        failure.Reason
                    );
                return await FailAsync(
                    document,
                    request.UserId,
                    ct,
                    failure.Reason,
                    failure.Code,
                    failure.IsValidation
                );
            }

            var xml = prepared.Xml!;
            var schemaVersion = prepared.SchemaVersion ?? string.Empty;
            var signed = prepared.Signed!;

            // El documento ya existe (creado en Draft por RegisterAsync) — su Id se reutiliza como
            // parte de la ruta de IFileStorage, en vez de generar uno nuevo.
            var storageResult = await _xmlStorageService.StoreAsync(
                request.TenantId,
                request.DocumentType,
                document.Id,
                xml,
                signed,
                ct
            );
            if (!storageResult.IsSuccess)
            {
                var reason =
                    storageResult.Error ?? "No se pudo almacenar el XML del documento electrónico.";
                LogStageFailed("Storage", request.SourceModule, request.SourceEntityId, reason);
                return await FailAsync(document, request.UserId, ct, reason, storageResult.Code);
            }

            if (guard is null)
            {
                // SRI-TOGGLE-02 — revalidación inmediatamente antes de persistir Signed (sin lock de origen).
                if (
                    await EmissionBlockedAsync(document, request.UserId, lockForShare: false, ct)
                    is { } claimBlocked
                )
                    return claimBlocked;
                document.MarkXmlGenerated(
                    storageResult.Value!.DraftXmlPath,
                    xml.Version,
                    schemaVersion,
                    request.UserId
                );
                document.MarkSigned(
                    storageResult.Value.SignedXmlPath,
                    AccessKey.Create(signed.AccessKey),
                    request.UserId
                );

                await _repository.SaveChangesAsync(ct);
            }
            else
            {
                // ADR-036 (D-3) — XmlGenerated→Signed(→Dispatching) persistidos juntos, bajo el lock
                // del origen y solo si sigue permitiendo procesamiento, ANTES de cualquier llamada externa.
                var claim = await ClaimDispatchAsync(
                    document,
                    guard,
                    storageResult.Value!,
                    xml.Version,
                    schemaVersion,
                    signed.AccessKey,
                    request.UserId,
                    ct
                );
                if (claim is not null)
                    return claim;
            }
            LogDocumentSigned(
                document.Id,
                request.SourceModule,
                request.SourceEntityId,
                document.AccessKey!.Value
            );

            signedXml = signed;
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

    /// <summary>Etapa previa a la firma que no pudo completarse; <see cref="Stage"/> null = sin log de etapa.</summary>
    private sealed record PipelineStageFailure(
        string? Stage,
        string Reason,
        string? Code = null,
        bool IsValidation = false
    );

    /// <summary>
    /// XML generado + XSD + firma + límite de tamaño. <see cref="Xml"/> existe en cuanto el supplier tuvo
    /// éxito, aunque una etapa posterior falle (el ambiente se captura igual).
    /// </summary>
    private sealed record PreparedSignedXml(
        ElectronicDocumentXml? Xml,
        string? SchemaVersion,
        SignedElectronicDocumentXml? Signed,
        PipelineStageFailure? Failure
    );

    /// <summary>
    /// Única secuencia Provider→XML→XSD→Firma→Tamaño, sin escrituras: la usan el pipeline de emisión
    /// (<see cref="RunPipelineAsync"/>) y la retransmisión corregida (R12 Fase B). Las excepciones las
    /// maneja cada llamador.
    /// </summary>
    private async Task<PreparedSignedXml> PrepareSignedXmlAsync(
        IElectronicDocumentXmlSupplier supplier,
        ElectronicDocumentType documentType,
        ElectronicDocumentSourceReference reference,
        CancellationToken ct
    )
    {
        // RETENTIONS-SRI-AUTHORIZATION-WIRING-04D: el supplier ya encapsula "obtener el modelo
        // común y construir el XML" (camino comercial) o su equivalente propio (Retención) —
        // este pipeline ya no conoce ElectronicDocumentData ni distingue por tipo aquí.
        var xmlResult = await supplier.BuildXmlAsync(reference, ct);
        if (!xmlResult.IsSuccess)
            return new(
                null,
                null,
                null,
                new(
                    "XmlSupplier",
                    xmlResult.Error ?? "No se pudo generar el XML del documento electrónico.",
                    xmlResult.Code
                )
            );
        var xml = xmlResult.Value!;

        var schemaValidator = _schemaValidatorResolver.Resolve(documentType);
        if (schemaValidator is null)
            return new(
                xml,
                null,
                null,
                new(
                    null,
                    $"No hay un validador de esquema registrado para el tipo de documento '{documentType}'."
                )
            );

        // El XML nunca avanza a la siguiente etapa (firma electrónica) sin pasar por esta
        // validación — incluida la ausencia del XSD oficial, que el validador reporta como
        // IsValid = false en vez de dejar pasar un documento no verificado.
        var validation = await schemaValidator.ValidateAsync(xml, ct);
        if (!validation.IsValid)
            return new(
                xml,
                null,
                null,
                new("SchemaValidator", string.Join(" ", validation.Errors), IsValidation: true)
            );

        // Si falla cualquier prerrequisito de firma (configuración SRI, certificado,
        // contraseña) o la firma en sí, el flujo se detiene aquí.
        var signResult = await _signingService.SignAsync(
            reference.TenantId,
            reference.CompanyId,
            xml,
            ct
        );
        if (!signResult.IsSuccess)
            return new(
                xml,
                validation.SchemaVersion,
                null,
                new(
                    "Signing",
                    signResult.Error ?? "No se pudo firmar el documento electrónico.",
                    signResult.Code
                )
            );

        // R16 (auditoría SRI 2.34): "para el envío individual, el tamaño máximo por comprobante
        // será de 320 Kb" (Ficha Técnica Offline §7.5; excederlo es el error 26 de RECEPCIÓN). Se
        // verifica sobre el XML firmado — lo que realmente se transmite — antes de persistirlo
        // como Signed y del primer envío, con el límite provisional de MaxIndividualSignedXmlBytes.
        var signedXmlBytes = Encoding.UTF8.GetByteCount(signResult.Value!.SignedXml);
        if (signedXmlBytes > MaxIndividualSignedXmlBytes)
            return new(
                xml,
                validation.SchemaVersion,
                null,
                new(
                    "SizeLimit",
                    $"El XML firmado ocupa {signedXmlBytes} bytes y supera el máximo de {MaxIndividualSignedXmlBytes} bytes (320 KB, límite provisional de la Ficha Técnica §7.5) por comprobante individual.",
                    IsValidation: true
                )
            );

        return new(xml, validation.SchemaVersion, signResult.Value, null);
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
        // ADR-036 — si el documento ya no está en Draft/Failed (p.ej. la anulación del origen lo
        // descartó mientras el pipeline corría y el reclamo de despacho releyó ese estado), no se
        // registra un fallo encima de un estado terminal.
        if (
            document.CurrentState
            is not (ElectronicDocumentState.Draft or ElectronicDocumentState.Failed)
        )
            return Result<ElectronicDocumentDto>.Failure(reason, code);

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

        // R12 Fase A — un guard sin protocolo Dispatching (Ventas) no cambia el reintento v1.0: Signed/
        // Received solo se consultan (R11, Caso #32) y Draft/Failed reanudan el pipeline, cuyo reclamo
        // sí revalida el origen bajo su lock (ClaimDispatchAsync).
        var guard = _sourceGuards.Resolve(document.SourceModule);
        if (guard is { UsesDispatchProtocol: true })
            return await RetryGuardedAsync(document, guard, userId, ct);

        if (document.CurrentState == ElectronicDocumentState.DeadLetter)
        {
            if (await ReactivationBlockedAsync(document, userId, ct) is { } emissionBlocked)
                return emissionBlocked;
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
            var pipeline = await RunPipelineAsync(document, request, ct);
            if (pipeline.Code == ApiResponseCodes.ElectronicDocuments.EmissionDisabled)
                return pipeline;
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
        //
        // Caso #32: un Signed pudo haber llegado al SRI aunque el envío no obtuviera respuesta. Nunca
        // se reenvía: solo se consulta su autorización por clave de acceso (igual que los orígenes con
        // guard). AUTORIZADO se conserva; una consulta no concluyente deja el estado intacto
        // ("Requiere conciliación").
        string? lastFailureReason;
        try
        {
            LogQueryOnlyRetry(document.Id, document.CurrentState);
            lastFailureReason = await AuthorizeAsync(
                document,
                tenantId,
                document.CompanyId,
                userId,
                ct
            );
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
                or ElectronicDocumentState.Dispatching
                or ElectronicDocumentState.Received
            )
        )
            return;
        var maximum = ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Resolve(
            await _repository.GetRetryMaximumSettingAsync(document.TenantId, document.CompanyId, ct));
        if (document.RetryCount < maximum)
            return;

        // R11: con resultado SRI pendiente (Caso #32: solo consulta) el máximo de la empresa no cierra
        // la consulta antes del plazo del SRI (24 h desde RECIBIDA, ElectronicDocumentRetryPolicy.
        // SriProcessingWindow), contado desde la primera transición a Received del timeline. Sin esa
        // evidencia (Signed: el ERP nunca supo si el SRI lo recibió) el plazo no puede calcularse y no
        // se cierra la consulta automática: fail-closed, nunca un DeadLetter por una fecha supuesta.
        // R12 Fase B: tras una retransmisión corregida el plazo corre por intento, desde la RECIBIDA de
        // ese intento (TransmissionAttemptStartedUtc es null sin retransmisión: comportamiento v1.0).
        if (document.RequiresSriReconciliation)
        {
            var receivedAtUtc = await _repository.GetFirstReceivedAtUtcAsync(
                document.TenantId,
                document.Id,
                document.TransmissionAttemptStartedUtc,
                ct
            );
            if (
                receivedAtUtc is null
                || DateTime.UtcNow < receivedAtUtc.Value + ElectronicDocumentRetryPolicy.SriProcessingWindow
            )
                return;
            lastFailureReason =
                $"Sin resultado definitivo del SRI tras el plazo de {ElectronicDocumentRetryPolicy.SriProcessingWindow.TotalHours:0} h; requiere conciliación. {lastFailureReason}".TrimEnd();
        }

        var reason = lastFailureReason is null
            ? $"Reintentos agotados tras {maximum} intentos."
            : $"Reintentos agotados tras {maximum} intentos. Último error: {lastFailureReason}";
        document.MarkDeadLetter(reason, userId);
        await _repository.SaveChangesAsync(ct);
        LogDocumentDeadLettered(document.Id, reason);
    }

    /// <summary>
    /// ADR-036 — reintento de un origen con guard (hoy Retenciones). Diferencias con v1.0:
    /// (1) si el origen ya no permite procesamiento, no hay reactivación, pipeline ni consulta —
    /// cero escrituras; (2) un documento que salió o pudo salir al SRI (Signed histórico,
    /// Dispatching, Received) NUNCA se reenvía: solo se consulta su autorización por clave de
    /// acceso (D-4, D-10). Una respuesta no concluyente (TIMEOUT, error, desconocido) deja el estado
    /// tal cual — requiere conciliación —, nunca autoriza un reenvío.
    /// </summary>
    private async Task<Result<ElectronicDocumentDto>> RetryGuardedAsync(
        ElectronicDocument document,
        IElectronicDocumentSourceLifecycleGuard guard,
        Guid userId,
        CancellationToken ct
    )
    {
        var lifecycle = await guard.EvaluateAsync(
            document.TenantId,
            document.CompanyId,
            document.SourceEntityId,
            lockForUpdate: false,
            ct
        );
        if (!lifecycle.AllowsProcessing)
            return SourceNotProcessable(document.SourceModule, document.SourceEntityId, lifecycle);

        if (document.CurrentState == ElectronicDocumentState.DeadLetter)
        {
            if (await ReactivationBlockedAsync(document, userId, ct) is { } emissionBlocked)
                return emissionBlocked;
            document.Reactivate(userId);
            await _repository.SaveChangesAsync(ct);
            LogDocumentReactivated(document.Id, document.CurrentState);
        }

        if (
            document.CurrentState is ElectronicDocumentState.Draft or ElectronicDocumentState.Failed
        )
        {
            var request = new RegisterElectronicDocumentRequest(
                document.TenantId,
                document.CompanyId,
                document.DocumentType,
                document.SourceModule,
                document.SourceEntityId,
                userId
            );
            var pipeline = await RunPipelineAsync(document, request, ct);
            if (
                pipeline.Code
                is ApiResponseCodes.ElectronicDocuments.SourceNotProcessable
                    or ApiResponseCodes.ElectronicDocuments.EmissionDisabled
            )
                return pipeline;
            await ApplyDeadLetterIfExhaustedAsync(document, userId, document.LastError, ct);
            return Result<ElectronicDocumentDto>.Success(ElectronicDocumentMapper.ToDto(document));
        }

        if (
            document.CurrentState
            is not (
                ElectronicDocumentState.Signed
                or ElectronicDocumentState.Dispatching
                or ElectronicDocumentState.Received
            )
        )
            return Result<ElectronicDocumentDto>.ValidationFailure(
                $"El documento no está en un estado reintentable (estado actual: {document.CurrentState})."
            );

        document.MarkRetryAttempted(userId);
        await _repository.SaveChangesAsync(ct);
        LogRetryAttempted(document.Id, document.CurrentState, document.RetryCount);

        string? lastFailureReason;
        try
        {
            // Revalidación inmediatamente antes de la llamada externa (consulta de autorización).
            var recheck = await guard.EvaluateAsync(
                document.TenantId,
                document.CompanyId,
                document.SourceEntityId,
                lockForUpdate: false,
                ct
            );
            if (!recheck.AllowsProcessing)
                return SourceNotProcessable(
                    document.SourceModule,
                    document.SourceEntityId,
                    recheck
                );

            LogQueryOnlyRetry(document.Id, document.CurrentState);
            lastFailureReason = await AuthorizeAsync(
                document,
                document.TenantId,
                document.CompanyId,
                userId,
                ct
            );
        }
        catch (Exception ex)
        {
            LogPipelineStageThrew(document.Id, "RetryAuthorizationQuery", ex);
            lastFailureReason = ex.Message;
        }

        await ApplyDeadLetterIfExhaustedAsync(document, userId, lastFailureReason, ct);
        return Result<ElectronicDocumentDto>.Success(ElectronicDocumentMapper.ToDto(document));
    }

    /// <summary>
    /// ADR-036 (D-3) — reclamo de despacho: en una transacción corta toma el lock del origen
    /// (<c>FOR UPDATE</c>, el mismo que toma la anulación del origen), revalida que siga permitiendo
    /// procesamiento, relee el documento bajo ese lock y persiste XmlGenerated→Signed→Dispatching (o
    /// solo hasta Signed si el guard no usa el protocolo Dispatching: Ventas, R12 Fase A) en un solo
    /// SaveChanges. Solo después del commit se llama al SRI — nunca se sostiene el lock ni la
    /// transacción durante SOAP. Devuelve <c>null</c> si el reclamo quedó persistido, o el Result de
    /// fallo (sin escrituras) si el origen ya no lo permite o el documento cambió concurrentemente.
    /// </summary>
    private async Task<Result<ElectronicDocumentDto>?> ClaimDispatchAsync(
        ElectronicDocument document,
        IElectronicDocumentSourceLifecycleGuard guard,
        ElectronicDocumentStoredXmlPaths storedPaths,
        string xmlVersion,
        string schemaVersion,
        string accessKey,
        Guid userId,
        CancellationToken ct
    )
    {
        if (_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException(
                "El despacho electrónico no puede reclamarse dentro de una transacción de negocio abierta (se llamaría al SRI con locks tomados)."
            );

        var environment = document.Environment;
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var lifecycle = await guard.EvaluateAsync(
                document.TenantId,
                document.CompanyId,
                document.SourceEntityId,
                lockForUpdate: true,
                ct
            );
            await _repository.ReloadAsync(document, ct);

            if (!lifecycle.AllowsProcessing)
            {
                await _unitOfWork.RollbackAsync(ct);
                return SourceNotProcessable(
                    document.SourceModule,
                    document.SourceEntityId,
                    lifecycle
                );
            }

            if (
                document.CurrentState
                is not (ElectronicDocumentState.Draft or ElectronicDocumentState.Failed)
            )
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<ElectronicDocumentDto>.Conflict(
                    $"El documento electrónico cambió de estado durante el procesamiento (estado actual: {document.CurrentState})."
                );
            }

            // SRI-TOGGLE-02/04 — dentro del mismo reclamo: el lock compartido del interruptor (hasta el commit)
            // serializa con su apagado/habilitación.
            if (
                await EmissionBlockedAsync(document, userId, lockForShare: true, ct)
                is { } emissionBlocked
            )
            {
                await _unitOfWork.RollbackAsync(ct);
                return emissionBlocked;
            }

            if (environment is not null)
                document.SetEnvironment(environment);
            document.MarkXmlGenerated(storedPaths.DraftXmlPath, xmlVersion, schemaVersion, userId);
            document.MarkSigned(storedPaths.SignedXmlPath, AccessKey.Create(accessKey), userId);
            if (guard.UsesDispatchProtocol)
                document.MarkDispatching(userId);
            await _repository.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            LogDispatchClaimed(
                document.Id,
                document.CurrentState,
                document.SourceModule,
                document.SourceEntityId
            );
            return null;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            await _repository.ReloadAsync(document, ct);
            throw;
        }
    }

    /// <summary>
    /// SRI-TOGGLE-02 — control previo a toda transmisión NUEVA al SRI (emisión, reanudación desde
    /// Draft/Failed y retransmisión corregida); las consultas de autorización/estado no pasan por aquí.
    /// null = puede transmitirse; si no, el Result de bloqueo, sin escrituras. Usuario
    /// <see cref="Guid.Empty"/> = proceso automático: además exige actividad posterior a la habilitación,
    /// de modo que los pendientes solo se reanudan por una acción explícita de un usuario.
    /// </summary>
    private async Task<Result<ElectronicDocumentDto>?> EmissionBlockedAsync(
        ElectronicDocument document,
        Guid userId,
        bool lockForShare,
        CancellationToken ct
    )
    {
        var automatic = userId == Guid.Empty;
        var emission = await _emissionSwitch.GetAsync(
            document.TenantId,
            document.CompanyId,
            lockForShare,
            ct
        );
        // SRI-TOGGLE-04 — sin intentos previos, la actividad de un proceso automático es el instante
        // persistido del origen (autorización/emisión), nunca la creación del comprobante.
        var sourceActivity =
            automatic && emission.Enabled && document.LastAttemptUtc is null
                ? await _emissionSwitch.GetSourceActivityUtcAsync(
                    document.TenantId,
                    document.CompanyId,
                    document.SourceModule,
                    document.DocumentType,
                    document.SourceEntityId,
                    ct
                )
                : null;
        return emission.AllowsTransmission(document.LastAttemptUtc, sourceActivity, automatic)
            ? null
            : EmissionBlocked(emission, document.Id, document.CompanyId);
    }

    private Result<ElectronicDocumentDto> EmissionBlocked(
        ElectronicEmissionState emission,
        Guid documentId,
        Guid companyId
    )
    {
        var reason = emission.Enabled
            ? "El comprobante quedó pendiente antes de habilitar la emisión electrónica: requiere reanudación explícita desde el Monitor."
            : "La emisión electrónica está deshabilitada para esta empresa: el comprobante no se transmitirá al SRI hasta habilitarla y reanudarlo.";
        LogEmissionBlocked(documentId, companyId, emission.Enabled);
        return Result<ElectronicDocumentDto>.ValidationFailure(
            reason,
            ApiResponseCodes.ElectronicDocuments.EmissionDisabled
        );
    }

    /// <summary>SRI-TOGGLE-02 — reactivar un DeadLetter hacia Draft/Failed reanudaría una transmisión: se controla antes de escribir.</summary>
    private Task<Result<ElectronicDocumentDto>?> ReactivationBlockedAsync(
        ElectronicDocument document,
        Guid userId,
        CancellationToken ct
    ) =>
        document.PreDeadLetterState is ElectronicDocumentState.Draft or ElectronicDocumentState.Failed
            ? EmissionBlockedAsync(document, userId, lockForShare: false, ct)
            : Task.FromResult<Result<ElectronicDocumentDto>?>(null);

    private Result<ElectronicDocumentDto> SourceNotProcessable(
        string sourceModule,
        Guid sourceEntityId,
        ElectronicDocumentSourceLifecycle lifecycle
    )
    {
        var reason =
            lifecycle.Reason ?? "El documento de origen ya no permite procesamiento electrónico.";
        LogSourceNotProcessable(sourceModule, sourceEntityId, reason);
        return Result<ElectronicDocumentDto>.ValidationFailure(
            reason,
            ApiResponseCodes.ElectronicDocuments.SourceNotProcessable
        );
    }

    // SOAP-02 (auditoría SRI, Fase 2): código de mensaje oficial del SRI para "Clave acceso en
    // procesamiento" (tabla de errores de la ficha técnica, validación RECEPCIÓN). Nota 2 de esa
    // misma tabla: "no se deberá reenviar el comprobante o generar el comprobante con otra clave
    // de acceso y secuencial hasta recibir una respuesta de autorización o rechazo del mismo, en
    // un tiempo máximo de 24 horas".
    //
    // RESP-01 (cierre ElectronicDocuments v1.0, robustez de reenvío): "43 Clave acceso registrada"
    // es la misma tabla oficial de errores de RECEPCIÓN y tiene idéntico significado funcional que
    // el 70: el SRI ya tiene un envío anterior de esta clave de acceso, no es un rechazo real, y la
    // ficha técnica exige en ese caso consultar autorización en vez de asumir rechazo.
    //
    // Cada mensaje de <see cref="SriReceptionResult.Errors"/> se construye como
    // "[identificador] mensaje: informacionAdicional" (ver SriSoapClient.ParseRecepcionResponse)
    // — se detecta por ese prefijo literal.
    private static readonly string[] AlreadyExistsErrorPrefixes = ["[70]", "[43]"];

    // RESP-01 / código 45 (corrección 2026-11-02): "45 Secuencial registrado" NO identifica esta clave
    // de acceso, sino OTRA clave con el mismo secuencial (colisión, p. ej. secuencia reiniciada). En
    // la primera transmisión (TransmissionAttempt 1) la clave nunca salió antes — Signed solo se
    // consulta (caso #32) y Failed nunca sigue a Signed —, y si el SRI la tuviera respondería 43/70:
    // un 45 sin 43/70 es colisión confirmada y se rechaza con el mensaje original (consultar
    // autorización de una clave que el SRI nunca guardó no termina nunca). Excepciones, que conservan
    // la consulta de autorización: retransmisión corregida (R12, misma clave que ya llegó al SRI) y
    // respuesta posterior a un POST incierto del mismo envío (SriReceptionResult.PrecededByUncertainPost).
    private const string SequentialRegisteredErrorPrefix = "[45]";

    /// <summary>Identificadores de RECEPCIÓN que no constituyen rechazo del comprobante (ver R13 en
    /// <see cref="TrySendToReceptionAsync"/>).</summary>
    private static readonly string?[] TransientReturnCodes = ["50"];

    /// <summary>
    /// Envía el XML firmado a Recepción del SRI y aplica la transición que corresponda
    /// (Sent→Received, Sent→Received si el SRI reporta que ya existe un envío anterior de esta
    /// clave de acceso — códigos 70/43, ver <see cref="AlreadyExistsErrorPrefixes"/>, y 45 solo en una
    /// retransmisión corregida, ver <see cref="SequentialRegisteredErrorPrefix"/> — o
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

        var recepcion = receptionResult.Value!;

        // R13 (auditoría SRI 2.34): DEVUELTA solo con el código 50 "Error interno general" ("Cuando
        // ocurre un error inesperado en el servidor", tabla de errores de RECEPCIÓN) es una falla del
        // SRI, no un defecto del comprobante, y "los comprobantes devueltos no se guardarán en la base
        // de datos del SRI" (nota 1). No es un rechazo: igual que un fallo de transporte, el documento
        // conserva su estado (Signed/Dispatching) y, por el Caso #32, solo se consulta, nunca se
        // reenvía automáticamente.
        if (
            !recepcion.Received
            && recepcion.StructuredMessages.Count > 0
            && recepcion.StructuredMessages.All(m => TransientReturnCodes.Contains(m.Code))
        )
        {
            var transientReason = string.Join(" ", recepcion.Errors);
            LogReceptionReturnedTransient(document.Id, recepcion.Status, transientReason);
            return (false, transientReason);
        }

        document.MarkSent(userId);
        if (recepcion.Received)
        {
            document.MarkReceived(userId);
            await _repository.SaveChangesAsync(ct);
            LogDocumentReceived(document.Id, recepcion.Status);
            return (true, null);
        }

        // Códigos 70/43 — "Clave acceso en procesamiento" / "Clave acceso registrada": el SRI ya
        // tiene un envío anterior de este mismo comprobante. NO es un rechazo: el comprobante sigue
        // vivo (o ya autorizado) en el SRI y debe tratarse como si hubiera llegado a Received, para
        // que el siguiente paso del pipeline consulte autorización directamente, sin reenviar ni
        // generar una nueva clave de acceso. El 45 solo entra aquí en una retransmisión corregida o si
        // la respuesta llegó tras un POST incierto del mismo envío (reintento HTTP de SriSoapClient:
        // el POST anterior pudo haber llegado al SRI); si no, cae en el rechazo de abajo.
        var matchedPrefix = AlreadyExistsErrorPrefixes.FirstOrDefault(prefix =>
            recepcion.Errors.Any(e => e.StartsWith(prefix, StringComparison.Ordinal))
        );
        if (
            matchedPrefix is null
            && (document.TransmissionAttempt > 1 || recepcion.PrecededByUncertainPost)
            && recepcion.Errors.Any(e =>
                e.StartsWith(SequentialRegisteredErrorPrefix, StringComparison.Ordinal)
            )
        )
            matchedPrefix = SequentialRegisteredErrorPrefix;
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
            if (document.TransmissionAttempt == 1)
            {
                var reason = auth.ErrorMessage ?? "El SRI rechazó el comprobante.";
                document.MarkRejected(reason, userId, auth.StructuredMessages);
                LogDocumentRejectedAtAuthorization(document.Id, reason);
            }
            else
            {
                // R12 Fase B — el SRI conserva los NO AUTORIZADO de intentos anteriores con la misma
                // clave. Solo una decisión posterior a todas las conocidas antes del intento (reloj del
                // SRI) lo cierra; si solo hay históricas, el intento sigue pendiente: solo consulta.
                var current = auth.Decisions
                    .Where(d => d.IsNotAuthorized && document.IsCurrentAttemptSriDecision(d.DecisionDateUtc))
                    .OrderByDescending(d => d.DecisionDateUtc)
                    .FirstOrDefault();
                if (current is null)
                {
                    var pending =
                        "El SRI solo devolvió respuestas NO AUTORIZADO de intentos anteriores; el intento vigente sigue pendiente de resultado.";
                    LogHistoricalRejectionIgnored(document.Id, document.TransmissionAttempt);
                    return pending;
                }

                var description = current.Describe();
                var reason = string.IsNullOrWhiteSpace(description)
                    ? "El SRI rechazó el comprobante."
                    : description;
                document.MarkRejected(reason, userId, current.Messages);
                LogDocumentRejectedAtAuthorization(document.Id, reason);
            }
        }
        else if (auth.Status.Equals("TIMEOUT", StringComparison.OrdinalIgnoreCase))
        {
            // TIMEOUT no es una decisión del SRI: el WS de autorización sigue sin resolver tras
            // el polling interno del cliente SOAP (SriSoapClient.CheckAuthorizationAsync agota
            // sus propios 5 intentos con backoff 2s/4s/8s/16s). Se trata igual que cualquier otra
            // consulta sin respuesta definitiva (rama !authResult.IsSuccess más arriba): el
            // documento permanece en Received, y solo ApplyDeadLetterIfExhaustedAsync decide el
            // DeadLetter, tras agotar el máximo efectivo de la empresa.
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
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Documento {ElectronicDocumentId} devuelto por error interno del SRI (estado {Status}), conserva su estado y solo se consultará: {Reason}"
    )]
    private partial void LogReceptionReturnedTransient(
        Guid electronicDocumentId,
        string status,
        string reason
    );

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
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Documento {ElectronicDocumentId}: el SRI solo devolvió NO AUTORIZADO históricos; el intento {Attempt} sigue pendiente"
    )]
    private partial void LogHistoricalRejectionIgnored(Guid electronicDocumentId, int attempt);

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
        Message = "[ElectronicDocuments] Origen {SourceModule}/{SourceEntityId} ya no permite procesamiento electrónico: {Reason}"
    )]
    private partial void LogSourceNotProcessable(
        string sourceModule,
        Guid sourceEntityId,
        string reason
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Transmisión de {ElectronicDocumentId} no iniciada (empresa {CompanyId}, emisión habilitada={EmissionEnabled}): deshabilitada o pendiente sin reanudación explícita"
    )]
    private partial void LogEmissionBlocked(
        Guid electronicDocumentId,
        Guid companyId,
        bool emissionEnabled
    );

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Despacho reclamado ({State}) para {ElectronicDocumentId}, origen {SourceModule}/{SourceEntityId}"
    )]
    private partial void LogDispatchClaimed(
        Guid electronicDocumentId,
        ElectronicDocumentState state,
        string sourceModule,
        Guid sourceEntityId
    );

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Reintento de {ElectronicDocumentId} en {State}: solo consulta de autorización, sin reenvío (ADR-036)"
    )]
    private partial void LogQueryOnlyRetry(
        Guid electronicDocumentId,
        ElectronicDocumentState state
    );

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
