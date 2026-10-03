using ERP.Application.Modules.Communications.Templates;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Application.Tests.TestSupport;

/// <summary>
/// SOLO PRUEBAS ESTRUCTURALES del alcance System: PASSWORD_RESET es un propósito reservado sin
/// template productivo hasta la fase 6 (ADR-039). Nunca contiene un token.
/// </summary>
internal static class StructuralPasswordResetTemplate
{
    public static readonly CommunicationTemplateDefinition Definition = new(
        CommunicationPurposes.PasswordReset,
        Version: 1,
        CommunicationTemplateSource.Default,
        SubjectTemplate: "Recupera tu acceso",
        HtmlTemplate: null,
        TextTemplate: "Hola {{UserName}}: instrucciones de prueba estructural.",
        Variables: [new CommunicationTemplateVariable("UserName")]
    );

    public sealed record Model(string UserName) : ICommunicationTemplateModel
    {
        public string TemplateKey => CommunicationPurposes.PasswordReset;

        public IReadOnlyDictionary<string, string?> ToVariables() =>
            new Dictionary<string, string?> { [nameof(UserName)] = UserName };
    }
}
