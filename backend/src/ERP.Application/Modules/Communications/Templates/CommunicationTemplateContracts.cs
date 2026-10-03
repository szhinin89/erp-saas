using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;

namespace ERP.Application.Modules.Communications.Templates;

/// <summary>
/// ZH-COMMUNICATIONS-TEMPLATES-01 (ADR-039 D12/D13) — variables de UN template, tipadas. Cada
/// template tiene su modelo (p. ej. <see cref="SalesInvoiceAuthorizedTemplateModel"/>) con solo los
/// datos que necesita, ya formateados por el módulo origen. El modelo expone sus variables de forma
/// explícita (sin reflexión): el renderer nunca navega objetos arbitrarios.
/// </summary>
public interface ICommunicationTemplateModel
{
    /// <summary>Template al que pertenece el modelo (= Purpose).</summary>
    string TemplateKey { get; }

    /// <summary>Nombre de variable → valor (texto plano; el renderer escapa en HTML).</summary>
    IReadOnlyDictionary<string, string?> ToVariables();
}

/// <summary>Variable declarada por el contrato de un template.</summary>
public sealed record CommunicationTemplateVariable(string Name, bool Required = true);

/// <summary>
/// Template resuelto (default o override de empresa) listo para renderizar. <see cref="Variables"/> es
/// el contrato del default: un override no puede usar placeholders fuera de él.
/// </summary>
public sealed record CommunicationTemplateDefinition(
    string Key,
    int Version,
    CommunicationTemplateSource Source,
    string SubjectTemplate,
    string? HtmlTemplate,
    string? TextTemplate,
    IReadOnlyCollection<CommunicationTemplateVariable> Variables
)
{
    public CommunicationTemplateUsage Usage => new(Key, Version, Source);
}

/// <summary>Resultado del render: lo que se persiste en la comunicación (y nunca se re-renderiza).</summary>
public sealed record RenderedCommunicationTemplate(
    CommunicationTemplateUsage Usage,
    string Subject,
    string? Html,
    string? Text
);

/// <summary>
/// Fallo de template al encolar (antes de persistir o enviar). <see cref="Code"/> es un valor de
/// <c>ApiResponseCodes.Communications</c>: la decisión semántica nunca depende del texto. El mensaje
/// nombra la variable o el placeholder, nunca su valor.
/// </summary>
public sealed class CommunicationTemplateException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
