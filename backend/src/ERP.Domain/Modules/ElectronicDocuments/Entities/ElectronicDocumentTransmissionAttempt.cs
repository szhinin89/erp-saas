using ERP.Domain.Common;

namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>
/// R12 Fase B — evidencia append-only de una retransmisión corregida reclamada para un
/// <see cref="ElectronicDocument"/>: quién la pidió, por qué, sobre qué rechazo, qué cambió y los
/// archivos (anterior y nuevo) con su SHA-256. Se inserta en la misma transacción que el reclamo
/// (<see cref="ElectronicDocument.ClaimCorrectedRetransmission"/>); los índices únicos por
/// (documento, intento) y (documento, clave de idempotencia) garantizan a lo sumo un reclamo por
/// intento y por solicitud. Nunca se edita ni se borra.
/// </summary>
public sealed class ElectronicDocumentTransmissionAttempt
    : ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public const int ReasonMaxLen = ElectronicDocument.RetransmissionReasonMaxLen;
    public const int RejectionStageMaxLen = 20;
    public const int RejectionCodesMaxLen = 200;
    public const int CorrectionSummaryMaxLen = 2000;
    public const int Sha256HexLen = 64;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid ElectronicDocumentId { get; private set; }

    /// <summary>Intento de transmisión que inicia este reclamo (≥ 2; el 1 es la emisión original).</summary>
    public int AttemptNumber { get; private set; }

    public Guid IdempotencyKey { get; private set; }
    public string Reason { get; private set; } = null!;
    public Guid RequestedBy { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }

    /// <summary>Procedencia del rechazo corregido: "Reception" (DEVUELTA) o "Authorization" (NO AUTORIZADO).</summary>
    public string RejectionStage { get; private set; } = null!;

    /// <summary>Identificadores SRI de error del rechazo corregido, separados por coma.</summary>
    public string RejectionCodes { get; private set; } = null!;

    /// <summary><c>fechaAutorizacion</c> (UTC) de la última respuesta NO AUTORIZADO conocida antes del reclamo.</summary>
    public DateTime? SupersededSriDecisionUtc { get; private set; }

    public string PreviousSignedXmlPath { get; private set; } = null!;
    public string PreviousSignedXmlSha256 { get; private set; } = null!;
    public string DraftXmlPath { get; private set; } = null!;
    public string DraftXmlSha256 { get; private set; } = null!;
    public string SignedXmlPath { get; private set; } = null!;
    public string SignedXmlSha256 { get; private set; } = null!;

    /// <summary>Diferencias semánticas demostradas entre el XML anterior y el corregido (sin la firma).</summary>
    public string CorrectionSummary { get; private set; } = null!;

    private ElectronicDocumentTransmissionAttempt() { }

    public static ElectronicDocumentTransmissionAttempt Create(
        ElectronicDocument document,
        int attemptNumber,
        Guid idempotencyKey,
        string reason,
        Guid requestedBy,
        string rejectionStage,
        IReadOnlyCollection<string> rejectionCodes,
        DateTime? supersededSriDecisionUtc,
        string previousSignedXmlPath,
        string previousSignedXmlSha256,
        string draftXmlPath,
        string draftXmlSha256,
        string signedXmlPath,
        string signedXmlSha256,
        string correctionSummary
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        if (attemptNumber < 2)
            throw new ArgumentOutOfRangeException(
                nameof(attemptNumber),
                "Una retransmisión inicia al menos el intento 2."
            );
        if (idempotencyKey == Guid.Empty)
            throw new ArgumentException("La clave de idempotencia es obligatoria.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo es obligatorio.", nameof(reason));
        if (string.IsNullOrWhiteSpace(correctionSummary))
            throw new ArgumentException(
                "La corrección demostrada es obligatoria.",
                nameof(correctionSummary)
            );
        if (rejectionCodes.Count == 0)
            throw new ArgumentException("El rechazo corregido debe tener códigos SRI.", nameof(rejectionCodes));

        return new ElectronicDocumentTransmissionAttempt
        {
            Id = Guid.NewGuid(),
            TenantId = document.TenantId,
            CompanyId = document.CompanyId,
            ElectronicDocumentId = document.Id,
            AttemptNumber = attemptNumber,
            IdempotencyKey = idempotencyKey,
            Reason = Truncate(reason.Trim(), ReasonMaxLen),
            RequestedBy = requestedBy,
            RequestedAtUtc = DateTime.UtcNow,
            RejectionStage = rejectionStage,
            RejectionCodes = Truncate(string.Join(",", rejectionCodes), RejectionCodesMaxLen),
            SupersededSriDecisionUtc = supersededSriDecisionUtc,
            PreviousSignedXmlPath = previousSignedXmlPath,
            PreviousSignedXmlSha256 = RequireSha256(previousSignedXmlSha256),
            DraftXmlPath = draftXmlPath,
            DraftXmlSha256 = RequireSha256(draftXmlSha256),
            SignedXmlPath = signedXmlPath,
            SignedXmlSha256 = RequireSha256(signedXmlSha256),
            CorrectionSummary = Truncate(correctionSummary, CorrectionSummaryMaxLen),
        };
    }

    private static string RequireSha256(string value) =>
        value is { Length: Sha256HexLen } && value.All(Uri.IsHexDigit)
            ? value.ToLowerInvariant()
            : throw new ArgumentException("Se esperaba un SHA-256 hexadecimal de 64 caracteres.");

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
