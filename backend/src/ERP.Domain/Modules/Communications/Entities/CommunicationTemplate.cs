using ERP.Domain.Common;
using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Domain.Modules.Communications.Entities;

public sealed class CommunicationTemplate
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public const int CodeMaxLen = 100;
    public const int NameMaxLen = 200;
    public const int SubjectTemplateMaxLen = 300;
    public const int BodyTemplateMaxLen = 16000;
    public const int LanguageMaxLen = 10;

    public Guid CompanyId { get; private set; }
    public Guid? BranchId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public CommunicationChannel Channel { get; private set; }
    public string SubjectTemplate { get; private set; } = null!;
    public string? HtmlTemplate { get; private set; }
    public string? TextTemplate { get; private set; }
    public string Language { get; private set; } = "es";
    public bool IsActive { get; private set; }

    private CommunicationTemplate() { }

    public static CommunicationTemplate Create(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        string code,
        string name,
        CommunicationChannel channel,
        string subjectTemplate,
        string? htmlTemplate,
        string? textTemplate,
        string language,
        Guid createdBy
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId es obligatorio.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId es obligatorio.", nameof(companyId));
        if (string.IsNullOrWhiteSpace(htmlTemplate) && string.IsNullOrWhiteSpace(textTemplate))
            throw new ArgumentException("La plantilla debe tener cuerpo HTML o texto.", nameof(textTemplate));

        var template = new CommunicationTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId == Guid.Empty ? null : branchId,
            Code = Required(code, CodeMaxLen, nameof(code)).ToUpperInvariant(),
            Name = Required(name, NameMaxLen, nameof(name)),
            Channel = channel,
            SubjectTemplate = Required(subjectTemplate, SubjectTemplateMaxLen, nameof(subjectTemplate)),
            HtmlTemplate = Optional(htmlTemplate, BodyTemplateMaxLen, nameof(htmlTemplate)),
            TextTemplate = Optional(textTemplate, BodyTemplateMaxLen, nameof(textTemplate)),
            Language = Required(language, LanguageMaxLen, nameof(language)).ToLowerInvariant(),
            IsActive = true,
        };
        template.SetCreated(createdBy);
        return template;
    }

    public void UpdateContent(
        string name,
        string subjectTemplate,
        string? htmlTemplate,
        string? textTemplate,
        Guid updatedBy
    )
    {
        if (string.IsNullOrWhiteSpace(htmlTemplate) && string.IsNullOrWhiteSpace(textTemplate))
            throw new ArgumentException("La plantilla debe tener cuerpo HTML o texto.", nameof(textTemplate));

        Name = Required(name, NameMaxLen, nameof(name));
        SubjectTemplate = Required(subjectTemplate, SubjectTemplateMaxLen, nameof(subjectTemplate));
        HtmlTemplate = Optional(htmlTemplate, BodyTemplateMaxLen, nameof(htmlTemplate));
        TextTemplate = Optional(textTemplate, BodyTemplateMaxLen, nameof(textTemplate));
        SetUpdated(updatedBy);
    }

    public void Activate(Guid updatedBy)
    {
        IsActive = true;
        SetUpdated(updatedBy);
    }

    public void Deactivate(Guid updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }

    private static string Required(string value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El valor es obligatorio.", paramName);
        return Trim(value, maxLength, paramName);
    }

    private static string? Optional(string? value, int maxLength, string paramName) =>
        string.IsNullOrWhiteSpace(value) ? null : Trim(value, maxLength, paramName);

    private static string Trim(string value, int maxLength, string paramName)
    {
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"El valor no puede superar {maxLength} caracteres.", paramName);
        return normalized;
    }
}
