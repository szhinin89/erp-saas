namespace ERP.Application.Modules.Session.DTOs;

/// <summary>
/// Sucursales autorizadas del usuario actual en la empresa operativa activa, junto con la
/// preferencia de arranque de sesión (CompanyUserPreferences) — insumo único para que el
/// frontend decida entre switch-branch automático (DirectToDefault) o selector manual
/// (AskBranch) después del login, sin reimplementar esa regla en el cliente.
/// </summary>
public sealed record MyAvailableBranchesDto(
    IReadOnlyList<AvailableBranchOptionDto> Branches,
    string LoginMode,
    Guid? DefaultBranchId);

public sealed record AvailableBranchOptionDto(Guid Id, string Name, bool IsMainBranch);
