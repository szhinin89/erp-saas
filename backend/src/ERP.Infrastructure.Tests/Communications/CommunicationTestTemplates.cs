using ERP.Application.Common;
using ERP.Application.Modules.Communications.Templates;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;

namespace ERP.Infrastructure.Tests.Communications;

/// <summary>
/// Soporte de pruebas de templates. PASSWORD_RESET es un propósito reservado sin template productivo
/// hasta la fase 6: solo las pruebas ESTRUCTURALES del alcance System usan esta definición (sin token).
/// </summary>
internal static class CommunicationTestTemplates
{
    public static readonly CommunicationTemplateDefinition StructuralPasswordReset = new(
        CommunicationPurposes.PasswordReset,
        Version: 1,
        CommunicationTemplateSource.Default,
        SubjectTemplate: "Recupera tu acceso",
        HtmlTemplate: null,
        TextTemplate: "Hola {{UserName}}: instrucciones de prueba estructural.",
        Variables: [new CommunicationTemplateVariable("UserName")]
    );

    public sealed record PasswordResetModel(string UserName) : ICommunicationTemplateModel
    {
        public string TemplateKey => CommunicationPurposes.PasswordReset;

        public IReadOnlyDictionary<string, string?> ToVariables() =>
            new Dictionary<string, string?> { [nameof(UserName)] = UserName };
    }

    public static SalesInvoiceAuthorizedTemplateModel Invoice(string customerName = "Cliente Demo") =>
        new(customerName, "001-001-000000001", "2108202601179214672100110010010000000011234567811", "100.00", "ZH Demo");

    /// <summary>Resolver real para templates productivos + la definición estructural de PASSWORD_RESET.</summary>
    public sealed class WithStructuralSystemTemplate(ICommunicationTemplateResolver inner) : ICommunicationTemplateResolver
    {
        public Task<Result<CommunicationTemplateDefinition>> ResolveAsync(CommunicationScope scope, string templateKey, CancellationToken ct = default) =>
            templateKey == CommunicationPurposes.PasswordReset
                ? Task.FromResult(Result<CommunicationTemplateDefinition>.Success(StructuralPasswordReset))
                : inner.ResolveAsync(scope, templateKey, ct);
    }
}
