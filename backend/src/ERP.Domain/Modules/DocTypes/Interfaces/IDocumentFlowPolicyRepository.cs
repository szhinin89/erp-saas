using ERP.Domain.Modules.DocTypes.Entities;

namespace ERP.Domain.Modules.DocTypes.Interfaces;

/// <summary>Repositorio de <see cref="DocumentFlowPolicy"/> para la pantalla de administración (DOCUMENT-FLOW-POLICY-01), no para la validación de flujo en runtime (ver <c>IDocumentFlowPolicyService</c>).</summary>
public interface IDocumentFlowPolicyRepository
{
    Task<IReadOnlyList<(DocumentFlowPolicy Policy, string DocumentTypeName)>> ListAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    );

    Task<(DocumentFlowPolicy Policy, string DocumentTypeName)?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    );

    Task SaveChangesAsync(CancellationToken ct = default);
}
