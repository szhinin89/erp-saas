using System.Net;
using System.Text.RegularExpressions;
using ERP.Application.Common;
using ERP.Domain.Modules.Communications.Entities;

namespace ERP.Application.Modules.Communications.Templates;

/// <summary>
/// ZH-COMMUNICATIONS-TEMPLATES-01 (ADR-039 D13) — ÚNICO renderer de Communications. Sin RazorLight,
/// sin código ni reflexión: solo placeholders <c>{{Nombre}}</c> sobre las variables explícitas de un
/// <see cref="ICommunicationTemplateModel"/>.
/// <list type="bullet">
/// <item>Fail-closed: placeholder no declarado en el contrato, placeholder mal formado/sin resolver,
/// variable obligatoria ausente o variable no declarada → fallo (nunca un correo a medias).</item>
/// <item>HTML: toda variable se escapa (<see cref="WebUtility.HtmlEncode"/>); no existe inserción cruda.
/// Texto: sin escape ni HTML. Asunto: texto de una línea (CR/LF → espacio).</item>
/// <item>Determinístico: sin cultura, reloj ni estado; los valores llegan ya formateados.</item>
/// </list>
/// Los mensajes de error nombran placeholders/variables, nunca sus valores.
/// </summary>
public static partial class CommunicationTemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*([A-Za-z][A-Za-z0-9_]*)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    /// <summary>Valida el template contra su propio contrato (sin variables).</summary>
    public static Result<bool> Validate(CommunicationTemplateDefinition template)
    {
        if (string.IsNullOrWhiteSpace(template.SubjectTemplate))
            return Invalid(template, "el asunto está vacío");
        if (string.IsNullOrWhiteSpace(template.HtmlTemplate) && string.IsNullOrWhiteSpace(template.TextTemplate))
            return Invalid(template, "no tiene cuerpo HTML ni texto");

        var declared = template.Variables.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var (part, content) in Parts(template))
        {
            foreach (Match match in Placeholder().Matches(content))
            {
                var name = match.Groups[1].Value;
                if (!declared.Contains(name))
                    return Invalid(template, $"el {part} usa el placeholder no declarado {{{{{name}}}}}");
            }

            var residue = Placeholder().Replace(content, string.Empty);
            if (residue.Contains("{{", StringComparison.Ordinal) || residue.Contains("}}", StringComparison.Ordinal))
                return Invalid(template, $"el {part} tiene un placeholder mal formado");
        }

        return Result<bool>.Success(true);
    }

    public static Result<RenderedCommunicationTemplate> Render(
        CommunicationTemplateDefinition template,
        ICommunicationTemplateModel model
    )
    {
        var validation = Validate(template);
        if (!validation.IsSuccess)
            return Result<RenderedCommunicationTemplate>.Failure(validation.Error!, validation.Code);

        if (!string.Equals(model.TemplateKey, template.Key, StringComparison.Ordinal))
            return RenderFailed(template, $"el modelo pertenece al template {model.TemplateKey}");

        var variables = model.ToVariables();
        var declared = template.Variables.ToDictionary(v => v.Name, StringComparer.Ordinal);
        var undeclared = variables.Keys.FirstOrDefault(name => !declared.ContainsKey(name));
        if (undeclared is not null)
            return RenderFailed(template, $"el modelo aporta la variable no declarada {undeclared}");

        var missing = template.Variables.FirstOrDefault(v =>
            v.Required && string.IsNullOrWhiteSpace(variables.GetValueOrDefault(v.Name))
        );
        if (missing is not null)
            return RenderFailed(template, $"falta la variable obligatoria {missing.Name}");

        var subject = Replace(template.SubjectTemplate, variables, value => value)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        var html = template.HtmlTemplate is null ? null : Replace(template.HtmlTemplate, variables, WebUtility.HtmlEncode);
        var text = template.TextTemplate is null ? null : Replace(template.TextTemplate, variables, value => value);

        if (string.IsNullOrWhiteSpace(subject) || subject.Trim().Length > CommunicationOutbox.SubjectMaxLen)
            return RenderFailed(template, $"el asunto renderizado está vacío o supera {CommunicationOutbox.SubjectMaxLen} caracteres");
        if (html?.Length > CommunicationOutbox.BodyMaxLen || text?.Length > CommunicationOutbox.BodyMaxLen)
            return RenderFailed(template, $"el cuerpo renderizado supera {CommunicationOutbox.BodyMaxLen} caracteres");

        return Result<RenderedCommunicationTemplate>.Success(new RenderedCommunicationTemplate(template.Usage, subject, html, text));
    }

    private static string Replace(
        string content,
        IReadOnlyDictionary<string, string?> variables,
        Func<string, string> encode
    ) => Placeholder().Replace(content, match => encode(variables.GetValueOrDefault(match.Groups[1].Value) ?? string.Empty));

    private static IEnumerable<(string Part, string Content)> Parts(CommunicationTemplateDefinition template)
    {
        yield return ("asunto", template.SubjectTemplate);
        if (template.HtmlTemplate is not null)
            yield return ("HTML", template.HtmlTemplate);
        if (template.TextTemplate is not null)
            yield return ("texto", template.TextTemplate);
    }

    private static Result<bool> Invalid(CommunicationTemplateDefinition template, string reason) =>
        Result<bool>.Failure(
            $"Template {template.Key} v{template.Version} ({template.Source}) inválido: {reason}.",
            ApiResponseCodes.Communications.TemplateInvalid
        );

    private static Result<RenderedCommunicationTemplate> RenderFailed(CommunicationTemplateDefinition template, string reason) =>
        Result<RenderedCommunicationTemplate>.Failure(
            $"No se pudo renderizar {template.Key} v{template.Version} ({template.Source}): {reason}.",
            ApiResponseCodes.Communications.TemplateRenderFailed
        );
}
