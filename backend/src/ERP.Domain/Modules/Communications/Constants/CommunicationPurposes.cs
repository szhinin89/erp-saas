using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Domain.Modules.Communications.Constants;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 — registro único de propósitos (ADR-039 D3/D12/D16). El código es
/// también la TemplateKey (D12). Encolar un propósito no registrado, o con un scope/canal que su
/// definición no permite, falla antes de persistir.
/// </summary>
public static class CommunicationPurposes
{
    public const string SalesInvoiceAuthorized = "SALES_INVOICE_AUTHORIZED";

    /// <summary>
    /// Contrato reservado (scope System, sensible): sin productor hasta la fase 6 de ADR-039; hoy la
    /// recuperación de contraseña usa la entrega simulada de Auth.
    /// </summary>
    public const string PasswordReset = "PASSWORD_RESET";

    private static readonly IReadOnlyDictionary<string, CommunicationPurposeDefinition> Definitions =
        new[]
        {
            new CommunicationPurposeDefinition(
                SalesInvoiceAuthorized,
                CommunicationScopeKind.Company,
                [CommunicationChannel.Email],
                IsSensitive: false,
                AllowsManualResend: true
            ),
            new CommunicationPurposeDefinition(
                PasswordReset,
                CommunicationScopeKind.System,
                [CommunicationChannel.Email],
                IsSensitive: true,
                AllowsManualResend: false
            ),
        }.ToDictionary(d => d.Code, StringComparer.Ordinal);

    public static IEnumerable<CommunicationPurposeDefinition> All => Definitions.Values;

    public static CommunicationPurposeDefinition Get(string code) =>
        Definitions.TryGetValue(code, out var definition)
            ? definition
            : throw new ArgumentException($"Propósito de comunicación no registrado: '{code}'.", nameof(code));
}

/// <param name="Code">Código estable (= TemplateKey).</param>
/// <param name="ScopeKind">Único alcance permitido.</param>
/// <param name="Channels">Canales permitidos.</param>
/// <param name="IsSensitive">Lleva datos sensibles (token): nunca se persisten ni registran en claro (D16).</param>
/// <param name="AllowsManualResend">Admite reenvío manual explícito (nueva comunicación).</param>
public sealed record CommunicationPurposeDefinition(
    string Code,
    CommunicationScopeKind ScopeKind,
    IReadOnlyCollection<CommunicationChannel> Channels,
    bool IsSensitive,
    bool AllowsManualResend
);
