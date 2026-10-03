using ERP.Application.Common;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.Interfaces;
using ERP.Domain.Modules.Communications.ValueObjects;

namespace ERP.Application.Modules.Communications.Templates;

/// <summary>
/// ZH-COMMUNICATIONS-TEMPLATES-01 (ADR-039 D12) — ÚNICO punto que decide qué template usa una
/// comunicación: <c>alcance + TemplateKey → definición</c>. No renderiza ni conoce el transporte.
/// </summary>
public interface ICommunicationTemplateResolver
{
    Task<Result<CommunicationTemplateDefinition>> ResolveAsync(
        CommunicationScope scope,
        string templateKey,
        CancellationToken ct = default
    );
}

/// <summary>
/// <list type="bullet">
/// <item>Sin default registrado → <c>COMMUNICATION_TEMPLATE_NOT_FOUND</c> (no se inventa contenido).</item>
/// <item>System → siempre el default (nunca consulta overrides de una empresa).</item>
/// <item>Company → override ACTIVO de ESA empresa (tenant + empresa + clave + canal + idioma) si
/// existe; si no, el default.</item>
/// <item>Override inválido (placeholder fuera del contrato del default, mal formado, sin asunto o
/// cuerpo) → <c>COMMUNICATION_TEMPLATE_INVALID</c>, SIN fallback: el administrador configuró un template
/// y ocultar el error enviaría algo distinto de lo que espera.</item>
/// </list>
/// </summary>
public sealed class CommunicationTemplateResolver : ICommunicationTemplateResolver
{
    private readonly ICommunicationTemplateRepository _overrides;

    public CommunicationTemplateResolver(ICommunicationTemplateRepository overrides)
    {
        _overrides = overrides;
    }

    public async Task<Result<CommunicationTemplateDefinition>> ResolveAsync(
        CommunicationScope scope,
        string templateKey,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(scope);
        var fallbackDefault = CommunicationDefaultTemplates.Find(templateKey);
        if (fallbackDefault is null)
            return Result<CommunicationTemplateDefinition>.Failure(
                $"No existe template para {templateKey}.",
                ApiResponseCodes.Communications.TemplateNotFound
            );

        if (scope.Kind == CommunicationScopeKind.System)
            return Result<CommunicationTemplateDefinition>.Success(fallbackDefault);

        var companyOverride = await _overrides.GetActiveAsync(
            scope.TenantId!.Value,
            scope.CompanyId!.Value,
            CommunicationChannel.Email,
            templateKey,
            CommunicationDefaultTemplates.Language,
            ct
        );
        if (companyOverride is null)
            return Result<CommunicationTemplateDefinition>.Success(fallbackDefault);

        var definition = new CommunicationTemplateDefinition(
            fallbackDefault.Key,
            companyOverride.Revision,
            CommunicationTemplateSource.CompanyOverride,
            companyOverride.SubjectTemplate,
            companyOverride.HtmlTemplate,
            companyOverride.TextTemplate,
            fallbackDefault.Variables
        );

        var validation = CommunicationTemplateRenderer.Validate(definition);
        return validation.IsSuccess
            ? Result<CommunicationTemplateDefinition>.Success(definition)
            : Result<CommunicationTemplateDefinition>.Failure(validation.Error!, validation.Code);
    }
}
