using ERP.Domain.Common;
using ERP.Domain.Modules.DocTypes.Enums;

namespace ERP.Domain.Modules.DocTypes.Entities;

/// <summary>
/// Política por company de cómo se maneja un <see cref="DocType"/>: si está habilitado y si/cómo
/// admite borrador. Una fila = una company + un doc type. Sin fila explícita, el servicio de
/// aplicación resuelve un default seguro compatible con el comportamiento previo a esta entidad
/// (ver <c>IDocWorkflowPolicyService</c>).
/// </summary>
public sealed class DocWorkflowPolicy : AuditableEntity, ITenantScopedEntity, ICompanyScopedEntity
{
    public Guid CompanyId { get; private set; }
    public string DocTypeCode { get; private set; } = null!;
    public bool IsEnabled { get; private set; }
    public DraftMode DraftMode { get; private set; }
    public DocWorkflowDefaultAction DefaultAction { get; private set; }

    private DocWorkflowPolicy() { }

    public static DocWorkflowPolicy Create(
        Guid tenantId,
        Guid companyId,
        string docTypeCode,
        bool isEnabled,
        DraftMode draftMode,
        DocWorkflowDefaultAction defaultAction,
        Guid createdBy
    )
    {
        var policy = new DocWorkflowPolicy
        {
            TenantId = tenantId,
            CompanyId = companyId,
            DocTypeCode = docTypeCode,
            IsEnabled = isEnabled,
            DraftMode = draftMode,
            DefaultAction = defaultAction,
        };
        policy.SetCreated(createdBy);
        return policy;
    }

    public void Update(bool isEnabled, DraftMode draftMode, DocWorkflowDefaultAction defaultAction, Guid updatedBy)
    {
        IsEnabled = isEnabled;
        DraftMode = draftMode;
        DefaultAction = defaultAction;
        SetUpdated(updatedBy);
    }
}
