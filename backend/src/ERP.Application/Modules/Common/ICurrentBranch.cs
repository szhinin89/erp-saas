namespace ERP.Application.Common;

/// <summary>
/// Contexto operativo ERP: sucursal activa dentro de la empresa operativa actual.
/// Fase I-2 — solo transporte (header X-Branch-Id), sin validación de autorización todavía
/// (ver IBranchAccessGuard, fase posterior). No reutilizar <see cref="ICurrentCompany"/> para
/// datos de sucursal.
/// </summary>
public interface ICurrentBranch
{
    /// <summary>Sucursal activa del request. <see cref="Guid.Empty"/> si no hay header.</summary>
    Guid BranchId { get; }

    bool IsAuthenticated { get; }

    /// <summary>True cuando <see cref="BranchId"/> está presente en el header X-Branch-Id.</summary>
    bool HasBranchContext { get; }
}
