using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Enums;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Domain.Modules.Communications.ValueObjects;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 (ADR-039 D8) — ÚNICO constructor de la identidad lógica (y de la
/// <c>IdempotencyKey</c>) de una comunicación. Deriva solo de datos estables:
/// alcance + propósito + canal + origen (módulo, tipo, id) + rol del destinatario + secuencia de
/// reenvío. Nunca del email, asunto, cuerpo, plantilla renderizada, instantes ni configuración SMTP.
/// <para>
/// <see cref="Key"/> = <c>cid:v1:</c> + SHA-256 (hex) de la forma canónica: determinística, de
/// largo fijo (71) y segura para índice. Retry = misma comunicación y misma identidad; reenvío
/// manual = identidad explícitamente distinta (<see cref="ForResend"/>), nunca una colisión.
/// </para>
/// </summary>
public sealed record CommunicationIdentity
{
    private const string Version = "v1";

    private CommunicationIdentity(
        CommunicationScope scope,
        string purpose,
        CommunicationChannel channel,
        CommunicationSource source,
        CommunicationRecipientRole recipientRole,
        int resendSequence
    )
    {
        Scope = scope;
        Purpose = purpose;
        Channel = channel;
        Source = source;
        RecipientRole = recipientRole;
        ResendSequence = resendSequence;
        Key = $"cid:{Version}:{Hash(Canonical())}";
    }

    public CommunicationScope Scope { get; }
    public string Purpose { get; }
    public CommunicationChannel Channel { get; }
    public CommunicationSource Source { get; }
    public CommunicationRecipientRole RecipientRole { get; }

    /// <summary>0 = comunicación original; n ≥ 1 = n-ésimo reenvío manual.</summary>
    public int ResendSequence { get; }

    public string Key { get; }

    public static CommunicationIdentity For(
        CommunicationScope scope,
        string purpose,
        CommunicationChannel channel,
        CommunicationSource source,
        CommunicationRecipientRole recipientRole
    )
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(source);
        var definition = CommunicationPurposes.Get(purpose);

        if (definition.ScopeKind != scope.Kind)
            throw new ArgumentException(
                $"El propósito {definition.Code} solo admite alcance {definition.ScopeKind}, no {scope.Kind}.",
                nameof(scope)
            );
        if (!definition.Channels.Contains(channel))
            throw new ArgumentException($"El propósito {definition.Code} no admite el canal {channel}.", nameof(channel));
        if (!Enum.IsDefined(recipientRole))
            throw new ArgumentException("Rol de destinatario inválido.", nameof(recipientRole));

        return new(scope, definition.Code, channel, source, recipientRole, resendSequence: 0);
    }

    /// <summary>Identidad del n-ésimo reenvío manual de esta comunicación (nueva comunicación).</summary>
    public CommunicationIdentity ForResend(int sequence)
    {
        if (sequence < 1)
            throw new ArgumentOutOfRangeException(nameof(sequence), "La secuencia de reenvío empieza en 1.");
        if (!CommunicationPurposes.Get(Purpose).AllowsManualResend)
            throw new DomainRuleViolationException($"El propósito {Purpose} no admite reenvío manual.");

        return new(Scope, Purpose, Channel, Source, RecipientRole, sequence);
    }

    private string Canonical() =>
        string.Join(
            '|',
            Version,
            Scope.Kind,
            Scope.TenantId?.ToString("N") ?? "-",
            Scope.CompanyId?.ToString("N") ?? "-",
            Purpose,
            Channel,
            Source.Module,
            Source.Type,
            Source.Id.ToString("N"),
            RecipientRole,
            ResendSequence
        );

    private static string Hash(string canonical) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
}
