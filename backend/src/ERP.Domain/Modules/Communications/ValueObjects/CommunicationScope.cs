using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Domain.Modules.Communications.ValueObjects;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 (ADR-039 D3/D11) — alcance explícito de una comunicación. Solo se
/// construye por sus factories, que hacen cumplir las invariantes:
/// <list type="bullet">
/// <item><see cref="Company"/>: tenant y empresa obligatorios (no vacíos), sucursal opcional.</item>
/// <item><see cref="System"/>: sin tenant, empresa ni sucursal (null; nunca un centinela).</item>
/// </list>
/// </summary>
public sealed record CommunicationScope
{
    private CommunicationScope(
        CommunicationScopeKind kind,
        Guid? tenantId,
        Guid? companyId,
        Guid? branchId
    )
    {
        Kind = kind;
        TenantId = tenantId;
        CompanyId = companyId;
        BranchId = branchId;
    }

    public CommunicationScopeKind Kind { get; }
    public Guid? TenantId { get; }
    public Guid? CompanyId { get; }
    public Guid? BranchId { get; }

    public static CommunicationScope System { get; } =
        new(CommunicationScopeKind.System, null, null, null);

    public static CommunicationScope Company(Guid tenantId, Guid companyId, Guid? branchId = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException(
                "Una comunicación de empresa requiere TenantId.",
                nameof(tenantId)
            );
        if (companyId == Guid.Empty)
            throw new ArgumentException(
                "Una comunicación de empresa requiere CompanyId.",
                nameof(companyId)
            );

        return new(
            CommunicationScopeKind.Company,
            tenantId,
            companyId,
            branchId == Guid.Empty ? null : branchId
        );
    }

    /// <summary>Reconstruye el alcance de una fila persistida; valida la misma invariante.</summary>
    public static CommunicationScope From(
        CommunicationScopeKind kind,
        Guid? tenantId,
        Guid? companyId,
        Guid? branchId
    ) =>
        kind switch
        {
            CommunicationScopeKind.Company
                when tenantId is { } tenant && companyId is { } company => Company(
                tenant,
                company,
                branchId
            ),
            CommunicationScopeKind.System
                when tenantId is null && companyId is null && branchId is null => System,
            _ => throw new ArgumentException(
                $"Alcance de comunicación inválido: {kind} con tenant/empresa/sucursal inconsistentes."
            ),
        };
}
