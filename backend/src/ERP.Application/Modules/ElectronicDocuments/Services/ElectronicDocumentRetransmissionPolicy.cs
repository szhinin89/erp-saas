using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>Procedencia de un rechazo: DEVUELTA de Recepción o NO AUTORIZADO de Autorización.</summary>
public enum ElectronicDocumentRejectionStage
{
    Reception = 1,
    Authorization = 2,
}

/// <summary>Motivo de bloqueo de la retransmisión corregida — <see cref="Code"/> estable para API y UI.</summary>
public sealed record ElectronicDocumentRetransmissionBlock(string Code, string Message);

/// <summary>
/// Elegibilidad calculada por el backend (R12 Fase B). <see cref="Eligible"/> habilita la acción
/// explícita; la corrección real se demuestra recién al regenerar el XML.
/// </summary>
public sealed record ElectronicDocumentRetransmissionEligibility(
    bool Eligible,
    int TransmissionAttempt,
    ElectronicDocumentRejectionStage? RejectionStage,
    IReadOnlyList<string> RejectionCodes,
    IReadOnlyList<ElectronicDocumentRetransmissionBlock> Blocks
);

/// <summary>
/// R12 Fase B — SSOT de "¿puede este comprobante rechazado corregirse y retransmitirse con la misma
/// clave?". Ficha Técnica Offline 2.34, tabla de errores, nota 1: el comprobante NO AUTORIZADO puede
/// reenviarse corregido con la misma clave de acceso. Habilita solo los errores del archivo
/// <see cref="CorrectableCodes"/>; todo otro código (o un rechazo sin códigos) queda bloqueado
/// preventivamente. La procedencia (DEVUELTA / NO AUTORIZADO) se toma del timeline, nunca del código.
/// Función pura: el emisor la evalúa con la evidencia persistida.
/// </summary>
public static class ElectronicDocumentRetransmissionPolicy
{
    public const string BlockNotRejected = "NOT_REJECTED";
    public const string BlockUnsupportedType = "UNSUPPORTED_DOCUMENT_TYPE";
    public const string BlockMissingEvidence = "MISSING_EVIDENCE";
    public const string BlockSourceNotProcessable = "SOURCE_NOT_PROCESSABLE";
    public const string BlockUnknownOrigin = "UNKNOWN_REJECTION_ORIGIN";
    public const string BlockNoErrorCodes = "NO_SRI_ERROR_CODES";
    public const string BlockCodesNotCorrectable = "CODES_NOT_CORRECTABLE";

    /// <summary>
    /// Errores del archivo corregibles sin cambiar la clave: 35 documento inválido (estructura), 36
    /// versión de esquema, 47 tipo de comprobante, 48 esquema XSD, 52 diferencias de cálculo, 58
    /// estructura de la clave de acceso y 67 fecha inválida (formato). Decisión R12 del propietario.
    /// </summary>
    public static readonly IReadOnlySet<string> CorrectableCodes = new HashSet<string>(
        ["35", "36", "47", "48", "52", "58", "67"],
        StringComparer.Ordinal
    );

    public static readonly IReadOnlySet<ElectronicDocumentType> SupportedDocumentTypes =
        new HashSet<ElectronicDocumentType>
        {
            ElectronicDocumentType.Invoice,
            ElectronicDocumentType.CreditNote,
            ElectronicDocumentType.Retention,
        };

    /// <summary>
    /// Procedencia por el estado desde el que se rechazó: solo Recepción rechaza desde Sent (MarkSent y
    /// MarkRejected en la misma respuesta DEVUELTA); Autorización rechaza desde Signed/Dispatching/Received.
    /// </summary>
    public static ElectronicDocumentRejectionStage? StageOf(ElectronicDocumentState? fromState) =>
        fromState switch
        {
            ElectronicDocumentState.Sent => ElectronicDocumentRejectionStage.Reception,
            ElectronicDocumentState.Signed
            or ElectronicDocumentState.Dispatching
            or ElectronicDocumentState.Received => ElectronicDocumentRejectionStage.Authorization,
            _ => null,
        };

    public static ElectronicDocumentRetransmissionEligibility Evaluate(
        ElectronicDocument document,
        ElectronicDocumentSourceLifecycle? source,
        ElectronicDocumentRejectionEvidence? evidence
    )
    {
        var blocks = new List<ElectronicDocumentRetransmissionBlock>();
        if (document.CurrentState != ElectronicDocumentState.Rejected)
            return new(
                false,
                document.TransmissionAttempt,
                null,
                [],
                [new(BlockNotRejected, "Solo un comprobante rechazado por el SRI puede corregirse y retransmitirse.")]
            );

        if (!SupportedDocumentTypes.Contains(document.DocumentType))
            blocks.Add(new(BlockUnsupportedType, "La retransmisión corregida solo está habilitada para Factura, Nota de crédito y Retención."));

        if (document.AccessKey is null || string.IsNullOrWhiteSpace(document.SignedXmlPath))
            blocks.Add(new(BlockMissingEvidence, "El comprobante no conserva la clave de acceso o el XML firmado del intento rechazado."));

        if (source is null)
            blocks.Add(new(BlockSourceNotProcessable, "El módulo de origen no protege el reclamo de envío; la retransmisión no está habilitada."));
        else if (!source.AllowsProcessing)
            blocks.Add(new(BlockSourceNotProcessable, source.Reason ?? "El documento de origen ya no permite procesamiento electrónico."));

        var stage = StageOf(evidence?.FromState);
        if (stage is null)
            blocks.Add(new(BlockUnknownOrigin, "No hay evidencia registrada de si el rechazo fue DEVUELTA (recepción) o NO AUTORIZADO (autorización)."));

        // Solo los mensajes de tipo ERROR motivan el rechazo; las advertencias no lo deciden.
        var errorCodes = (evidence?.Messages ?? [])
            .Where(m => string.Equals(m.MessageType, "ERROR", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Code?.Trim())
            .ToList();
        var codes = errorCodes
            .Where(c => !string.IsNullOrEmpty(c))
            .Select(c => c!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (codes.Count == 0)
            blocks.Add(new(BlockNoErrorCodes, "El rechazo no registra códigos de error del SRI que permitan clasificarlo."));
        else if (errorCodes.Any(string.IsNullOrEmpty))
            blocks.Add(new(BlockNoErrorCodes, "El rechazo contiene errores del SRI sin código; no puede clasificarse como corregible."));

        var blocked = codes.Where(c => !CorrectableCodes.Contains(c)).ToList();
        if (blocked.Count > 0)
            blocks.Add(new(
                BlockCodesNotCorrectable,
                $"Los códigos SRI {string.Join(", ", blocked)} no son corregibles con la misma clave de acceso; la retransmisión queda bloqueada."
            ));

        return new(blocks.Count == 0, document.TransmissionAttempt, stage, codes, blocks);
    }
}
